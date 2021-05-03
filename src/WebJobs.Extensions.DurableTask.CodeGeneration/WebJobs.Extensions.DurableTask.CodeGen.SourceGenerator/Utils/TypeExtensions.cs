// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Utils
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

        public static bool IsNullable(this PropertyInfo property) => IsNullableHelper(property.PropertyType, property.DeclaringType, property.CustomAttributes);

        public static bool IsNullable(this FieldInfo field) => IsNullableHelper(field.FieldType, field.DeclaringType, field.CustomAttributes);

        public static bool IsNullable(this ParameterInfo parameter) => IsNullableHelper(parameter.ParameterType, parameter.Member, parameter.CustomAttributes);

        private static bool IsNullableHelper(Type memberType, MemberInfo? declaringType, IEnumerable<CustomAttributeData> customAttributes)
        {
            if (memberType.IsValueType)
                return Nullable.GetUnderlyingType(memberType) != null;

            var nullable = customAttributes
                .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
            if (nullable != null && nullable.ConstructorArguments.Count == 1)
            {
                var attributeArgument = nullable.ConstructorArguments[0];
                if (attributeArgument.ArgumentType == typeof(byte[]))
                {
                    var args = (ReadOnlyCollection<CustomAttributeTypedArgument>)attributeArgument.Value!;
                    if (args.Count > 0 && args[0].ArgumentType == typeof(byte))
                    {
                        return (byte)args[0].Value! == 2;
                    }
                }
                else if (attributeArgument.ArgumentType == typeof(byte))
                {
                    return (byte)attributeArgument.Value! == 2;
                }
            }

            for (var type = declaringType; type != null; type = type.DeclaringType)
            {
                var context = type.CustomAttributes
                    .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
                if (context != null &&
                    context.ConstructorArguments.Count == 1 &&
                    context.ConstructorArguments[0].ArgumentType == typeof(byte))
                {
                    return (byte)context.ConstructorArguments[0].Value! == 2;
                }
            }

            // Couldn't find a suitable attribute
            return false;
        }
    }
}
