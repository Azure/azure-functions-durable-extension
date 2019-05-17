// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microsoft.Azure.WebJobs
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
        public static async Task DispatchAsync<T>(this IDurableEntityContext context)
            where T : new()
        {
            // find the method corresponding to the operation
            // (may throw an AmbiguousMatchException)
            var method = typeof(T).GetMethod(
                context.OperationName,
                System.Reflection.BindingFlags.IgnoreCase
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);

            // check that the number of arguments is zero or one
            var parameters = method.GetParameters();
            if (parameters.Length > 1)
            {
                throw new InvalidOperationException("Only a single argument can be used for operation input.");
            }

            object[] args;
            if (parameters.Length == 1)
            {
                // determine the expected type of the operation input and deserialize
                var inputType = method.GetParameters()[0].ParameterType;
                var input = context.GetInput(inputType);
                args = new object[1] { input };
            }
            else
            {
                args = Array.Empty<object>();
            }

            var state = context.GetState(() => new T());

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
    }
}
