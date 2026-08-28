// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EntityProxyLockAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0308";

        private const string OrchestrationContextMetadataName = "Microsoft.Azure.WebJobs.Extensions.DurableTask.IDurableOrchestrationContext";
        private const string EntityIdMetadataName = "Microsoft.Azure.WebJobs.Extensions.DurableTask.EntityId";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityProxyLockAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityProxyLockAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityProxyLockAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.EntityInterface;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            Severity,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol orchestrationContextType = compilationContext.Compilation.GetTypeByMetadataName(OrchestrationContextMetadataName);
                INamedTypeSymbol entityIdType = compilationContext.Compilation.GetTypeByMetadataName(EntityIdMetadataName);
                if (orchestrationContextType == null || entityIdType == null)
                {
                    return;
                }

                var implementationResolver = new EntityImplementationResolver();
                compilationContext.RegisterSyntaxNodeAction(
                    syntaxContext => AnalyzeInvocation(
                        syntaxContext,
                        orchestrationContextType,
                        entityIdType,
                        implementationResolver),
                    SyntaxKind.InvocationExpression);
            });
        }

        private static void AnalyzeInvocation(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol orchestrationContextType,
            INamedTypeSymbol entityIdType,
            EntityImplementationResolver implementationResolver)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!(context.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invokedMethod)
                || !invokedMethod.ReturnsVoid
                || !TryGetInvocationReceiver(
                    invocation,
                    out ExpressionSyntax receiver,
                    out SimpleNameSyntax methodName)
                || !TryGetProxyCreation(
                    receiver,
                    invocation,
                    context.SemanticModel,
                    orchestrationContextType,
                    entityIdType,
                    new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                    out ExpressionSyntax proxyTarget,
                    out INamedTypeSymbol proxyInterface)
                || !IsProxyInterfaceMethod(invokedMethod, proxyInterface))
            {
                return;
            }

            foreach (InvocationExpressionSyntax lockInvocation in GetActiveLockInvocations(
                invocation,
                context.SemanticModel,
                orchestrationContextType,
                entityIdType))
            {
                foreach (ExpressionSyntax lockedEntity in GetLockedEntityExpressions(
                    lockInvocation,
                    context.SemanticModel,
                    entityIdType,
                    new HashSet<ISymbol>(SymbolEqualityComparer.Default)))
                {
                    if (AreEquivalentEntityExpressions(proxyTarget, lockedEntity, context.SemanticModel)
                        || AreStringKeyProxyAndEntityIdEquivalent(
                            proxyTarget,
                            proxyInterface,
                            lockedEntity,
                            context.SemanticModel,
                            implementationResolver))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Rule,
                            methodName.GetLocation(),
                            invokedMethod.Name));
                        return;
                    }
                }
            }
        }

        private static bool TryGetInvocationReceiver(
            InvocationExpressionSyntax invocation,
            out ExpressionSyntax receiver,
            out SimpleNameSyntax methodName)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                receiver = memberAccess.Expression;
                methodName = memberAccess.Name;
                return true;
            }

            if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
            {
                ConditionalAccessExpressionSyntax conditionalAccess = invocation.Ancestors()
                    .OfType<ConditionalAccessExpressionSyntax>()
                    .FirstOrDefault(candidate => candidate.WhenNotNull.Span.Contains(invocation.Span));
                if (conditionalAccess != null)
                {
                    receiver = conditionalAccess.Expression;
                    methodName = memberBinding.Name;
                    return true;
                }
            }

            receiver = null;
            methodName = null;
            return false;
        }

        private static bool TryGetProxyCreation(
            ExpressionSyntax expression,
            InvocationExpressionSyntax proxyMethodInvocation,
            SemanticModel semanticModel,
            INamedTypeSymbol orchestrationContextType,
            INamedTypeSymbol entityIdType,
            HashSet<ISymbol> visitedSymbols,
            out ExpressionSyntax proxyTarget,
            out INamedTypeSymbol proxyInterface)
        {
            expression = UnwrapExpression(expression);
            if (expression is InvocationExpressionSyntax creationInvocation
                && TryGetProxyCreationTarget(
                    creationInvocation,
                    semanticModel,
                    orchestrationContextType,
                    entityIdType,
                    out proxyTarget,
                    out proxyInterface))
            {
                return true;
            }

            ISymbol receiverSymbol = semanticModel.GetSymbolInfo(expression).Symbol;
            if (!(receiverSymbol is ILocalSymbol localSymbol)
                || !visitedSymbols.Add(localSymbol))
            {
                proxyTarget = null;
                proxyInterface = null;
                return false;
            }

            foreach (SyntaxReference declarationReference in localSymbol.DeclaringSyntaxReferences)
            {
                if (declarationReference.GetSyntax() is VariableDeclaratorSyntax declarator
                    && declarator.SpanStart < proxyMethodInvocation.SpanStart
                    && declarator.Initializer != null
                    && TryGetProxyCreation(
                        declarator.Initializer.Value,
                        proxyMethodInvocation,
                        semanticModel,
                        orchestrationContextType,
                        entityIdType,
                        visitedSymbols,
                        out proxyTarget,
                        out proxyInterface))
                {
                    if (HasWriteBetweenDeclarationAndUse(
                        localSymbol,
                        proxyMethodInvocation,
                        semanticModel))
                    {
                        proxyTarget = null;
                        proxyInterface = null;
                        return false;
                    }

                    return true;
                }
            }

            proxyTarget = null;
            proxyInterface = null;
            return false;
        }

        private static bool TryGetProxyCreationTarget(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            INamedTypeSymbol orchestrationContextType,
            INamedTypeSymbol entityIdType,
            out ExpressionSyntax proxyTarget,
            out INamedTypeSymbol proxyInterface)
        {
            ExpressionSyntax targetArgument = invocation.ArgumentList.Arguments.Count == 1
                ? invocation.ArgumentList.Arguments[0].Expression
                : null;
            ITypeSymbol argumentType = targetArgument == null
                ? null
                : semanticModel.GetTypeInfo(targetArgument).Type;
            IMethodSymbol[] methods = GetMethodSymbols(semanticModel, invocation)
                .Where(candidate =>
                    candidate.Name == "CreateEntityProxy"
                    && candidate.TypeArguments.Length == 1
                    && candidate.Parameters.Length == 1
                    && IsOrchestrationContextMethod(candidate, orchestrationContextType))
                .Where(candidate =>
                    SymbolEqualityComparer.Default.Equals(candidate.Parameters[0].Type, entityIdType)
                    || candidate.Parameters[0].Type.SpecialType == SpecialType.System_String)
                .ToArray();
            IMethodSymbol method = methods.FirstOrDefault(candidate =>
                    argumentType != null
                    && SymbolEqualityComparer.Default.Equals(candidate.Parameters[0].Type, argumentType))
                ?? methods.FirstOrDefault(candidate =>
                    SymbolEqualityComparer.Default.Equals(candidate.Parameters[0].Type, entityIdType))
                ?? methods.FirstOrDefault(candidate =>
                    candidate.Parameters[0].Type.SpecialType == SpecialType.System_String);
            if (method == null
                || !(method.TypeArguments[0] is INamedTypeSymbol interfaceType)
                || targetArgument == null)
            {
                proxyTarget = null;
                proxyInterface = null;
                return false;
            }

            proxyTarget = targetArgument;
            proxyInterface = interfaceType;
            return true;
        }

        private static bool IsProxyInterfaceMethod(IMethodSymbol method, INamedTypeSymbol proxyInterface)
        {
            if (method.IsExtensionMethod
                || method.ContainingType == null
                || method.ContainingType.TypeKind != TypeKind.Interface)
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(method.ContainingType, proxyInterface)
                || proxyInterface.AllInterfaces.Any(
                    interfaceType => SymbolEqualityComparer.Default.Equals(interfaceType, method.ContainingType));
        }

        private static bool IsOrchestrationContextMethod(IMethodSymbol method, INamedTypeSymbol orchestrationContextType)
        {
            INamedTypeSymbol containingType = method.ContainingType;
            return containingType != null
                && (SymbolEqualityComparer.Default.Equals(containingType, orchestrationContextType)
                    || containingType.AllInterfaces.Any(
                        interfaceType => SymbolEqualityComparer.Default.Equals(interfaceType, orchestrationContextType)));
        }

        private static IEnumerable<InvocationExpressionSyntax> GetActiveLockInvocations(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            INamedTypeSymbol orchestrationContextType,
            INamedTypeSymbol entityIdType)
        {
            for (SyntaxNode ancestor = invocation.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor is AnonymousFunctionExpressionSyntax
                    || ancestor is LocalFunctionStatementSyntax
                    || ancestor is BaseMethodDeclarationSyntax)
                {
                    yield break;
                }

                if (ancestor is UsingStatementSyntax usingStatement
                    && TryGetLockInvocation(
                        usingStatement.Expression,
                        usingStatement.Declaration,
                        semanticModel,
                        orchestrationContextType,
                        entityIdType,
                        out InvocationExpressionSyntax lockInvocation)
                    && !WasResourceDisposedBefore(
                        usingStatement.Expression,
                        usingStatement.Declaration,
                        invocation,
                        semanticModel))
                {
                    yield return lockInvocation;
                }

                if (ancestor is BlockSyntax block)
                {
                    foreach (StatementSyntax statement in block.Statements)
                    {
                        if (statement.SpanStart >= invocation.SpanStart)
                        {
                            break;
                        }

                        if (statement is LocalDeclarationStatementSyntax declaration
                            && declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)
                            && TryGetLockInvocation(
                                null,
                                declaration.Declaration,
                                semanticModel,
                                orchestrationContextType,
                                entityIdType,
                                out lockInvocation)
                            && !WasResourceDisposedBefore(
                                null,
                                declaration.Declaration,
                                invocation,
                                semanticModel))
                        {
                            yield return lockInvocation;
                        }
                    }
                }
            }
        }

        private static bool TryGetLockInvocation(
            ExpressionSyntax expression,
            VariableDeclarationSyntax declaration,
            SemanticModel semanticModel,
            INamedTypeSymbol orchestrationContextType,
            INamedTypeSymbol entityIdType,
            out InvocationExpressionSyntax lockInvocation)
        {
            return TryGetLockInvocation(
                expression,
                declaration,
                semanticModel,
                orchestrationContextType,
                entityIdType,
                new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                out lockInvocation);
        }

        private static bool TryGetLockInvocation(
            ExpressionSyntax expression,
            VariableDeclarationSyntax declaration,
            SemanticModel semanticModel,
            INamedTypeSymbol orchestrationContextType,
            INamedTypeSymbol entityIdType,
            HashSet<ISymbol> visitedSymbols,
            out InvocationExpressionSyntax lockInvocation)
        {
            IEnumerable<SyntaxNode> resources = expression != null
                ? new[] { expression }
                : declaration?.Variables
                    .Where(variable => variable.Initializer != null)
                    .Select(variable => (SyntaxNode)variable.Initializer.Value)
                    ?? Enumerable.Empty<SyntaxNode>();

            foreach (InvocationExpressionSyntax candidate in resources
                .SelectMany(resource => resource.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()))
            {
                if (GetMethodSymbols(semanticModel, candidate).Any(
                    method => method.Name == "LockAsync"
                        && method.Parameters.Length == 1
                        && method.Parameters[0].Type is IArrayTypeSymbol arrayType
                        && SymbolEqualityComparer.Default.Equals(arrayType.ElementType, entityIdType)
                        && IsOrchestrationContextMethod(method, orchestrationContextType)))
                {
                    lockInvocation = candidate;
                    return true;
                }
            }

            if (expression != null
                && semanticModel.GetSymbolInfo(UnwrapExpression(expression)).Symbol is ILocalSymbol localSymbol
                && visitedSymbols.Add(localSymbol)
                && !HasWriteBetweenDeclarationAndUse(localSymbol, expression, semanticModel))
            {
                VariableDeclaratorSyntax declarator = localSymbol.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax())
                    .OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(candidate => candidate.Initializer != null);
                if (declarator != null)
                {
                    return TryGetLockInvocation(
                        declarator.Initializer.Value,
                        null,
                        semanticModel,
                        orchestrationContextType,
                        entityIdType,
                        visitedSymbols,
                        out lockInvocation);
                }
            }

            lockInvocation = null;
            return false;
        }

        private static bool WasResourceDisposedBefore(
            ExpressionSyntax expression,
            VariableDeclarationSyntax declaration,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel)
        {
            var resourceSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            if (expression != null
                && semanticModel.GetSymbolInfo(UnwrapExpression(expression)).Symbol is ILocalSymbol expressionSymbol)
            {
                resourceSymbols.Add(expressionSymbol);
            }

            if (declaration != null)
            {
                foreach (VariableDeclaratorSyntax variable in declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable) is ILocalSymbol declarationSymbol)
                    {
                        resourceSymbols.Add(declarationSymbol);
                    }
                }
            }

            if (resourceSymbols.Count == 0)
            {
                return false;
            }

            SyntaxNode executableScope = GetExecutableScope(invocation);
            return executableScope != null
                && executableScope.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(candidate => candidate.SpanStart < invocation.SpanStart)
                    .Any(candidate =>
                    {
                        return candidate.ArgumentList.Arguments.Count == 0
                            && TryGetInvocationReceiver(
                                candidate,
                                out ExpressionSyntax receiver,
                                out SimpleNameSyntax methodName)
                            && methodName.Identifier.ValueText == nameof(IDisposable.Dispose)
                            && resourceSymbols.Contains(
                                semanticModel.GetSymbolInfo(UnwrapExpression(receiver)).Symbol);
                    });
        }

        private static IEnumerable<ExpressionSyntax> GetLockedEntityExpressions(
            InvocationExpressionSyntax lockInvocation,
            SemanticModel semanticModel,
            INamedTypeSymbol entityIdType,
            HashSet<ISymbol> visitedSymbols)
        {
            foreach (ArgumentSyntax argument in lockInvocation.ArgumentList.Arguments)
            {
                foreach (ExpressionSyntax expression in ExpandEntityExpression(
                    argument.Expression,
                    semanticModel,
                    entityIdType,
                    lockInvocation,
                    visitedSymbols))
                {
                    yield return expression;
                }
            }
        }

        private static IEnumerable<ExpressionSyntax> ExpandEntityExpression(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            INamedTypeSymbol entityIdType,
            SyntaxNode use,
            HashSet<ISymbol> visitedSymbols)
        {
            expression = UnwrapExpression(expression);

            if (expression is ArrayCreationExpressionSyntax arrayCreation
                && arrayCreation.Initializer != null)
            {
                foreach (ExpressionSyntax item in arrayCreation.Initializer.Expressions)
                {
                    yield return item;
                }

                yield break;
            }

            if (expression is ImplicitArrayCreationExpressionSyntax implicitArrayCreation)
            {
                foreach (ExpressionSyntax item in implicitArrayCreation.Initializer.Expressions)
                {
                    yield return item;
                }

                yield break;
            }

            if (semanticModel.GetSymbolInfo(expression).Symbol is ILocalSymbol localSymbol
                && localSymbol.Type is IArrayTypeSymbol arrayType
                && SymbolEqualityComparer.Default.Equals(arrayType.ElementType, entityIdType)
                && visitedSymbols.Add(localSymbol)
                && !HasWriteBetweenDeclarationAndUse(localSymbol, use, semanticModel))
            {
                VariableDeclaratorSyntax declarator = localSymbol.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax())
                    .OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(candidate => candidate.Initializer != null);
                if (declarator != null)
                {
                    foreach (ExpressionSyntax item in ExpandEntityExpression(
                        declarator.Initializer.Value,
                        semanticModel,
                        entityIdType,
                        use,
                        visitedSymbols))
                    {
                        yield return item;
                    }

                    yield break;
                }
            }

            yield return expression;
        }

        private static bool AreEquivalentEntityExpressions(
            ExpressionSyntax first,
            ExpressionSyntax second,
            SemanticModel semanticModel)
        {
            first = UnwrapExpression(first);
            second = UnwrapExpression(second);

            if (TryGetEntityIdConstructorArguments(first, semanticModel, out ExpressionSyntax firstName, out ExpressionSyntax firstKey)
                && TryGetEntityIdConstructorArguments(second, semanticModel, out ExpressionSyntax secondName, out ExpressionSyntax secondKey)
                && (AreEquivalentEntityNames(firstName, secondName, semanticModel)
                    && AreEquivalentValues(firstKey, secondKey, semanticModel)))
            {
                return true;
            }

            ISymbol firstSymbol = semanticModel.GetSymbolInfo(first).Symbol;
            ISymbol secondSymbol = semanticModel.GetSymbolInfo(second).Symbol;
            return IsSupportedValueSymbol(firstSymbol)
                && SymbolEqualityComparer.Default.Equals(firstSymbol, secondSymbol)
                && !HasWriteBetween(
                    firstSymbol,
                    first,
                    second,
                    semanticModel);
        }

        private static bool AreStringKeyProxyAndEntityIdEquivalent(
            ExpressionSyntax proxyTarget,
            INamedTypeSymbol proxyInterface,
            ExpressionSyntax lockedEntity,
            SemanticModel semanticModel,
            EntityImplementationResolver implementationResolver)
        {
            if (semanticModel.GetTypeInfo(proxyTarget).Type?.SpecialType != SpecialType.System_String)
            {
                return false;
            }

            INamedTypeSymbol implementationType = implementationResolver.FindSingleImplementation(proxyInterface);
            return implementationType != null
                && TryGetEntityIdConstructorArguments(
                    lockedEntity,
                    semanticModel,
                    out ExpressionSyntax entityName,
                    out ExpressionSyntax entityKey)
                && TryGetEntityName(entityName, semanticModel, out string lockedEntityName)
                && string.Equals(implementationType.Name, lockedEntityName, StringComparison.OrdinalIgnoreCase)
                && AreEquivalentValues(proxyTarget, entityKey, semanticModel);
        }

        private static bool TryGetEntityIdConstructorArguments(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            out ExpressionSyntax entityName,
            out ExpressionSyntax entityKey)
        {
            return TryGetEntityIdConstructorArguments(
                expression,
                semanticModel,
                new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                out entityName,
                out entityKey);
        }

        private static bool TryGetEntityIdConstructorArguments(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            HashSet<ISymbol> visitedSymbols,
            out ExpressionSyntax entityName,
            out ExpressionSyntax entityKey)
        {
            expression = UnwrapExpression(expression);
            if (semanticModel.GetOperation(expression) is IObjectCreationOperation objectCreation
                && objectCreation.Constructor?.ContainingType.ToDisplayString() == EntityIdMetadataName)
            {
                IArgumentOperation nameOperation = objectCreation.Arguments
                    .FirstOrDefault(argument => argument.Parameter?.Ordinal == 0);
                IArgumentOperation keyOperation = objectCreation.Arguments
                    .FirstOrDefault(argument => argument.Parameter?.Ordinal == 1);
                ArgumentSyntax nameArgument = nameOperation?.Syntax as ArgumentSyntax;
                ArgumentSyntax keyArgument = keyOperation?.Syntax as ArgumentSyntax;
                if (nameArgument != null && keyArgument != null)
                {
                    entityName = nameArgument.Expression;
                    entityKey = keyArgument.Expression;
                    return true;
                }
            }

            if (semanticModel.GetSymbolInfo(expression).Symbol is ILocalSymbol localSymbol
                && visitedSymbols.Add(localSymbol)
                && !HasWriteBetweenDeclarationAndUse(localSymbol, expression, semanticModel))
            {
                VariableDeclaratorSyntax declarator = localSymbol.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax())
                    .OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault(candidate => candidate.Initializer != null);
                if (declarator != null)
                {
                    return TryGetEntityIdConstructorArguments(
                        declarator.Initializer.Value,
                        semanticModel,
                        visitedSymbols,
                        out entityName,
                        out entityKey);
                }
            }

            entityName = null;
            entityKey = null;
            return false;
        }

        private static bool TryGetEntityName(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            out string entityName)
        {
            Optional<object> constant = semanticModel.GetConstantValue(expression);
            if (constant.HasValue && constant.Value is string constantName)
            {
                entityName = constantName;
                return true;
            }

            if (expression is InvocationExpressionSyntax invocation
                && invocation.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == "nameof"
                && invocation.ArgumentList.Arguments.Count == 1)
            {
                string value = invocation.ArgumentList.Arguments[0].Expression.ToString();
                int separator = value.LastIndexOf('.');
                entityName = separator >= 0 ? value.Substring(separator + 1) : value;
                return true;
            }

            entityName = null;
            return false;
        }

        private static bool AreEquivalentEntityNames(
            ExpressionSyntax first,
            ExpressionSyntax second,
            SemanticModel semanticModel)
        {
            return (TryGetEntityName(first, semanticModel, out string firstName)
                    && TryGetEntityName(second, semanticModel, out string secondName)
                    && string.Equals(firstName, secondName, StringComparison.OrdinalIgnoreCase))
                || AreEquivalentValues(first, second, semanticModel);
        }

        private static bool AreEquivalentValues(
            ExpressionSyntax first,
            ExpressionSyntax second,
            SemanticModel semanticModel)
        {
            first = UnwrapExpression(first);
            second = UnwrapExpression(second);

            Optional<object> firstConstant = semanticModel.GetConstantValue(first);
            Optional<object> secondConstant = semanticModel.GetConstantValue(second);
            if (firstConstant.HasValue && secondConstant.HasValue)
            {
                return object.Equals(firstConstant.Value, secondConstant.Value);
            }

            ISymbol firstSymbol = semanticModel.GetSymbolInfo(first).Symbol;
            ISymbol secondSymbol = semanticModel.GetSymbolInfo(second).Symbol;
            return IsSupportedValueSymbol(firstSymbol)
                && SymbolEqualityComparer.Default.Equals(firstSymbol, secondSymbol)
                && !HasWriteBetween(
                    firstSymbol,
                    first,
                    second,
                    semanticModel);
        }

        private static bool IsSupportedValueSymbol(ISymbol symbol)
        {
            return symbol != null
                && (symbol.Kind == SymbolKind.Local
                    || symbol.Kind == SymbolKind.Parameter);
        }

        private static bool HasWriteBetweenDeclarationAndUse(
            ILocalSymbol localSymbol,
            SyntaxNode use,
            SemanticModel semanticModel)
        {
            SyntaxNode declaration = localSymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .FirstOrDefault();

            return declaration == null
                || HasWriteBetween(localSymbol, declaration, use, semanticModel);
        }

        private static bool HasWriteBetween(
            ISymbol symbol,
            SyntaxNode first,
            SyntaxNode second,
            SemanticModel semanticModel)
        {
            SyntaxNode firstExecutableScope = GetExecutableScope(first);
            SyntaxNode secondExecutableScope = GetExecutableScope(second);
            if (firstExecutableScope == null
                || secondExecutableScope == null
                || !IsSameExecutableScope(firstExecutableScope, secondExecutableScope))
            {
                return true;
            }

            int start = first.SpanStart < second.SpanStart ? first.SpanStart : second.SpanStart;
            int end = first.SpanStart < second.SpanStart ? second.SpanStart : first.SpanStart;

            return firstExecutableScope.DescendantNodes()
                .Any(candidate =>
                {
                    if (!IsPotentialWrite(candidate, symbol, semanticModel))
                    {
                        return false;
                    }

                    SyntaxNode candidateExecutableScope = GetExecutableScope(candidate);
                    return !IsSameExecutableScope(firstExecutableScope, candidateExecutableScope)
                        || (candidate.SpanStart > start && candidate.SpanStart < end);
                });
        }

        private static SyntaxNode GetExecutableScope(SyntaxNode node)
        {
            return node.AncestorsAndSelf().FirstOrDefault(
                ancestor => ancestor is BaseMethodDeclarationSyntax
                    || ancestor is LocalFunctionStatementSyntax
                    || ancestor is AnonymousFunctionExpressionSyntax);
        }

        private static bool IsSameExecutableScope(SyntaxNode first, SyntaxNode second)
        {
            return first != null
                && second != null
                && first.SyntaxTree == second.SyntaxTree
                && first.Span == second.Span;
        }

        private static bool IsPotentialWrite(
            SyntaxNode node,
            ISymbol symbol,
            SemanticModel semanticModel)
        {
            bool isMutableArray = symbol is ILocalSymbol localSymbol
                && localSymbol.Type is IArrayTypeSymbol;

            switch (node)
            {
                case AssignmentExpressionSyntax assignment:
                    return ContainsSymbol(assignment.Left, symbol, semanticModel)
                        || (isMutableArray && ContainsSymbol(assignment.Right, symbol, semanticModel));

                case PrefixUnaryExpressionSyntax prefix
                    when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                        || prefix.IsKind(SyntaxKind.PreDecrementExpression):
                    return ContainsSymbol(prefix.Operand, symbol, semanticModel);

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.PostIncrementExpression)
                        || postfix.IsKind(SyntaxKind.PostDecrementExpression):
                    return ContainsSymbol(postfix.Operand, symbol, semanticModel);

                case ArgumentSyntax argument
                    when argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                        || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword):
                    return ContainsSymbol(argument.Expression, symbol, semanticModel);

                case RefExpressionSyntax refExpression:
                    return ContainsSymbol(refExpression.Expression, symbol, semanticModel);

                case VariableDeclaratorSyntax declarator
                    when isMutableArray && declarator.Initializer != null:
                    return ContainsSymbol(declarator.Initializer.Value, symbol, semanticModel);

                case InvocationExpressionSyntax invocation when isMutableArray:
                    return ContainsSymbol(invocation, symbol, semanticModel);

                default:
                    return false;
            }
        }

        private static bool ContainsSymbol(
            ExpressionSyntax expression,
            ISymbol symbol,
            SemanticModel semanticModel)
        {
            return expression.DescendantNodesAndSelf()
                .OfType<ExpressionSyntax>()
                .Any(candidate => SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(candidate).Symbol,
                    symbol));
        }

        private static IEnumerable<IMethodSymbol> GetMethodSymbols(
            SemanticModel semanticModel,
            InvocationExpressionSyntax invocation)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                yield return method;
            }

            foreach (IMethodSymbol candidate in symbolInfo.CandidateSymbols.OfType<IMethodSymbol>())
            {
                yield return candidate;
            }
        }

        private sealed class EntityImplementationResolver
        {
            private readonly ConcurrentDictionary<IAssemblySymbol, Lazy<IReadOnlyDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>>> implementationMaps =
                new ConcurrentDictionary<IAssemblySymbol, Lazy<IReadOnlyDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>>>(
                    SymbolEqualityComparer.Default);

            public INamedTypeSymbol FindSingleImplementation(INamedTypeSymbol interfaceType)
            {
                IAssemblySymbol assembly = interfaceType.ContainingAssembly;
                if (assembly == null)
                {
                    return null;
                }

                IReadOnlyDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> implementationMap =
                    this.implementationMaps.GetOrAdd(
                        assembly,
                        currentAssembly => new Lazy<IReadOnlyDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>>(
                            () => CreateImplementationMap(currentAssembly),
                            LazyThreadSafetyMode.ExecutionAndPublication))
                    .Value;

                return implementationMap.TryGetValue(interfaceType, out ImmutableArray<INamedTypeSymbol> implementations)
                    && implementations.Length == 1
                    ? implementations[0]
                    : null;
            }

            private static IReadOnlyDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> CreateImplementationMap(
                IAssemblySymbol assembly)
            {
                var implementations = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(
                    SymbolEqualityComparer.Default);

                foreach (INamedTypeSymbol type in GetNamedTypes(assembly.GlobalNamespace)
                    .Where(candidate => candidate.TypeKind == TypeKind.Class && !candidate.IsAbstract))
                {
                    foreach (INamedTypeSymbol interfaceType in type.AllInterfaces)
                    {
                        if (!implementations.TryGetValue(interfaceType, out List<INamedTypeSymbol> implementingTypes))
                        {
                            implementingTypes = new List<INamedTypeSymbol>();
                            implementations.Add(interfaceType, implementingTypes);
                        }

                        if (implementingTypes.Count < 2)
                        {
                            implementingTypes.Add(type);
                        }
                    }
                }

                var result = new Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>(
                    SymbolEqualityComparer.Default);
                foreach (KeyValuePair<INamedTypeSymbol, List<INamedTypeSymbol>> pair in implementations)
                {
                    result.Add(pair.Key, pair.Value.ToImmutableArray());
                }

                return result;
            }

            private static IEnumerable<INamedTypeSymbol> GetNamedTypes(INamespaceSymbol namespaceSymbol)
            {
                foreach (INamedTypeSymbol type in namespaceSymbol.GetTypeMembers())
                {
                    foreach (INamedTypeSymbol nestedType in GetNamedTypes(type))
                    {
                        yield return nestedType;
                    }
                }

                foreach (INamespaceSymbol childNamespace in namespaceSymbol.GetNamespaceMembers())
                {
                    foreach (INamedTypeSymbol type in GetNamedTypes(childNamespace))
                    {
                        yield return type;
                    }
                }
            }

            private static IEnumerable<INamedTypeSymbol> GetNamedTypes(INamedTypeSymbol type)
            {
                yield return type;

                foreach (INamedTypeSymbol nestedType in type.GetTypeMembers())
                {
                    foreach (INamedTypeSymbol descendant in GetNamedTypes(nestedType))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        private static ExpressionSyntax UnwrapExpression(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        expression = parenthesized.Expression;
                        continue;
                    case CastExpressionSyntax cast:
                        expression = cast.Expression;
                        continue;
                    case CheckedExpressionSyntax checkedExpression:
                        expression = checkedExpression.Expression;
                        continue;
                    case PostfixUnaryExpressionSyntax postfix
                        when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                        expression = postfix.Operand;
                        continue;
                    default:
                        return expression;
                }
            }
        }
    }
}
