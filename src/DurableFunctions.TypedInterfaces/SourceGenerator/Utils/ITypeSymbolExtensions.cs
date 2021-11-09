// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Utils
{
    public static class ITypeSymbolExtensions
    {
        public static bool IsSystemVoid(this ITypeSymbol symbol)
            => symbol?.SpecialType == SpecialType.System_Void;

        public static string GetFullyQualifiedName(this ITypeSymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public static IList<INamedTypeSymbol> GetAllInterfacesIncludingThis(this ITypeSymbol type)
        {
            var allInterfaces = type.AllInterfaces;
            if (type is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Interface && !allInterfaces.Contains(namedType))
            {
                var result = new List<INamedTypeSymbol>(allInterfaces.Length + 1);
                result.Add(namedType);
                result.AddRange(allInterfaces);
                return result;
            }

            return allInterfaces;
        }

        public static string ToPrettyString(this ITypeSymbol type)
        {
            if (type is INamedTypeSymbol namedTypeSymbol)
                return namedTypeSymbol.ToPrettyString();

            return (type.SpecialType != SpecialType.None) ? type.ToString() : type.ToDisplayString();
        }

        public static string ToPrettyString(this INamedTypeSymbol type)
        {
            if (type.IsGenericType)
            {
                var argumentPrettyStrings = new List<string>();

                for (var i = 0; i < type.TypeArguments.Length; i++)
                {
                    var typeArgument = type.TypeArguments[i];

                    argumentPrettyStrings.Add(typeArgument.ToPrettyString());
                }

                return $"{type.Name}<{string.Join(",", argumentPrettyStrings)}>";
            }

            return (type.SpecialType != SpecialType.None) ? type.ToString() : type.Name;
        }

    }
}
