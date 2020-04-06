// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Extends the durable entity context to support reflection-based invocation of entity operations.
    /// </summary>
    public static class TypedInvocationExtensions
    {
        /// <summary>
        /// Dynamically dispatches the incoming entity operation using reflection.
        /// </summary>
        /// <typeparam name="T">The class to use for entity instances.</typeparam>
        /// <returns>A task that completes when the dispatched operation has finished.</returns>
        /// <exception cref="AmbiguousMatchException">If there is more than one method with the given operation name.</exception>
        /// <exception cref="MissingMethodException">If there is no method with the given operation name.</exception>
        /// <exception cref="InvalidOperationException">If the method has more than one argument.</exception>
        /// <remarks>
        /// If the entity's state is null, an object of type <typeparamref name="T"/> is created first. Then, reflection
        /// is used to try to find a matching method. This match is based on the method name
        /// (which is the operation name) and the argument list (which is the operation content, deserialized into
        /// an object array).
        /// </remarks>
        /// <param name="context">Context object to use to dispatch entity operations.</param>
        /// <param name="constructorParameters">Parameters to feed to the entity constructor. Should be primarily used for
        /// output bindings. Parameters must match the order in the constructor after ignoring parameters populated on
        /// constructor via dependency injection.</param>
        public static async Task DispatchAsync<T>(this IDurableEntityContext context, params object[] constructorParameters)
            where T : class
        {
            MethodInfo method = FindMethodForContext<T>(context);

            if (method == null)
            {
                if (string.Equals("delete", context.OperationName, StringComparison.InvariantCultureIgnoreCase))
                {
                    Entity.Current.DeleteState();
                    return;
                }
                else
                {
                    throw new InvalidOperationException($"No operation named '{context.OperationName}' was found.");
                }
            }

            // check that the number of arguments is zero or one
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length > 1)
            {
                throw new InvalidOperationException("Only a single argument can be used for operation input.");
            }

            object[] args;
            if (parameters.Length == 1)
            {
                // determine the expected type of the operation input and deserialize
                Type inputType = method.GetParameters()[0].ParameterType;
                object input = context.GetInput(inputType);
                args = new object[1] { input };
            }
            else
            {
                args = Array.Empty<object>();
            }

#if !FUNCTIONS_V1
            T Constructor() => (T)context.FunctionBindingContext.CreateObjectInstance(typeof(T), constructorParameters);
#else
            T Constructor() => (T)Activator.CreateInstance(typeof(T), constructorParameters);
#endif

            var state = ((Extensions.DurableTask.DurableEntityContext)context).GetStateWithInjectedDependencies(Constructor);

            object result = method.Invoke(state, args);

            if (method.ReturnType != typeof(void))
            {
                if (result is Task task)
                {
                    await task;

                    if (task.GetType().IsGenericType)
                    {
                        context.Return(task.GetType().GetProperty("Result").GetValue(task));
                    }
                }
                else
                {
                    context.Return(result);
                }
            }
        }

        internal static MethodInfo FindMethodForContext<T>(IDurableEntityContext context)
        {
            var type = typeof(T);

            var interfaces = type.GetInterfaces();
            const BindingFlags bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var method = type.GetMethod(context.OperationName, bindingFlags);
            if (interfaces.Length == 0 || method != null)
            {
                return method;
            }

            return interfaces.Select(i => i.GetMethod(context.OperationName, bindingFlags)).FirstOrDefault(m => m != null);
        }
    }
}
