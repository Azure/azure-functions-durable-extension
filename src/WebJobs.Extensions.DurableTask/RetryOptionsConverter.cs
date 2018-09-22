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

            JToken firstRetryIntervalToken;
            if (!obj.TryGetValue(FirstRetryIntervalField, out firstRetryIntervalToken))
            {
                throw new ArgumentException($"Invalid JSON. Must contain field {FirstRetryIntervalField}", "reader");
            }

            JToken maxIntervalAttemptsToken;
            if (!obj.TryGetValue(MaxAttemptsField, out maxIntervalAttemptsToken))
            {
                throw new ArgumentException($"Invalid JSON. Must contain field {MaxAttemptsField}", "reader");
            }

            var firstRetryIntervalInMilliseconds = firstRetryIntervalToken.Value<int>();
            var maxNumberOfAttempts = maxIntervalAttemptsToken.Value<int>();

            var target = new RetryOptions(
                TimeSpan.FromMilliseconds(firstRetryIntervalInMilliseconds),
                maxNumberOfAttempts);

            JToken backoffCoefficientToken;
            if (obj.TryGetValue(BackoffCoefficientField, out backoffCoefficientToken))
            {
                target.BackoffCoefficient = backoffCoefficientToken.Value<double>();
            }

            JToken maxRetryIntervalToken;
            if (obj.TryGetValue(MaxRetryIntervalField, out maxRetryIntervalToken))
            {
                int maxRetryIntervalMilliseconds = maxRetryIntervalToken.Value<int>();
                target.MaxRetryInterval = TimeSpan.FromMilliseconds(maxRetryIntervalMilliseconds);
            }

            JToken retryTimeoutToken;
            if (obj.TryGetValue(RetryTimeoutField, out retryTimeoutToken))
            {
                int retryTimeoutMilliseconds = retryTimeoutToken.Value<int>();
                target.RetryTimeout = TimeSpan.FromMilliseconds(retryTimeoutMilliseconds);
            }

            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanWrite is false. The type will skip the converter.");
        }
    }
}
