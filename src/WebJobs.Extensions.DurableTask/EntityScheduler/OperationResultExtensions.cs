// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.Core.Entities;
using DurableTask.Core.Entities.EventFormat;
using DurableTask.Core.Entities.OperationFormat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class OperationResultExtensions
    {
        public static void SetResult(this OperationResult operationResult, object result, MessagePayloadDataConverter dataConverter)
        {
            operationResult.Error = null;
            if (result is JToken jtoken)
            {
                operationResult.Result = jtoken.ToString(Formatting.None);
            }
            else
            {
                operationResult.Result = dataConverter.Serialize(result);
            }
        }

        public static void SetExceptionResult(this OperationResult operationResult, Exception exception, string operation, MessagePayloadDataConverter errorDataConverter)
        {
            operationResult.Error = exception.GetType().AssemblyQualifiedName;

            try
            {
                operationResult.Result = errorDataConverter.Serialize(exception);
            }
            catch (Exception)
            {
                // sometimes, exceptions cannot be serialized. In that case we create a serializable wrapper
                // exception which lets the caller know something went wrong.

                var wrapper = string.IsNullOrEmpty(operation) ?
                      new OperationErrorException($"{operationResult.Error} while processing operations: {exception.Message}")
                    : new OperationErrorException($"{operationResult.Error} in operation '{operation}': {exception.Message}");

                operationResult.Error = wrapper.GetType().AssemblyQualifiedName;
                operationResult.Result = errorDataConverter.Serialize(wrapper);
            }
        }

        public static T GetResult<T>(this OperationResult operationResult, MessagePayloadDataConverter messageDataConverter, MessagePayloadDataConverter errorDataConverter)
        {
            if (operationResult.Error != null)
            {
                Exception e = null;

                // do a best-effort attempt at deserializing this exception
                try
                {
                    var type = Type.GetType(operationResult.Error, true);
                    e = (Exception)errorDataConverter.Deserialize(operationResult.Result, type);
                }
                catch
                {
                }

                if (e == null)
                {
                    // Could not deserialize. Let's just wrap it legibly,
                    // to help developers figure out what happened
                    e = new FunctionFailedException($"Entity operation threw {operationResult.Error}, content = {operationResult.Result}");
                }

                throw e;
            }
            else if (operationResult.Result == null)
            {
                return default(T);
            }
            else
            {
                return messageDataConverter.Deserialize<T>(operationResult.Result);
            }
        }
    }
}
