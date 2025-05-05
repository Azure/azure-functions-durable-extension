// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public static class SyntaxNodeUtils
    {
        public static DurableVersion? version;

        public static DurableVersion GetDurableVersion(SemanticModel semanticModel)
        {
            if (version != null)
            {
                return (DurableVersion)version;
            }

            var versionTwoInterface = semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Azure.WebJobs.Extensions.DurableTask.IDurableOrchestrationContext");
            version = versionTwoInterface != null ? DurableVersion.V2 : DurableVersion.V1;
            return (DurableVersion)version;
        }

        public static bool TryGetSemanticModelForSyntaxTree(SemanticModel model, SyntaxNode node, out SemanticModel newModel)
        {
            if (model?.SyntaxTree == null || node?.SyntaxTree == null)
            {
                newModel = null;
                return false;
            }

            try
            {
                var compilation = model.Compilation;
                if (!compilation.ContainsSyntaxTree(node.SyntaxTree))
                {
                    var newCompilation = compilation.AddSyntaxTrees(node.SyntaxTree);
                    newModel = newCompilation.GetSemanticModel(node.SyntaxTree);
                }
                else
                {
                    newModel = model.SyntaxTree == node.SyntaxTree
                    ? model
                    : model.Compilation.GetSemanticModel(node.SyntaxTree);
                }
            }
            catch( ArgumentException e) when (e.Message.Contains("Inconsistent language versions"))
            {
                // model.Compilation.AddSyntaxTrees(node.SyntaxTree) can sometimes throw an ArgumentException with this message if the SyntaxTree
                // that is being added has an inconsistent language version with the compilation.
                newModel = null;
                return false;
            }

            return newModel != null;
        }

        public static bool TryGetITypeSymbol(SemanticModel semanticModel, SyntaxNode node, out ITypeSymbol typeSymbol)
        {
            if (node != null && TryGetSemanticModelForSyntaxTree(semanticModel, node, out SemanticModel newModel))
            {
                typeSymbol = newModel.GetTypeInfo(node).Type;
                return typeSymbol != null;
            }

            typeSymbol = null;
            return false;
        }

        public static bool TryGetISymbol(SemanticModel semanticModel, SyntaxNode node, out ISymbol symbol)
        {
            if (node != null && TryGetSemanticModelForSyntaxTree(semanticModel, node, out SemanticModel newModel))
            {
                symbol = newModel.GetSymbolInfo(node).Symbol;
                return symbol != null;
            }

            symbol = null;
            return false;
        }

        public static bool TryGetDeclaredSyntaxNode(SemanticModel semanticModel, SyntaxNode node, out SyntaxNode declaredNode)
        {
            if (TryGetISymbol(semanticModel, node, out ISymbol symbol))
            {
                var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxReference != null)
                {
                    var declaration = syntaxReference.GetSyntax();
                    if (declaration != null)
                    {
                        declaredNode = declaration;
                        return true;
                    }

                }

            }

            declaredNode = null;
            return false;
        }

        public static bool TryGetClosestString(string name, IEnumerable<string> availableNames, out string closestString)
        {
            closestString = availableNames.OrderBy(x => x.LevenshteinDistance(name)).FirstOrDefault();
            return closestString != null;
        }

        public static bool IsInsideOrchestratorFunction(SemanticModel semanticModel, SyntaxNode node)
        {
            return IsInsideOrchestrationTrigger(node) && IsInsideFunction(semanticModel, node);
        }

        public static bool IsInsideOrchestrationTrigger(SyntaxNode node)
        {
            if (TryGetMethodDeclaration(node, out MethodDeclarationSyntax methodDeclaration))
            {
                var parameters = methodDeclaration.ParameterList.ChildNodes();

                foreach (SyntaxNode parameter in parameters)
                {
                    var attributeLists = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                    foreach (SyntaxNode attributeList in attributeLists)
                    {
                        //An AttributeList will always have a child node Attribute
                        if (attributeList.ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool IsInsideFunction(SemanticModel semanticModel, SyntaxNode node)
        {
            return TryGetFunctionName(semanticModel, node, out _);
        }

        internal static bool TryGetMethodReturnTypeNode(SyntaxNode node, out SyntaxNode returnTypeNode)
        {
            if (TryGetMethodDeclaration(node, out MethodDeclarationSyntax methodDeclaration))
            {
                returnTypeNode = methodDeclaration.ReturnType;
                return returnTypeNode != null;
            }

            returnTypeNode = null;
            return false;
        }

        private static bool TryGetChildTypeNode(SyntaxNode node, out SyntaxNode childTypeNode)
        {
            childTypeNode = node.ChildNodes().FirstOrDefault(
                x => x.IsKind(SyntaxKind.IdentifierName)
                || x.IsKind(SyntaxKind.PredefinedType)
                || x.IsKind(SyntaxKind.GenericName)
                || x.IsKind(SyntaxKind.ArrayType)
                || x.IsKind(SyntaxKind.TupleType)
                || x.IsKind(SyntaxKind.NullableType));

            return childTypeNode != null;
        }

        private static bool TryGetAttribute(SyntaxNode node, string attributeName, out SyntaxNode attribute)
        {
            if (TryGetMethodDeclaration(node, out MethodDeclarationSyntax methodDeclaration))
            {
                var attributeLists = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                foreach (var attributeList in attributeLists)
                {
                    //An AttributeList will always have a child node Attribute
                    attribute = attributeList.ChildNodes().First();

                    if (attribute.ChildNodes().Any() && attribute.ChildNodes().First().ToString().Equals(attributeName))
                    {
                        return true;
                    }
                }
            }

            attribute = null;
            return false;

        }

        internal static bool TryGetMethodDeclaration(SyntaxNode node, out MethodDeclarationSyntax methodDeclaration) => TryGetContainingSyntaxNode(node, SyntaxKind.MethodDeclaration, out methodDeclaration);

        internal static bool TryGetInvocationExpression(SyntaxNode node, out InvocationExpressionSyntax invocationExpression) => TryGetContainingSyntaxNode(node, SyntaxKind.InvocationExpression, out invocationExpression);

        internal static bool TryGetClassDeclaration(SyntaxNode node, out ClassDeclarationSyntax classDeclaration) => TryGetContainingSyntaxNode(node, SyntaxKind.ClassDeclaration, out classDeclaration);

        private static bool TryGetContainingSyntaxNode<T>(SyntaxNode node, SyntaxKind kind, out T kindNode)
        {
            kindNode = default(T);
            var currNode = node.IsKind(kind) ? node : node.Parent;
            while (!currNode.IsKind(kind))
            {
                if (currNode.IsKind(SyntaxKind.CompilationUnit))
                {
                    break;
                }
                currNode = currNode.Parent;
            }

            if (currNode is T)
            {
                kindNode = (T)(object)currNode;
                return true;
            }

            return false;
        }

        internal static bool TryGetConstructor(SyntaxNode node, out ConstructorDeclarationSyntax constructor)
        {
            if (TryGetClassDeclaration(node, out ClassDeclarationSyntax classDeclaration))
            {
                var constructorNode = classDeclaration.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.ConstructorDeclaration));
                if (constructorNode != null)
                {
                    constructor = (ConstructorDeclarationSyntax)constructorNode;
                    return true;
                }
            }

            constructor = null;
            return false;
        }

        internal static bool TryGetClassName(SyntaxNode node, out string className)
        {
            if (TryGetClassDeclaration(node, out ClassDeclarationSyntax classDeclaration))
            {
                className = classDeclaration.Identifier.ToString();
                return true;
            }

            className = null;
            return false;
        }

        internal static bool IsInStaticClass(SyntaxNode node)
        {
            if (TryGetClassDeclaration(node, out ClassDeclarationSyntax classDeclaration))
            {
                if (HasStaticChildNode(classDeclaration))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsInStaticMethod(SyntaxNode node)
        {
            if (TryGetMethodDeclaration(node, out MethodDeclarationSyntax methodDeclaration))
            {
                if (HasStaticChildNode(methodDeclaration))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasStaticChildNode(SyntaxNode node)
        {
            var staticKeyword = node.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.StaticKeyword));
            if (staticKeyword != null && !staticKeyword.IsKind(SyntaxKind.None))
            {
                return true;
            }

            return false;
        }

        public static bool TryGetFunctionName(SemanticModel semanticModel, SyntaxNode node, out string functionName)
        {
            return TryGetFunctionNameAndNode(semanticModel, node, out _, out functionName);
        }

        internal static bool TryGetFunctionNameAndNode(SemanticModel semanticModel, SyntaxNode node, out SyntaxNode attributeArgument, out string functionName)
        {
            if (TryGetFunctionAttribute(node, out SyntaxNode functionAttribute))
            {
                if (TryGetFunctionNameAttributeArgument(functionAttribute, out attributeArgument))
                {
                    if (TryParseFunctionName(semanticModel, attributeArgument.ChildNodes().FirstOrDefault(), out functionName))
                    {
                        return true;
                    }
                }
            }

            attributeArgument = null;
            functionName = null;
            return false;
        }

        public static bool TryParseFunctionName(SemanticModel semanticModel, SyntaxNode node, out string functionName)
        {
            if (TryGetFunctionNameInStringLiteral(node, out functionName))
            {
                return true;
            }

            if (TryGetFunctionNameInNameOfOperator(node, out functionName))
            {
                return true;
            }

            if (TryGetFunctionNameInConstant(semanticModel, node, out functionName))
            { 
                return true;
            }
            
            functionName = null;
            return false;
        }

        public static bool TryGetFunctionNameInConstant(SemanticModel semanticModel, SyntaxNode node, out string functionName)
        {
            if (node != null && (node.IsKind(SyntaxKind.IdentifierName) || node.IsKind(SyntaxKind.SimpleMemberAccessExpression)))
            {
                if (TryGetSemanticModelForSyntaxTree(semanticModel, node, out SemanticModel newModel))
                {
                    var constValue = newModel.GetConstantValue(node);
                    if (constValue.HasValue && constValue.Value is string constString)
                    {
                        functionName = constString;
                        return true;
                    }
                }
            }

            functionName = null;
            return false;
        }

        private static bool TryGetFunctionNameInNameOfOperator(SyntaxNode node, out string functionName)
        {
            if (node != null && node.IsKind(SyntaxKind.InvocationExpression))
            {
                var argumentList = node.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.ArgumentList));
                if (argumentList != null)
                {
                    var argument = argumentList.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.Argument));
                    if (argument != null)
                    {
                        var identifierName = argument.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierName));
                        if (identifierName != null)
                        {
                            functionName = identifierName.ToString();
                            var lastIndex = functionName.LastIndexOf('.');
                            if (lastIndex != -1)
                            {
                                functionName = functionName.Substring(lastIndex);
                            }

                            return true;
                        }
                    }
                }
            }

            functionName = null;
            return false;
        }

        private static bool TryGetFunctionNameInStringLiteral(SyntaxNode node, out string functionName)
        {
            if (node != null && node.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var stringLiteralToken = node.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.StringLiteralToken));
                if (stringLiteralToken != null)
                {
                    functionName = stringLiteralToken.ValueText;
                    return true;
                }
            }

            functionName = null;
            return false;
        }

        private static bool TryGetFunctionNameAttributeArgument(SyntaxNode functionAttribute, out SyntaxNode attributeArgument)
        {
            var attributeArgumentListSyntax = ((AttributeSyntax)functionAttribute).ArgumentList;
            if (attributeArgumentListSyntax != null)
            {
                var attributeArgumentSyntax = attributeArgumentListSyntax.Arguments.FirstOrDefault();
                if (attributeArgumentSyntax != null)
                {
                    attributeArgument = attributeArgumentSyntax;
                    return true;
                }
            }

            attributeArgument = null;
            return false;
        }

        private static bool TryGetFunctionAttribute(SyntaxNode attributeExpression, out SyntaxNode functionAttribute) => TryGetAttribute(attributeExpression, "FunctionName", out functionAttribute);

        internal static bool TryGetParameterNodeNextToAttribute(AttributeSyntax attributeExpression, out SyntaxNode inputType)
        {
            var parameter = attributeExpression.Parent.Parent;
            return TryGetChildTypeNode(parameter, out inputType);
        }

        internal static bool TryGetTypeArgumentIdentifier(MemberAccessExpressionSyntax expression, out SyntaxNode identifierNode)
        {
            var genericName = expression.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.GenericName));
            if (genericName != null)
            {
                return TryGetTypeArgumentIdentifier((GenericNameSyntax)genericName, out identifierNode);
            }

            identifierNode = null;
            return false;
        }

        internal static bool TryGetTypeArgumentIdentifier(GenericNameSyntax node, out SyntaxNode identifierNode)
        {
            //GenericName will always have a TypeArgumentList
            identifierNode = node.TypeArgumentList.ChildNodes().First();
            return (identifierNode != null && !identifierNode.IsKind(SyntaxKind.OmittedTypeArgument));
        }

        internal static bool IsActivityTriggerAttribute(AttributeSyntax attribute) => IsSpecifiedAttribute(attribute, "ActivityTrigger");

        internal static bool IsEntityTriggerAttribute(AttributeSyntax attribute) => IsSpecifiedAttribute(attribute, "EntityTrigger");

        private static bool IsSpecifiedAttribute(AttributeSyntax attribute, string attributeName)
        {
            if (attribute != null && attribute.ChildNodes().First().ToString() == attributeName)
            {
                return true;
            }

            return false;
        }

        public static bool IsDurableActivityContext(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            return (type.ToString().Equals("Microsoft.Azure.WebJobs.Extensions.DurableTask.IDurableActivityContext")
                || type.ToString().Equals("Microsoft.Azure.WebJobs.DurableActivityContext")
                || type.ToString().Equals("Microsoft.Azure.WebJobs.DurableActivityContextBase"));
        }

        public static bool IsMatchingDerivedOrCompatibleType(ITypeSymbol subclassOrMatching, ITypeSymbol superOrMatching)
        {
            if (subclassOrMatching == null || superOrMatching == null)
            {
                return false;
            }

            return (subclassOrMatching.Equals(superOrMatching, SymbolEqualityComparer.Default)
                || AreMatchingValueTuples(subclassOrMatching, superOrMatching)
                || AreMatchingGenericTypes(subclassOrMatching, superOrMatching)
                || IsSubclassOrImplementation(subclassOrMatching, superOrMatching)
                || AreCompatibleIEnumerableTypes(subclassOrMatching, superOrMatching));
        }

        private static bool AreMatchingValueTuples(ITypeSymbol subclassOrMatching, ITypeSymbol superOrMatching)
        {
            if (subclassOrMatching == null || superOrMatching == null)
            {
                return false;
            }

            if (subclassOrMatching.IsTupleType && superOrMatching.IsTupleType)
            {
                return HaveMatchingOrCompatibeTypeArguments(subclassOrMatching, superOrMatching);
            }

            return false;
        }

        private static bool HaveMatchingOrCompatibeTypeArguments(ITypeSymbol subclassOrMatching, ITypeSymbol superOrMatching)
        {
            if (subclassOrMatching == null || superOrMatching == null
                || !(subclassOrMatching is INamedTypeSymbol subclassNamedType
                    && superOrMatching is INamedTypeSymbol superNamedType))
            {
                return false;
            }

            var subclassTypeArguments = subclassNamedType.TypeArguments;
            var superTypeArguments = superNamedType.TypeArguments;

            if (NotNullAndMatchingLength(subclassTypeArguments, superTypeArguments))
            {
                for (int i = 0; i < subclassTypeArguments.Length; i++)
                {
                    if (!IsMatchingDerivedOrCompatibleType(subclassTypeArguments[i], superTypeArguments[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        private static bool NotNullAndMatchingLength(ImmutableArray<ITypeSymbol> immutableArrayOne, ImmutableArray<ITypeSymbol> immutableArrayTwo)
        {
            if (immutableArrayOne != null && immutableArrayOne != null)
            {
                return immutableArrayOne.Length == immutableArrayTwo.Length;
            }

            return false;
        }

        private static bool AreMatchingGenericTypes(ITypeSymbol subclassOrMatching, ITypeSymbol superOrMatching)
        {
            if (subclassOrMatching == null || superOrMatching == null)
            {
                return false;
            }

            if (subclassOrMatching.Name == superOrMatching.Name)
            {
                return HaveMatchingOrCompatibeTypeArguments(subclassOrMatching, superOrMatching);
            }

            return false;
        }

        private static bool IsSubclassOrImplementation(ITypeSymbol subclassOrImplementation, ITypeSymbol superOrInterface)
        {
            if (subclassOrImplementation == null || superOrInterface == null)
            {
                return false;
            }

            var superOrInterfaceName = superOrInterface is IArrayTypeSymbol arrayType ? arrayType.ElementType.Name : superOrInterface.Name;

            if (TypeSymbolImplementsOrExtendsType(subclassOrImplementation, superOrInterfaceName))
            {
                return HaveMatchingOrCompatibeTypeArguments(subclassOrImplementation, superOrInterface);
            }

            return false;
        }

        private static bool AreCompatibleIEnumerableTypes(ITypeSymbol typeOne, ITypeSymbol typeTwo)
        {
            if (typeOne == null || typeTwo == null)
            {
                return false;
            }

            if (CollectionTypesMatch(typeOne, typeTwo))
            {
                return TypeSymbolImplementsOrExtendsType(typeOne, "IEnumerable")
                    && TypeSymbolImplementsOrExtendsType(typeTwo, "IEnumerable");
            }

            return false;
        }

        private static bool CollectionTypesMatch(ITypeSymbol typeOne, ITypeSymbol typeTwo)
        {
            if (typeOne == null || typeTwo == null)
            {
                return false;
            }

            return (TryGetCollectionType(typeOne, out ITypeSymbol invocationCollectionType)
                && TryGetCollectionType(typeTwo, out ITypeSymbol functionCollectionType)
                && IsMatchingDerivedOrCompatibleType(invocationCollectionType, functionCollectionType));
        }

        private static bool TryGetCollectionType(ITypeSymbol type, out ITypeSymbol collectionType)
        {
            if (type != null)
            {
                if (type.Kind.Equals(SymbolKind.ArrayType))
                {
                    collectionType = ((IArrayTypeSymbol)type).ElementType;
                    return true;
                }

                if (type.Kind.Equals(SymbolKind.NamedType))
                {
                    collectionType = ((INamedTypeSymbol)type).TypeArguments.FirstOrDefault();
                    return collectionType != null;
                }
            }

            collectionType = null;
            return false;
        }

        public static bool TypeSymbolImplementsOrExtendsType(ITypeSymbol node, string interfaceOrBase)
        {
            if (node == null || string.IsNullOrEmpty(interfaceOrBase))
            {
                return false;
            }

            return TypeSymbolImplementsInterface(node, interfaceOrBase)
                || TypeSymbolIsSubclass(node, interfaceOrBase);

        }

        private static bool TypeSymbolImplementsInterface(ITypeSymbol node, string interfaceName)
        {
            if (node == null || string.IsNullOrEmpty(interfaceName))
            {
                return false;
            }

            return node.AllInterfaces.Any(i => i.Name.Equals(interfaceName));
        }

        private static bool TypeSymbolIsSubclass(ITypeSymbol node, string baseClass)
        {
            if (node == null || string.IsNullOrEmpty(baseClass))
            {
                return false;
            }

            var curr = node.BaseType;
            while (curr != null)
            {
                if (curr.Name.Equals(baseClass))
                {
                    return true;
                }

                curr = curr.BaseType;
            }

            return false;

        }
    }
}
