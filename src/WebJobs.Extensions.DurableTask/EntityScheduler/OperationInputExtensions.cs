// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.Core.Entities.EventFormat;
using DurableTask.Core.Entities.OperationFormat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class OperationInputExtensions
    {
        public static string SerializeOperationInput(string operationName, object obj, MessagePayloadDataConverter dataConverter)
        {
            try
            {
                if (obj is JToken jtoken)
                {
                    return jtoken.ToString(Formatting.None);
                }
                else
                {
                    return dataConverter.Serialize(obj);
                }
            }
            catch (Exception e)
            {
                throw new EntitySchedulerException($"Failed to serialize input for operation '{operationName}': {e.Message}", e);
            }
        }

        public static TInput GetInput<TInput>(this OperationRequest operationRequest, MessagePayloadDataConverter dataConverter)
        {
            try
            {
                return dataConverter.Deserialize<TInput>(operationRequest.Input);
            }
            catch (Exception e)
            {
                throw new EntitySchedulerException($"Failed to deserialize input for operation '{operationRequest.Operation}': {e.Message}", e);
            }
        }

        public static object GetInput(this OperationRequest operationRequest, Type inputType, MessagePayloadDataConverter dataConverter)
        {
            try
            {
                return dataConverter.Deserialize(operationRequest.Input, inputType);
            }
            catch (Exception e)
            {
                throw new EntitySchedulerException($"Failed to deserialize input for operation '{operationRequest.Operation}': {e.Message}", e);
            }
        }
    }
}
