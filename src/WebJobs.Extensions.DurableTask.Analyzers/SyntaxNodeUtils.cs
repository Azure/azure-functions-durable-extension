// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
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

        internal static bool IsMarkedDeterministic(SyntaxNode node) => TryGetDeterministicAttribute(node, out _);


        internal static bool TryGetDeterministicAttribute(SyntaxNode node, out SyntaxNode deterministicAttribute) => TryGetAttribute(node, "Deterministic", out deterministicAttribute);

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

        internal static bool TryGetClassSymbol(SyntaxNode node, SemanticModel semanticModel, out INamedTypeSymbol classSymbol)
        {
            var currNode = node.IsKind(SyntaxKind.ClassDeclaration) ? node : node.Parent;
            while (!currNode.IsKind(SyntaxKind.ClassDeclaration))
            {
                if (currNode.IsKind(SyntaxKind.CompilationUnit))
                {
                    classSymbol = null;
                    return false;
                }
                currNode = currNode.Parent;
            }

            var classDeclaration = (ClassDeclarationSyntax)currNode;
            classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            return true;
        }

        internal static bool TryGetFunctionNameParameterNode(AttributeSyntax attributeExpression, out SyntaxNode attributeArgument)
        {
            if (TryGetFunctionAttribute(attributeExpression, out SyntaxNode functionAttribute))
            {
                return TryGetFunctionName(functionAttribute, out attributeArgument);
            }

            attributeArgument = null;
            return false;
        }

        private static bool TryGetFunctionName(SyntaxNode functionAttribute, out SyntaxNode attributeArgument)
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
