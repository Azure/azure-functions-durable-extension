// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;
using DurableTask.Core.Tracing;
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

        [JsonProperty(PropertyName = "requestInfo", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public RequestInformation RequestInfo { get; set; }

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

                // do a best-effort attempt at deserializing this exception
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

        internal class RequestInformation
        {
            [JsonProperty(PropertyName = "operation")]
            public string Operation { get; set; }

            [JsonProperty(PropertyName = "scheduledTime")]
            public DateTime? ScheduledTime { get; set; }

            [JsonProperty(PropertyName = "requestTime")]
            public DateTimeOffset? RequestTime { get; set; }

            /// <summary>
            /// Span ID to use when creating an Activity for the call entity request that led to this response.
            /// This is used to correctly link the trace for the call request to the corresponding trace for fulfilling the call request <see cref="TaskEntityShim.ProcessOperationRequestAsync"/>.
            /// </summary>
            [JsonProperty(PropertyName = "clientSpanId")]
            public string ClientSpanId { get; set; }

            [JsonProperty(PropertyName = "parentTraceContext")]
            public DistributedTraceContext ParentTraceContext { get; set; }
        }
    }
}
