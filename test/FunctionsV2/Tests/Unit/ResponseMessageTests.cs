// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class ResponseMessageTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetResult_DoesNotDeserializeNonExceptionType()
        {
            var response = new ResponseMessage
            {
                ExceptionType = typeof(DeserializationCallback).AssemblyQualifiedName,
                Result = "{}",
            };

            DeserializationCallback.WasCreated = false;
            MessagePayloadDataConverter dataConverter = CreateDataConverter();

            Assert.Throws<FunctionFailedException>(() => response.GetResult<object>(dataConverter, dataConverter));
            Assert.False(DeserializationCallback.WasCreated);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetResult_IgnoresNestedTypeMetadataInException()
        {
            var result = new JObject
            {
                [nameof(ExceptionWithPayload.Payload)] = new JObject
                {
                    ["$type"] = typeof(DeserializationCallback).AssemblyQualifiedName,
                },
            };
            var response = new ResponseMessage
            {
                ExceptionType = typeof(ExceptionWithPayload).AssemblyQualifiedName,
                Result = result.ToString(Formatting.None),
            };

            DeserializationCallback.WasCreated = false;
            MessagePayloadDataConverter dataConverter = CreateDataConverter();

            Assert.Throws<ExceptionWithPayload>(() => response.GetResult<object>(dataConverter, dataConverter));
            Assert.False(DeserializationCallback.WasCreated);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetResult_HonorsConfiguredDateParseHandling()
        {
            const string Timestamp = "2026-08-14T12:34:56Z";
            var response = new ResponseMessage
            {
                ExceptionType = typeof(ExceptionWithPayload).AssemblyQualifiedName,
                Result = new JObject
                {
                    [nameof(ExceptionWithPayload.Payload)] = Timestamp,
                }.ToString(Formatting.None),
            };
            MessagePayloadDataConverter dataConverter = CreateDataConverter(DateParseHandling.None);

            ExceptionWithPayload exception = Assert.Throws<ExceptionWithPayload>(
                () => response.GetResult<object>(dataConverter, dataConverter));

            Assert.Equal(Timestamp, Assert.IsType<string>(exception.Payload));
        }

        private static MessagePayloadDataConverter CreateDataConverter(
            DateParseHandling dateParseHandling = DateParseHandling.DateTime)
        {
            return new MessagePayloadDataConverter(
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Objects,
                    DateParseHandling = dateParseHandling,
                },
                isDefault: true);
        }

        public class DeserializationCallback
        {
            public DeserializationCallback()
            {
                WasCreated = true;
            }

            public static bool WasCreated { get; set; }
        }

        public class ExceptionWithPayload : Exception
        {
            public object Payload { get; set; }
        }
    }
}