// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    internal static class EntityProxyFactory
    {
        private static readonly ConcurrentDictionary<Type, Type> TypeMappings = new ConcurrentDictionary<Type, Type>();

        internal static TEntityInterface Create<TEntityInterface>(IEntityProxyContext context, EntityId entityId)
        {
            var type = TypeMappings.GetOrAdd(typeof(TEntityInterface), CreateProxyType);

            return (TEntityInterface)Activator.CreateInstance(type, context, entityId);
        }

        private static Type CreateProxyType(Type interfaceType)
        {
            var assemblyName = new AssemblyName($"DynamicAssembly_{Guid.NewGuid():N}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
            var typeName = $"{interfaceType.Name}_{Guid.NewGuid():N}";

            var typeBuilder = moduleBuilder.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.BeforeFieldInit | TypeAttributes.AnsiClass,
                typeof(EntityProxy));

            typeBuilder.AddInterfaceImplementation(interfaceType);

            BuildConstructor(typeBuilder);
            BuildMethods(typeBuilder, interfaceType);

            return typeBuilder.CreateTypeInfo();
        }

        private static void BuildConstructor(TypeBuilder typeBuilder)
        {
            var ctorArgTypes = new[] { typeof(IEntityProxyContext), typeof(EntityId) };

            // Create ctor
            var ctor = typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                ctorArgTypes);

            var ilGenerator = ctor.GetILGenerator();

            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Ldarg_2);
            ilGenerator.Emit(OpCodes.Call, typeof(EntityProxy).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, ctorArgTypes, null));
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void BuildMethods(TypeBuilder typeBuilder, Type interfaceType)
        {
            var methods = interfaceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

            var entityProxyMethods = typeof(EntityProxy).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);

            var callAsyncMethod = entityProxyMethods.First(x => x.Name == nameof(EntityProxy.CallAsync) && !x.IsGenericMethod);
            var callAsyncWithGenericMethod = entityProxyMethods.First(x => x.Name == nameof(EntityProxy.CallAsync) && x.IsGenericMethod);
            var signalMethod = entityProxyMethods.First(x => x.Name == nameof(EntityProxy.Signal));

            foreach (var methodInfo in methods)
            {
                var parameters = methodInfo.GetParameters();

                if (parameters.Length > 1)
                {
                    throw new InvalidOperationException("Only a single argument can be used for operation input.");
                }

                if (methodInfo.ReturnType != typeof(void) && !(methodInfo.ReturnType == typeof(Task) || methodInfo.ReturnType.BaseType == typeof(Task)))
                {
                    throw new InvalidOperationException("Only a return type in void, Task, Task<T>.");
                }

                var method = typeBuilder.DefineMethod(
                    methodInfo.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName | MethodAttributes.Virtual,
                    methodInfo.ReturnType,
                    parameters.Length == 0 ? null : new[] { parameters[0].ParameterType });

                typeBuilder.DefineMethodOverride(method, methodInfo);

                var ilGenerator = method.GetILGenerator();

                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldstr, methodInfo.Name);

                if (parameters.Length == 0)
                {
                    ilGenerator.Emit(OpCodes.Ldnull);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldarg_1);

                    if (parameters[0].ParameterType.IsValueType)
                    {
                        ilGenerator.Emit(OpCodes.Box, parameters[0].ParameterType);
                    }
                }

                if (methodInfo.ReturnType == typeof(void))
                {
                    ilGenerator.Emit(OpCodes.Call, signalMethod);
                }
                else
                {
                    ilGenerator.DeclareLocal(methodInfo.ReturnType);

                    ilGenerator.Emit(OpCodes.Call, methodInfo.ReturnType.IsGenericType ? callAsyncWithGenericMethod.MakeGenericMethod(methodInfo.ReturnType.GetGenericArguments()[0]) : callAsyncMethod);

                    ilGenerator.Emit(OpCodes.Stloc_0);
                    ilGenerator.Emit(OpCodes.Ldloc_0);
                }

                ilGenerator.Emit(OpCodes.Ret);
            }
        }
    }
}