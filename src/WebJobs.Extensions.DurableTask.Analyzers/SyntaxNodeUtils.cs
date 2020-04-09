// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
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

        public static SemanticModel GetSyntaxTreeSemanticModel(SemanticModel model, SyntaxNode node)
        {
            return model.SyntaxTree == node.SyntaxTree
                ? model
                : model.Compilation.GetSemanticModel(node.SyntaxTree);
        }

        public static bool TryGetClosestString(string name, IEnumerable<string> availableNames, out string closestString)
        {
            closestString = availableNames.OrderBy(x => x.LevenshteinDistance(name)).FirstOrDefault();
            return closestString != null;
        }

        internal static bool IsInsideOrchestrator(SyntaxNode node)
        {
            if (TryGetMethodDeclaration(node, out SyntaxNode methodDeclaration))
            {
                var parameterList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ParameterList)).First();

                foreach (SyntaxNode parameter in parameterList.ChildNodes())
                {
                    var attributeLists = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                    foreach (SyntaxNode attribute in attributeLists)
                    {
                        //An AttributeList will always have a child node Attribute
                        if (attribute.ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal static bool TryGetMethodReturnTypeNode(SyntaxNode node, out SyntaxNode returnTypeNode)
        {
            if (TryGetMethodDeclaration(node, out SyntaxNode methodDeclaration))
            {
                returnTypeNode = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName) || x.IsKind(SyntaxKind.PredefinedType) || x.IsKind(SyntaxKind.IdentifierName) || x.IsKind(SyntaxKind.ArrayType)).FirstOrDefault();
                return true;
            }

            returnTypeNode = null;
            return false;
        }

        private static bool TryGetAttribute(SyntaxNode node, string attributeName, out SyntaxNode attribute)
        {
            if (TryGetMethodDeclaration(node, out SyntaxNode methodDeclaration))
            {
                var attributeLists = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                foreach (var attributeList in attributeLists)
                {
                    //An AttributeList will always have a child node Attribute
                    attribute = attributeList.ChildNodes().First();
                    if (attribute.ChildNodes().First().ToString().Equals(attributeName))
                    {
                        return true;
                    }
                }
            }

            attribute = null;
            return false;

        }

        internal static bool TryGetMethodDeclaration(SyntaxNode node, out SyntaxNode methodDeclaration) => TryGetContainingSyntaxNode(node, SyntaxKind.MethodDeclaration, out methodDeclaration);

        internal static bool TryGetInvocationExpression(SyntaxNode node, out SyntaxNode invocationExpression) => TryGetContainingSyntaxNode(node, SyntaxKind.InvocationExpression, out invocationExpression);

        private static bool TryGetContainingSyntaxNode(SyntaxNode node, SyntaxKind kind, out SyntaxNode kindNode)
        {
            var currNode = node.IsKind(kind) ? node : node.Parent;
            while (!currNode.IsKind(kind))
            {
                if (currNode.IsKind(SyntaxKind.CompilationUnit))
                {
                    kindNode = null;
                    return false;
                }
                currNode = currNode.Parent;
            }

            kindNode = currNode;
            return true;
        }

        internal static bool IsInsideFunction(SemanticModel semanticModel, SyntaxNode node)
        {
            return TryGetFunctionName(semanticModel, node, out string functionName);
        }

        internal static bool TryGetClassName(SyntaxNode node, out string className)
        {
            var currNode = node.IsKind(SyntaxKind.ClassDeclaration) ? node : node.Parent;
            while (!currNode.IsKind(SyntaxKind.ClassDeclaration))
            {
                if (currNode.IsKind(SyntaxKind.CompilationUnit))
                {
                    className = null;
                    return false;
                }
                currNode = currNode.Parent;
            }

            var classDeclaration = (ClassDeclarationSyntax)currNode;
            className = classDeclaration.Identifier.ToString();
            return true;
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

        private static bool TryGetFunctionNameInConstant(SemanticModel semanticModel, SyntaxNode node, out string functionName)
        {
            if (node != null && (node.IsKind(SyntaxKind.IdentifierName) || node.IsKind(SyntaxKind.SimpleMemberAccessExpression)))
            {
                var constValue = semanticModel.GetConstantValue(node);
                if (constValue.HasValue && constValue.Value is string constString)
                {
                    functionName = constString;
                    return true;
                }
            }

            functionName = null;
            return false;
        }

        private static bool TryGetFunctionNameInNameOfOperator(SyntaxNode node, out string functionName)
        {
            if (node != null && node.IsKind(SyntaxKind.InvocationExpression))
            {
                var argumentList = node.ChildNodes().Where(x => x.IsKind(SyntaxKind.ArgumentList)).FirstOrDefault();
                if (argumentList != null)
                {
                    var argument = argumentList.ChildNodes().Where(x => x.IsKind(SyntaxKind.Argument)).FirstOrDefault();
                    if (argument != null)
                    {
                        var identifierName = argument.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName)).FirstOrDefault();
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
                var stringLiteralToken = node.ChildTokens().Where(x => x.IsKind(SyntaxKind.StringLiteralToken)).FirstOrDefault();
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

        internal static bool TryGetParameterNodeNextToAttribute(SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression, out SyntaxNode inputType)
        {
            var parameter = attributeExpression.Parent.Parent;
            inputType = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName) || x.IsKind(SyntaxKind.PredefinedType) || x.IsKind(SyntaxKind.GenericName)).FirstOrDefault();
            return inputType != null;
        }

        internal static bool TryGetTypeArgumentNode(MemberAccessExpressionSyntax expression, out SyntaxNode identifierNode)
        {
            var genericName = expression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName)).FirstOrDefault();
            if (genericName != null)
            {
                //GenericName will always have a TypeArgumentList
                var typeArgumentList = genericName.ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList)).First();

                //TypeArgumentList will always have a child node
                identifierNode = typeArgumentList.ChildNodes().First();
                return true;
            }

            identifierNode = null;
            return false;
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

        internal static string GetQualifiedTypeName(ITypeSymbol typeInfo)
        {
            if (typeInfo != null)
            {
                if (typeInfo is INamedTypeSymbol namedTypeInfo)
                {
                    var tupleUnderlyingType = namedTypeInfo.TupleUnderlyingType;
                    if (tupleUnderlyingType != null)
                    {
                        return $"System.Tuple<{string.Join(", ", tupleUnderlyingType.TypeArguments.Select(x => x.ToString()))}>";
                    }

                    return typeInfo.ToString();
                }

                var arrayString = "";
                if (typeInfo.Kind.Equals(SymbolKind.ArrayType))
                {
                    arrayString = "[]";
                    typeInfo = ((IArrayTypeSymbol)typeInfo).ElementType;
                }

                if (!string.IsNullOrEmpty(typeInfo.Name))
                {
                    return typeInfo.ContainingNamespace?.ToString() + "." + typeInfo.Name.ToString() + arrayString;
                }
            }

            return "Unknown Type";
        }

        internal static bool InputMatchesOrCompatibleType(ITypeSymbol invocationType, ITypeSymbol definitionType)
        {
            if (invocationType == null || definitionType == null)
            {
                return false;
            }

            return invocationType.Equals(definitionType)
                || AreEqualTupleTypes(invocationType, definitionType)
                || AreCompatibleIEnumerableTypes(invocationType, definitionType);
        }

        private static bool AreEqualTupleTypes(ITypeSymbol invocationType, ITypeSymbol definitionType)
        {
            var invocationQualifiedName = GetQualifiedTypeName(invocationType);
            var definitionQualifiedName = GetQualifiedTypeName(definitionType);

            return invocationQualifiedName.Equals(definitionQualifiedName);
        }

        private static bool AreCompatibleIEnumerableTypes(ITypeSymbol invocationType, ITypeSymbol functionType)
        {
            if (AreArrayOrNamedTypes(invocationType, functionType) && UnderlyingTypesMatch(invocationType, functionType))
            {
                return ((invocationType.AllInterfaces.Any(i => i.Name.Equals("IEnumerable")))
                    && (functionType.AllInterfaces.Any(i => i.Name.Equals("IEnumerable"))));
            }

            return false;
        }

        private static bool AreArrayOrNamedTypes(ITypeSymbol invocationType, ITypeSymbol functionType)
        {
            return (invocationType.Kind.Equals(SymbolKind.ArrayType) || invocationType.Kind.Equals(SymbolKind.NamedType))
                && (functionType.Kind.Equals(SymbolKind.ArrayType) || functionType.Kind.Equals(SymbolKind.NamedType));
        }

        private static bool UnderlyingTypesMatch(ITypeSymbol invocationType, ITypeSymbol functionType)
        {
            return (TryGetUnderlyingType(invocationType, out ITypeSymbol invocationUnderlyingType)
                && TryGetUnderlyingType(functionType, out ITypeSymbol functionUnderlyingType)
                && invocationUnderlyingType.Name.Equals(functionUnderlyingType.Name));
        }

        private static bool TryGetUnderlyingType(ITypeSymbol type, out ITypeSymbol underlyingType)
        {
            if (type.Kind.Equals(SymbolKind.ArrayType))
            {
                underlyingType = ((IArrayTypeSymbol)type).ElementType;
                return true;
            }

            if (type.Kind.Equals(SymbolKind.NamedType))
            {
                underlyingType = ((INamedTypeSymbol)type).TypeArguments.FirstOrDefault();
                return underlyingType != null;
            }
            else
            {
                underlyingType = null;
                return false;
            }
        }
    }
}
