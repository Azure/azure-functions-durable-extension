// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

        internal static bool TryGetFunctionName(SyntaxNode functionAttribute, out SyntaxNode attributeArgument)
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

        internal static bool TryGetFunctionAttribute(SyntaxNode attributeExpression, out SyntaxNode functionAttribute)
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

        internal static bool TryGetParameterNodeNextToAttribute(AttributeSyntax attributeExpression, SyntaxNodeAnalysisContext context, out SyntaxNode inputType)
        {
            var parameter = attributeExpression.Parent.Parent;
            var parameterTypeNamesEnumerable = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName) || x.IsKind(SyntaxKind.PredefinedType));
            if (parameterTypeNamesEnumerable.Any())
            {
                inputType = parameterTypeNamesEnumerable.First();
                return true;
            }

            inputType = null;
            return false;
        }

        internal static bool TryGetReturnType(AttributeSyntax attributeExpression, SyntaxNodeAnalysisContext context, out ITypeSymbol returnType)
        {
            if (TryGetMethodDeclaration(attributeExpression, out SyntaxNode methodDeclaration))
            {
                returnType = context.SemanticModel.GetTypeInfo((methodDeclaration as MethodDeclarationSyntax).ReturnType).Type;
                return true;
            }

            returnType = null;
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
    }
}
