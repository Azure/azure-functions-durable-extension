// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Linq;

namespace DurableFunctionsAnalyzer
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

            var versionTwoInterface = semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Azure.WebJobs.IDurableOrchestrationContext");
            version = versionTwoInterface != null ? DurableVersion.V2 : DurableVersion.V1;
            return (DurableVersion)version;
        }
        
        public static bool IsInsideOrchestrator(SyntaxNode node)
        {
            if (TryGetMethodDeclaration(out SyntaxNode methodDeclaration, node))
            {
                var parameterList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ParameterList)).First();

                foreach (SyntaxNode parameter in parameterList.ChildNodes())
                {
                    var attributeList = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                    if (attributeList.Count() >= 1 && attributeList.First().ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool TryGetClassSymbol(out INamedTypeSymbol classSymbol, SemanticModel semanticModel)
        {
            var classDeclarationSyntax = semanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last();
            if (classDeclarationSyntax != null)
            {
                var classDeclarationSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
                if (classDeclarationSymbol != null)
                {
                    classSymbol = classDeclarationSymbol;
                    return true;
                }
            }

            classSymbol = null;
            return false;
        }

        internal static bool TryGetFunctionName(out string functionName, SyntaxNode functionAttribute)
        {
            var attributeArgumentListEnumerable = functionAttribute.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeArgumentList));
            if (attributeArgumentListEnumerable.Any())
            {
                var attributeArgumentEnumerable = attributeArgumentListEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeArgument));
                if (attributeArgumentListEnumerable.Any())
                {
                    functionName = attributeArgumentListEnumerable.First().ToString().Trim('"');
                    return true;
                }
            }

            functionName = null;
            return false;
        }

        internal static bool TryGetFunctionAttribute(out SyntaxNode functionAttribute, SyntaxNode attributeExpression)
        {
            if (TryGetMethodDeclaration(out SyntaxNode methodDeclaration, attributeExpression))
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
            if (GetDeterministicAttribute(node) != null)
            {
                return true;
            }
            return false;
        }

        public static SyntaxNode GetDeterministicAttribute(SyntaxNode node)
        {
            if (TryGetMethodDeclaration(out SyntaxNode methodDeclaration, node))
            {
                var IEnumeratorAttributeList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                if (IEnumeratorAttributeList.Any())
                {
                    foreach (SyntaxNode attributeList in IEnumeratorAttributeList)
                    {
                        if (attributeList.ToString().Equals("[Deterministic]"))
                        {

                            return attributeList;
                        }
                    }
                }
                
            }
            
            return null;
        }

        internal static bool TryGetParameterNodeNextToAttribute(out SyntaxNode inputType, AttributeSyntax attributeExpression, SyntaxNodeAnalysisContext context)
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

        internal static bool TryGetReturnType(out ITypeSymbol returnType, AttributeSyntax attributeExpression, SyntaxNodeAnalysisContext context)
        {
            if (TryGetMethodDeclaration(out SyntaxNode methodDeclaration, attributeExpression))
            {
                returnType = context.SemanticModel.GetTypeInfo((methodDeclaration as MethodDeclarationSyntax).ReturnType).Type;
                return true;
            }

            returnType = null;
            return false;
        }

        internal static bool TryGetMethodDeclaration(out SyntaxNode methodDeclaration, SyntaxNode node)
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
            return true; ;
        }
    }
}
