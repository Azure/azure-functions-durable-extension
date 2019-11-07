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
        
        public static bool IsInsideOrchestrator(SyntaxNode node)
        {
            if (TryGetMethodDeclaration(node, out SyntaxNode methodDeclaration))
            {
                var parameterList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ParameterList)).First();

                foreach (SyntaxNode parameter in parameterList.ChildNodes())
                {
                    var attributeListEnumerable = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                    foreach (SyntaxNode attribute in attributeListEnumerable)
                    {
                        if (attribute.ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        
        public static bool IsMarkedDeterministic(SyntaxNode node)
        {
            if (TryGetDeterministicAttribute(node, out SyntaxNode deterministicAttribute))
            {
                return true;
            }
            return false;
        }


        public static bool TryGetDeterministicAttribute(SyntaxNode node, out SyntaxNode deterministicAttribute)
        {
            if (TryGetMethodDeclaration(node, out SyntaxNode methodDeclaration))
            {
                var IEnumeratorAttributeList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                if (IEnumeratorAttributeList.Any())
                {
                    foreach (SyntaxNode attributeList in IEnumeratorAttributeList)
                    {
                        if (attributeList.ToString().Equals("[Deterministic]"))
                        {
                            deterministicAttribute = attributeList;
                            return true;
                        }
                    }
                }

            }

            deterministicAttribute = null;
            return false;
        }

        internal static bool TryGetMethodDeclaration(SyntaxNode node, out SyntaxNode methodDeclaration)
        {
            var currNode = node.IsKind(SyntaxKind.MethodDeclaration) ? node : node.Parent;
            while (!currNode.IsKind(SyntaxKind.MethodDeclaration))
            {
                if (currNode.IsKind(SyntaxKind.CompilationUnit))
                {
                    methodDeclaration = null;
                    return false;
                }
                currNode = currNode.Parent;
            }

            methodDeclaration = currNode;
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

        internal static bool TryGetFunctionNameNode(AttributeSyntax attributeExpression, out SyntaxNode attributeArgument)
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

        private static bool TryGetFunctionAttribute(SyntaxNode attributeExpression, out SyntaxNode functionAttribute)
        {
            if (TryGetMethodDeclaration(attributeExpression, out SyntaxNode methodDeclaration))
            {
                var attributeLists = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                if (attributeLists.Any())
                {
                    foreach (var attributeList in attributeLists)
                    {
                        var attribute = attributeList.ChildNodes().First();
                        if (attribute.ChildNodes().First().ToString().Equals("FunctionName"))
                        {
                            functionAttribute = attribute;
                            return true;
                        }
                    }
                }
            }

            functionAttribute = null;
            return false;
        }

        internal static bool TryGetParameterNodeNextToAttribute(SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression, out SyntaxNode inputType)
        {
            var parameter = attributeExpression.Parent.Parent;
            var parameterTypeNamesEnumerable = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName) || x.IsKind(SyntaxKind.PredefinedType) || x.IsKind(SyntaxKind.GenericName));
            if (parameterTypeNamesEnumerable.Any())
            {
                inputType = parameterTypeNamesEnumerable.First();
                return true;
            }

            inputType = null;
            return false;
        }

        internal static bool TryGetTypeArgumentList(MemberAccessExpressionSyntax expression, out SyntaxNode identifierNode)
        {
            var genericNameEnumerable = expression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName));
            if (genericNameEnumerable.Any())
            {
                //GenericName will always have a TypeArgumentList
                var typeArgumentList = genericNameEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList)).First();

                //TypeArgumentList will always have a child node
                identifierNode = typeArgumentList.ChildNodes().First();
                return true;
            }

            identifierNode = null;
            return false;
        }

        internal static bool TryGetActivityTriggerAttributeExpression(SyntaxNodeAnalysisContext context, out AttributeSyntax attributeExpression)
        {
            return TryGetAttributeExpression(context, "ActivityTrigger", out attributeExpression);
        }

        internal static bool TryGetEntityTriggerAttributeExpression(SyntaxNodeAnalysisContext context, out AttributeSyntax attributeExpression)
        {
            return TryGetAttributeExpression(context, "EntityTrigger", out attributeExpression);
        }

        private static bool TryGetAttributeExpression(SyntaxNodeAnalysisContext context, string attributeName, out AttributeSyntax attributeExpression)
        {
            attributeExpression = context.Node as AttributeSyntax;
            if (attributeExpression != null && attributeExpression.ChildNodes().First().ToString() == attributeName)
            {
                return true;
            }

            attributeExpression = null;
            return false;
        }
    }
}
