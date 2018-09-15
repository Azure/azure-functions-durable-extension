// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RetryOptionsConverter : JsonConverter
    {
        private const string BackoffCoefficientField = "backoffCoefficient";
        private const string FirstRetryIntervalField = "firstRetryIntervalInMilliseconds";
        private const string MaxAttemptsField = "maxNumberOfAttempts";
        private const string MaxRetryIntervalField = "maxRetryIntervalInMilliseconds";
        private const string RetryTimeoutField = "retryTimeoutInMilliseconds";

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => objectType.IsAssignableFrom(typeof(RetryOptions));

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);

            if (!this.FieldExists(FirstRetryIntervalField, obj))
            {
                throw new ArgumentException("Invalid JSON. Must contain field firstRetryIntervalInMilliseconds", "reader");
            }

            if (!this.FieldExists(MaxAttemptsField, obj))
            {
                throw new ArgumentException("Invalid JSON. Must contain field maxNumberOfAttempts", "reader");
            }

            var firstRetryIntervalInMilliseconds = obj[FirstRetryIntervalField].Value<int>();
            var maxNumberOfAttempts = obj[MaxAttemptsField].Value<int>();

            var target = new RetryOptions(
                new TimeSpan(firstRetryIntervalInMilliseconds * TimeSpan.TicksPerMillisecond),
                maxNumberOfAttempts);

            if (this.FieldExists(BackoffCoefficientField, obj))
            {
                target.BackoffCoefficient = obj[BackoffCoefficientField].Value<double>();
            }

            if (this.FieldExists(MaxRetryIntervalField, obj))
            {
                target.MaxRetryInterval = new TimeSpan(obj[MaxRetryIntervalField].Value<int>() * TimeSpan.TicksPerMillisecond);
            }

            if (this.FieldExists(RetryTimeoutField, obj))
            {
                target.RetryTimeout = new TimeSpan(obj[RetryTimeoutField].Value<int>() * TimeSpan.TicksPerMillisecond);
            }

            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanWrite is false. The type will skip the converter.");
        }

        private bool FieldExists(string fieldName, JObject jObject)
        {
            return jObject[fieldName] != null;
        }
    }
}
