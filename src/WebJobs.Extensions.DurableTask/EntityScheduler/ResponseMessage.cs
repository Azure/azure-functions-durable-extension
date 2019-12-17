// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ResponseMessage
    {
        // This no-arg constructor is used when a RequestMessage is deserialized.
        public ResponseMessage() { }

        public ResponseMessage(MessagePayloadDataConverter dataConverter)
        {
            if (dataConverter == null)
            {
                throw new ArgumentNullException(nameof(dataConverter));
            }

            this.DataConverter = dataConverter;
        }

        /// <summary>
        /// MessagePayloadDataConverter for serialization.
        /// </summary>
        [JsonIgnore]
        public MessagePayloadDataConverter DataConverter { get; set; }

        [JsonProperty(PropertyName = "result")]
        public string Result { get; set; }

        [JsonProperty(PropertyName = "exceptionType", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ExceptionType { get; set; }

        [JsonIgnore]
        public bool IsException => this.ExceptionType != null;

        public void SetResult(object result)
        {
            this.ExceptionType = null;
            if (result is JToken jtoken)
            {
                this.Result = jtoken.ToString(Formatting.None);
            }
            else
            {
                this.Result = this.DataConverter.MessageConverter.Serialize(result);
            }
        }

        public void SetExceptionResult(Exception exception, string operation, EntityId entity)
        {
            this.ExceptionType = exception.GetType().AssemblyQualifiedName;

            try
            {
                this.Result = this.DataConverter.ErrorConverter.Serialize(exception);
            }
            catch (Exception)
            {
                // sometimes, exceptions cannot be serialized. In that case we create a serializable wrapper
                // exception which lets the caller know something went wrong.

                var wrapper = new OperationErrorException($"{this.ExceptionType} in operation '{operation}': {exception.Message}");
                this.ExceptionType = wrapper.GetType().AssemblyQualifiedName;
                this.Result = this.DataConverter.ErrorConverter.Serialize(wrapper);
            }
        }

        public T GetResult<T>()
        {
            if (this.IsException)
            {
                Exception e = null;

                // do a best-effort attempt at deserializing this exception
                try
                {
                    var type = Type.GetType(this.ExceptionType, true);
                    e = (Exception)this.DataConverter.ErrorConverter.Deserialize(this.Result, type);
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
                return this.DataConverter.MessageConverter.Deserialize<T>(this.Result);
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
