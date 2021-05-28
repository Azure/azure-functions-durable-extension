// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Models
{
    public enum DurableFunctionKind
    {
        Unknonwn,
        Orchestration,
        Activity
    }

    public class DurableFunction
    {
        public string FullTypeName { get; }
        public HashSet<string> RequiredNamespaces { get; }
        public string Name { get; }
        public DurableFunctionKind Kind { get; }
        public List<TypedParameter> Parameters { get; }
        public string ReturnType { get; }
        public string CallGenerics { get; }

        public DurableFunction(string fullTypeName, string name, DurableFunctionKind kind, List<TypedParameter> parameters, TypeSyntax returnTypeSyntax, HashSet<string> requiredNamespaces)
        {
            FullTypeName = fullTypeName;
            RequiredNamespaces = requiredNamespaces;
            Name = name;
            Kind = kind;
            Parameters = parameters;

            if (returnTypeSyntax is GenericNameSyntax genericReturnTypeSyntax)
            {
                var identifier = genericReturnTypeSyntax.Identifier;
                var typeArgumentList = genericReturnTypeSyntax.TypeArgumentList;

                ReturnType = genericReturnTypeSyntax.ToString();
                CallGenerics = $"<{typeArgumentList.Arguments.First()}>";
            }
            else if (returnTypeSyntax is IdentifierNameSyntax identifierNameSyntax)
            {
                var identifier = identifierNameSyntax;

                var isVoidReturn = ReturnType == "Task";
                ReturnType = isVoidReturn ? "Task" : $"Task<{identifier}>";
                CallGenerics = isVoidReturn ? string.Empty : $"<{identifier.ToFullString()}>";
            }
            else if (returnTypeSyntax is PredefinedTypeSyntax predefinedTypeSyntax)
            {
                ReturnType = $"Task<{predefinedTypeSyntax}>";
                CallGenerics = $"<{predefinedTypeSyntax.ToFullString()}>";
            }
        }

        public static bool TryParse(SemanticModel model, MethodDeclarationSyntax method, out DurableFunction function)
        {
            function = null;

            if (!SyntaxNodeUtility.TryGetFunctionName(model, method, out string name))
                return false;

            if (!SyntaxNodeUtility.TryGetFunctionKind(method, out DurableFunctionKind kind))
                return false;

            if (!SyntaxNodeUtility.TryGetReturnType(method, out TypeSyntax returnType))
                return false;

            if (!SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> parameters))
                return false;

            if (!SyntaxNodeUtility.TryGetQualifiedTypeName(model, method, out string fullTypeName))
                return false;

            var usedTypes = new List<TypeSyntax>();
            usedTypes.Add(returnType);
            usedTypes.AddRange(parameters.Select(p => p.Type));

            if (!SyntaxNodeUtility.TryGetRequiredNamespaces(model, usedTypes, out HashSet<string> requiredNamespaces))
                return false;

            requiredNamespaces.UnionWith(GetRequiredGlobalNamespaces());

            function = new DurableFunction(fullTypeName, name, kind, parameters, returnType, requiredNamespaces);
            return true;
        }

        private static string[] GetRequiredGlobalNamespaces()
        {
            return new[] {
                "System.Threading.Tasks",
                "Microsoft.Azure.WebJobs.Extensions.DurableTask"
            };
        }
    }
}
