// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Utils
{
    /// <summary>
    /// Extension methods for <see cref="Type"/>.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Dictionary of type names.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, string> typeNames;

        /// <summary>
        /// Initializes static members of the <see cref="TypeExtensions"/> class.
        /// </summary>
        static TypeExtensions()
        {
            typeNames = new ConcurrentDictionary<Type, string>
            {
                [typeof(bool)] = "bool",
                [typeof(byte)] = "byte",
                [typeof(char)] = "char",
                [typeof(decimal)] = "decimal",
                [typeof(double)] = "double",
                [typeof(float)] = "float",
                [typeof(int)] = "int",
                [typeof(long)] = "long",
                [typeof(sbyte)] = "sbyte",
                [typeof(short)] = "short",
                [typeof(string)] = "string",
                [typeof(uint)] = "uint",
                [typeof(ulong)] = "ulong",
                [typeof(ushort)] = "ushort",
                [typeof(void)] = "void",
                [typeof(object)] = "object"
            };
        }

        /// <summary>
        /// Gets the type name with generics and array ranks resolved.
        /// </summary>
        /// <param name="type">
        /// The type whose name to resolve.
        /// </param>
        /// <returns>
        /// The resolved type name.
        /// </returns>
        public static string ToCSTypeName(this Type type)
        {
            return typeNames.GetOrAdd(type, GetPrettyName);
        }

        /// <summary>
        /// Gets the type name as it would be written in C#
        /// </summary>
        /// <param name="type">
        /// The type whose name is to be written.
        /// </param>
        /// <returns>
        /// The type name as it is written in C#
        /// </returns>
        private static string GetPrettyName(Type type)
        {
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return $"{ToCSTypeName(Nullable.GetUnderlyingType(type))}?";
                }

                var typeList = string.Join(", ", type.GenericTypeArguments.Select(ToCSTypeName).ToArray());
                var typeName = type.Name.Split('`')[0];

                return $"{typeName}<{typeList}>";
            }

            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                var typeList = string.Join(", ", genericArgs.Select(ToCSTypeName).ToArray());
                var typeName = type.Name.Split('`')[0];

                return $"{typeName}<{typeList}>";
            }

            if (type.IsArray)
            {
                var arrayRank = string.Empty.PadLeft(type.GetArrayRank() - 1, ',');
                var elementType = ToCSTypeName(type.GetElementType());
                return $"{elementType}[{arrayRank}]";
            }

            return type.Name;
        }
    }
}
