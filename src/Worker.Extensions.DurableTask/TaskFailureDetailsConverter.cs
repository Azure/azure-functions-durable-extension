// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Microsoft.DurableTask.Worker;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
internal class TaskFailureDetailsConverter
{
    internal static P.TaskFailureDetails? TaskFailureFromException(Exception? fromException)
    {
        return TaskFailureFromException(fromException, null);
    }

    internal static P.TaskFailureDetails? TaskFailureFromException(Exception? fromException, IExceptionPropertiesProvider? exceptionPropertiesProvider)
    {
        if (fromException is null)
        {
            return null;
        }

        var failureDetails = new P.TaskFailureDetails()
        {
            ErrorType = fromException.GetType().FullName,
            ErrorMessage = fromException.Message,
            StackTrace = fromException.StackTrace,
            InnerFailure = TaskFailureFromException(fromException.InnerException, exceptionPropertiesProvider),
            IsNonRetriable = false
        };

        // Add custom properties if provider is available
        if (exceptionPropertiesProvider != null)
        {
            var customProperties = exceptionPropertiesProvider.GetExceptionProperties(fromException);
            if (customProperties != null && customProperties.Count > 0)
            {
                foreach (var property in customProperties)
                {
                    failureDetails.Properties[property.Key] = ConvertObjectToValue(property.Value);
                }
            }
        }

        return failureDetails;
    }

    private static Value ConvertObjectToValue(object? obj)
    {
        return obj switch
        {
            null => Value.ForNull(),
            string str => Value.ForString(str),
            bool b => Value.ForBool(b),
            int i => Value.ForNumber(i),
            long l => Value.ForNumber(l),
            float f => Value.ForNumber(f),
            double d => Value.ForNumber(d),
            decimal dec => Value.ForNumber((double)dec),

            // For DateTime and DateTimeOffset, add prefix to distinguish from normal string.
            DateTime dt => Value.ForString(dt.ToString("O")),
            DateTimeOffset dto => Value.ForString(dto.ToString("O")),
            IDictionary<string, object?> dict => Value.ForStruct(new Struct
            {
                Fields = { dict.ToDictionary(kvp => kvp.Key, kvp => ConvertObjectToValue(kvp.Value)) },
            }),
            IEnumerable e => Value.ForList(e.Cast<object?>().Select(ConvertObjectToValue).ToArray()),

            // Fallback: convert unlisted type to string.
            _ => Value.ForString(obj.ToString() ?? string.Empty),
        };
    }
}
