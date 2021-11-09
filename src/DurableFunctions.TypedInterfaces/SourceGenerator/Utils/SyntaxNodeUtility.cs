// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Utils
{
    public static class SyntaxNodeUtility
    {
        public static bool TryGetFunctionName(SemanticModel model, MethodDeclarationSyntax method, out string functionName)
        {
            if (TryGetAttributeByName(method, "FunctionName", out AttributeSyntax functionNameAttribute))
            {
                if (functionNameAttribute?.ArgumentList?.Arguments.Count == 1)
                {
                    var expression = functionNameAttribute.ArgumentList.Arguments.First().Expression;
                    functionName = model.GetConstantValue(expression).Value.ToString();
                    return true;
                }
            }

            functionName = null;
            return false;
        }

        public static bool TryGetReturnType(MethodDeclarationSyntax method, out TypeSyntax returnTypeSyntax)
        {
            returnTypeSyntax = method.ReturnType;
            return true;
        }

        public static bool TryGetFunctionKind(MethodDeclarationSyntax method, out DurableFunctionKind kind)
        {
            var parameters = method.ParameterList.Parameters;

            foreach (var parameterSyntax in parameters)
            {
                var parameterAttributes = parameterSyntax.AttributeLists.SelectMany(a => a.Attributes);

                foreach (var attribute in parameterAttributes)
                {
                    if (attribute.ToString().Equals("OrchestrationTrigger"))
                    {
                        kind = DurableFunctionKind.Orchestration;
                        return true;
                    }

                    if (attribute.ToString().Equals("ActivityTrigger"))
                    {
                        kind = DurableFunctionKind.Activity;
                        return true;
                    }
                }
            }

            kind = DurableFunctionKind.Unknonwn;
            return false;
        }

        public static bool TryGetRequiredNamespaces(SemanticModel model, List<TypeSyntax> types, out HashSet<string> requiredNamespaces)
        {
            requiredNamespaces = new HashSet<string>();

            var remaining = new Queue<TypeSyntax>(types);

            while (remaining.Any())
            {
                var toProcess = remaining.Dequeue();

                if (toProcess is PredefinedTypeSyntax)
                    continue;

                var typeInfo = model.GetTypeInfo(toProcess);

                if (!(toProcess is PredefinedTypeSyntax) && typeInfo.Type.ContainingNamespace.IsGlobalNamespace)
                {
                    requiredNamespaces = null;
                    return false;
                }

                requiredNamespaces.Add(typeInfo.Type.ContainingNamespace.ToDisplayString());

                if (toProcess is GenericNameSyntax genericType)
                {
                    foreach (var typeArgument in genericType.TypeArgumentList.Arguments)
                    {
                        remaining.Enqueue(typeArgument);
                    }
                }
            }

            return true;
        }

        public static bool TryGetParameters(SemanticModel model, MethodDeclarationSyntax method, out List<TypedParameter> parameters)
        {
            parameters = null;
            var invocation = FindGetInputInvocation(method);

            List<string> parameterNames;
            List<TypeSyntax> parameterTypes;

            if (IsAssignmentExpression(invocation))
            {
                // var (a, b) = context.GetInput<(X, Y)>();
                var assignment = GetAssignment(invocation);

                if (!TryGetParameterNames(assignment, out parameterNames))
                    return false;

                if (!TryGetParameterTypes(assignment, out parameterTypes))
                    return false;
            }
            else if (IsLocalDeclarationExpression(invocation))
            {
                // var a = context.GetInput<X>();
                var localDeclaration = GetLocalDeclaration(invocation);

                if (!TryGetParameterNames(localDeclaration, out parameterNames))
                    return false;

                if (!TryGetParameterTypes(localDeclaration, out parameterTypes))
                    return false;
            }
            else
            {
                // It is possible for the function to require no input parameters
                parameterNames = new List<string>();
                parameterTypes = new List<TypeSyntax>();
            }

            if (parameterNames?.Count != parameterTypes?.Count)
            {
                parameters = null;
                return false;
            }

            parameters = parameterTypes.Select((t, i) => new TypedParameter(t, parameterNames[i])).ToList();
            return true;
        }

        public static bool TryGetQualifiedTypeName(SemanticModel model, MethodDeclarationSyntax method, out string fullTypeName)
        {
            var symbol = model.GetEnclosingSymbol(method.SpanStart);
            fullTypeName = $@"{symbol.ToDisplayString()}.{method.Identifier}";
            return true;
        }

        private static bool TryGetAttributeByName(MethodDeclarationSyntax method, string attributeName, out AttributeSyntax attribute)
        {
            attribute = method.AttributeLists.SelectMany(a => a.Attributes).FirstOrDefault(a => a.Name.NormalizeWhitespace().ToFullString().Equals(attributeName));
            return (attribute != null);
        }

        private static InvocationExpressionSyntax FindGetInputInvocation(MethodDeclarationSyntax method)
        {
            var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

            return invocations.FirstOrDefault(inv => (inv.Expression is MemberAccessExpressionSyntax memberAccess) &&
                                            (memberAccess.Name is GenericNameSyntax name) &&
                                            (name.Identifier.Text == "GetInput"));
        }

        private static bool TryGetParameterNames(AssignmentExpressionSyntax assignment, out List<string> parameterNames)
        {
            if (assignment.Left is DeclarationExpressionSyntax declaration)
            {
                if (declaration.Designation is ParenthesizedVariableDesignationSyntax designation)
                {
                    parameterNames = designation.Variables.Select(v => v.ToString()).ToList();
                    return true;
                }
            }

            parameterNames = null;
            return false;
        }

        private static bool TryGetParameterNames(LocalDeclarationStatementSyntax local, out List<string> parameterNames)
        {
            if (local.Declaration.Variables.Count == 1)
            {
                parameterNames = new List<string>() { local.Declaration.Variables.First().Identifier.ToFullString().Trim() };
                return true;
            }

            parameterNames = null;
            return false;
        }

        private static bool TryGetParameterTypes(AssignmentExpressionSyntax assignment, out List<TypeSyntax> parameterTypes)
        {
            if (assignment.Right is InvocationExpressionSyntax invocation)
            {
                return TryGetParameterTypes(invocation, out parameterTypes);
            }

            parameterTypes = null;
            return false;
        }

        private static bool TryGetParameterTypes(LocalDeclarationStatementSyntax local, out List<TypeSyntax> parameterTypes)
        {
            var variables = local.Declaration.Variables;

            if (variables.Count == 1)
            {
                if (variables.First().Initializer.Value is InvocationExpressionSyntax invocation)
                {
                    TryGetParameterTypes(invocation, out parameterTypes);
                    return true;
                }
            }

            parameterTypes = null;
            return false;
        }

        private static bool IsAssignmentExpression(InvocationExpressionSyntax invocation)
        {
            return invocation?.Parent is AssignmentExpressionSyntax;
        }

        private static AssignmentExpressionSyntax GetAssignment(InvocationExpressionSyntax invocation)
        {
            return invocation.Parent as AssignmentExpressionSyntax;
        }

        private static bool IsLocalDeclarationExpression(InvocationExpressionSyntax invocation)
        {
            if (!(invocation?.Parent is EqualsValueClauseSyntax equals))
                return false;

            if (!(equals.Parent is VariableDeclaratorSyntax variableDeclarator))
                return false;

            if (!(variableDeclarator.Parent is VariableDeclarationSyntax variableDeclaration))
                return false;

            return variableDeclaration.Parent is LocalDeclarationStatementSyntax;
        }

        private static LocalDeclarationStatementSyntax GetLocalDeclaration(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Parent is EqualsValueClauseSyntax equals))
                return null;

            if (!(equals.Parent is VariableDeclaratorSyntax variableDeclarator))
                return null;

            if (!(variableDeclarator.Parent is VariableDeclarationSyntax variableDeclaration))
                return null;

            return variableDeclaration.Parent as LocalDeclarationStatementSyntax;
        }

        private static bool TryGetParameterTypes(InvocationExpressionSyntax invocation, out List<TypeSyntax> parameterTypes)
        {
            parameterTypes = new List<TypeSyntax>();

            if (!(invocation.Expression is MemberAccessExpressionSyntax expression))
                return false;

            if (!(expression.Name is GenericNameSyntax name))
                return false;

            var argumentList = name.TypeArgumentList;

            var arguments = argumentList.Arguments;

            if (arguments.Count != 1)
                return false;

            var argument = arguments.First();

            if (argument is IdentifierNameSyntax identifier)      // context.GetInput<MyType>()
                parameterTypes.Add(identifier);
            else if (argument is PredefinedTypeSyntax predefined) // context.GetInput<int>()
                parameterTypes.Add(predefined);
            else if (argument is GenericNameSyntax generic)       // context.GetInput<List<int>>()
                parameterTypes.Add(generic);
            else if (argument is TupleTypeSyntax tupleArgument)   // context.GetInput<(X,Y,Z)>()
                parameterTypes.AddRange(tupleArgument.Elements.Select(e => e.Type));
            else
                return false;

            return true;
        }
    }
}
