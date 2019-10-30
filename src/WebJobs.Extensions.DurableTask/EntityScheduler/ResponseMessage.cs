// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ResponseMessage
    {
        private MessagePayloadDataConverter dataConverter;

        public ResponseMessage(MessagePayloadDataConverter dataConverter)
        {
            this.dataConverter = dataConverter;
        }

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
                this.Result = this.dataConverter.MessageConverter.Serialize(result);
            }
        }

        public void SetExceptionResult(Exception exception, string operation, EntityId entity)
        {
            this.ExceptionType = exception.GetType().AssemblyQualifiedName;

            try
            {
                this.Result = this.dataConverter.ErrorConverter.Serialize(exception);
            }
            catch (Exception)
            {
                // sometimes, exceptions cannot be serialized. In that case we create a serializable wrapper
                // exception which lets the caller know something went wrong.

                var wrapper = new OperationErrorException($"{this.ExceptionType} in operation '{operation}': {exception.Message}");
                this.ExceptionType = wrapper.GetType().AssemblyQualifiedName;
                this.Result = this.dataConverter.ErrorConverter.Serialize(wrapper);
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
                    e = (Exception)this.dataConverter.ErrorConverter.Deserialize(this.Result, type);
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
                return this.dataConverter.MessageConverter.Deserialize<T>(this.Result);
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
