// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

        internal static bool IsInsideFunction(SyntaxNode node)
        {
            return TryGetFunctionNameAndNode(node, out SyntaxNode functionAttribute, out string functionName);
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

        internal static bool TryGetFunctionNameAndNode(SyntaxNode node, out SyntaxNode attributeArgument, out string functionName)
        {
            if (TryGetFunctionAttribute(node, out SyntaxNode functionAttribute))
            {
                if (TryGetFunctionNameAttributeArgument(functionAttribute, out attributeArgument))
                {
                    if (TryGetFunctionName(attributeArgument, out functionName))
                    {
                        return true;
                    }
                }
            }

            attributeArgument = null;
            functionName = null;
            return false;
        }

        private static bool TryGetFunctionName(SyntaxNode attributeArgument, out string functionName)
        {
            var stringLiteralExpression = attributeArgument.ChildNodes().Where(x => x.IsKind(SyntaxKind.StringLiteralExpression)).FirstOrDefault();
            if (stringLiteralExpression != null)
            {
                var stringLiteralToken = stringLiteralExpression.ChildTokens().Where(x => x.IsKind(SyntaxKind.StringLiteralToken)).FirstOrDefault();
                if (stringLiteralToken != null)
                {
                    functionName = stringLiteralToken.ValueText;
                    return true;
                }
            }

            var invocationExpression = attributeArgument.ChildNodes().Where(x => x.IsKind(SyntaxKind.InvocationExpression)).FirstOrDefault();
            if (invocationExpression != null)
            {
                var argumentList = invocationExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.ArgumentList)).FirstOrDefault();
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

        internal static bool TryGetTypeArgumentIdentifierNode(MemberAccessExpressionSyntax expression, out SyntaxNode identifierNode)
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
    }
}
