// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class EntityProxyFactory
    {
        private static readonly ModuleBuilder DynamicModuleBuilder;
        private static readonly ConcurrentDictionary<Type, Type> TypeMappings = new ConcurrentDictionary<Type, Type>();

        static EntityProxyFactory()
        {
            var assemblyName = new AssemblyName($"DynamicAssembly_{Guid.NewGuid():N}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            DynamicModuleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
        }

        internal static TEntityInterface Create<TEntityInterface>(IEntityProxyContext context, EntityId entityId)
        {
            var type = TypeMappings.GetOrAdd(typeof(TEntityInterface), CreateProxyType);

            return (TEntityInterface)Activator.CreateInstance(type, context, entityId);
        }

        private static Type CreateProxyType(Type interfaceType)
        {
            ValidateInterface(interfaceType);

            var typeName = $"{interfaceType.Name}_{Guid.NewGuid():N}";

            var typeBuilder = DynamicModuleBuilder.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.BeforeFieldInit | TypeAttributes.AnsiClass,
                typeof(EntityProxy));

            typeBuilder.AddInterfaceImplementation(interfaceType);

            BuildConstructor(typeBuilder);
            BuildMethods(typeBuilder, interfaceType);

            return typeBuilder.CreateTypeInfo();
        }

        private static void ValidateInterface(Type interfaceType)
        {
            if (!interfaceType.IsInterface)
            {
                throw new InvalidOperationException($"{interfaceType.Name} is not an interface. Entity proxy type parameters must be interfaces.");
            }

            if (interfaceType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0)
            {
                throw new InvalidOperationException($"Interface '{interfaceType.FullName}' defines properties. Entity proxy interfaces with properties are not supported.");
            }
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
            var methods = interfaceType.GetMethods(BindingFlags.Instance | BindingFlags.Public).ToList();

            // Interfaces can inherit from other interfaces, getting those methods too
            var interfaces = interfaceType.GetInterfaces();
            if (interfaces.Length > 0)
            {
                foreach (var inter in interfaces)
                {
                    methods.AddRange(inter.GetMethods(BindingFlags.Instance | BindingFlags.Public));
                }
            }

            if (methods.Count == 0)
            {
                throw new InvalidOperationException($"Interface '{interfaceType.FullName}' has no methods defined.");
            }

            var entityProxyMethods = typeof(EntityProxy).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);

            var callAsyncMethod = entityProxyMethods.First(x => x.Name == nameof(EntityProxy.CallAsync) && !x.IsGenericMethod);
            var callAsyncGenericMethod = entityProxyMethods.First(x => x.Name == nameof(EntityProxy.CallAsync) && x.IsGenericMethod);
            var signalMethod = entityProxyMethods.First(x => x.Name == nameof(EntityProxy.Signal));

            foreach (var methodInfo in methods)
            {
                var parameters = methodInfo.GetParameters();

                // check that the number of arguments is zero or one
                if (parameters.Length > 1)
                {
                    throw new InvalidOperationException($"Method '{methodInfo.Name}' defines more than one parameter. Entity proxy interface methods must define at most one argument for operation input.");
                }

                var returnType = methodInfo.ReturnType;

                // check that return type is void / Task / Task<T>.
                if (returnType != typeof(void) && !(returnType == typeof(Task) || returnType.BaseType == typeof(Task)))
                {
                    throw new InvalidOperationException($"Method '{methodInfo.Name}' has a return type which is neither void nor a Task. Entity proxy interface methods may only return void, Task, or Task<T>.");
                }

                var proxyMethod = typeBuilder.DefineMethod(
                    methodInfo.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName | MethodAttributes.Virtual,
                    returnType,
                    parameters.Length == 0 ? null : new[] { parameters[0].ParameterType });

                typeBuilder.DefineMethodOverride(proxyMethod, methodInfo);

                var ilGenerator = proxyMethod.GetILGenerator();

                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldstr, methodInfo.Name);

                if (parameters.Length == 0)
                {
                    ilGenerator.Emit(OpCodes.Ldnull);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldarg_1);

                    // ValueType needs boxing.
                    if (parameters[0].ParameterType.IsValueType)
                    {
                        ilGenerator.Emit(OpCodes.Box, parameters[0].ParameterType);
                    }
                }

                if (returnType == typeof(void))
                {
                    ilGenerator.Emit(OpCodes.Call, signalMethod);
                }
                else
                {
                    ilGenerator.DeclareLocal(returnType);

                    ilGenerator.Emit(OpCodes.Call, returnType.IsGenericType ? callAsyncGenericMethod.MakeGenericMethod(returnType.GetGenericArguments()[0]) : callAsyncMethod);

                    ilGenerator.Emit(OpCodes.Stloc_0);
                    ilGenerator.Emit(OpCodes.Ldloc_0);
                }

                ilGenerator.Emit(OpCodes.Ret);
            }
        }
    }
}
