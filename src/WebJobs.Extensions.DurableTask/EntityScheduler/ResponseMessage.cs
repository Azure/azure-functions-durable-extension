// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ResponseMessage
    {
        [JsonProperty(PropertyName = "result")]
        public string Result { get; set; }

        [JsonProperty(PropertyName = "exceptionType", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ExceptionType { get; set; }

        [JsonIgnore]
        public bool IsException => this.ExceptionType != null;

        public void SetResult(object result, MessagePayloadDataConverter dataConverter)
        {
            this.ExceptionType = null;
            if (result is JToken jtoken)
            {
                this.Result = jtoken.ToString(Formatting.None);
            }
            else
            {
                this.Result = dataConverter.Serialize(result);
            }
        }

        public void SetExceptionResult(Exception exception, string operation, MessagePayloadDataConverter errorDataConverter)
        {
            this.ExceptionType = exception.GetType().AssemblyQualifiedName;

            try
            {
                this.Result = errorDataConverter.Serialize(exception);
            }
            catch (Exception)
            {
                // sometimes, exceptions cannot be serialized. In that case we create a serializable wrapper
                // exception which lets the caller know something went wrong.

                var wrapper = string.IsNullOrEmpty(operation) ?
                      new OperationErrorException($"{this.ExceptionType} while processing operations: {exception.Message}")
                    : new OperationErrorException($"{this.ExceptionType} in operation '{operation}': {exception.Message}");

                this.ExceptionType = wrapper.GetType().AssemblyQualifiedName;
                this.Result = errorDataConverter.Serialize(wrapper);
            }
        }

        public T GetResult<T>(MessagePayloadDataConverter messageDataConverter, MessagePayloadDataConverter errorDataConverter)
        {
            if (this.IsException)
            {
                Exception e = null;

                // This path assumes a runtime-generated entity response from the protected task hub.
                // ExceptionType is recorded by SetExceptionResult, separately from any JSON $type metadata.
                // Dynamic type resolution is intentional: a fixed type allowlist would break propagation of
                // customer-defined exceptions. Do not route arbitrary untrusted response metadata here.
                // The cast and catch below provide a best-effort fallback, not a security boundary:
                // deserialization can construct objects before the cast. Backend write access must be trusted.
                // See "Task Hub Trust Boundary and Exception Serialization" in this extension's README.md.
                try
                {
                    var type = Type.GetType(this.ExceptionType, true);
                    e = (Exception)errorDataConverter.Deserialize(this.Result, type);
                }
                catch
                {
                }

                if (e == null)
                {
                    // Could not deserialize. Let's just wrap it legibly,
                    // to help developers figure out what happened
                    e = new FunctionFailedException($"Entity operation threw {this.ExceptionType}, content = {this.Result}");
                }

                throw e;
            }
            else if (this.Result == null)
            {
                return default(T);
            }
            else
            {
                return messageDataConverter.Deserialize<T>(this.Result);
            }
        }

        public override string ToString()
        {
            if (this.IsException)
            {
                return $"[ExceptionResponse {this.Result}]";
            }
            else
            {
                return $"[Response {this.Result}]";
            }
        }
    }
}
