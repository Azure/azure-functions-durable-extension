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
        [JsonProperty(PropertyName = "result")]
        public string Result { get; set; }

        [JsonProperty(PropertyName = "exceptionType", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ExceptionType { get; set; }

        [JsonIgnore]
        public bool IsException => ExceptionType != null;

        public void SetResult(object result)
        {
            this.ExceptionType = null;
            if (result is JToken jtoken)
            {
                this.Result = jtoken.ToString(Formatting.None);
            }
            else
            {
                this.Result = MessagePayloadDataConverter.Default.Serialize(result);
            }
        }

        public void SetExceptionResult(Exception exception, string operation, ActorId actor)
        {
            this.ExceptionType = exception.GetType().AssemblyQualifiedName;

            this.Result = MessagePayloadDataConverter.ErrorConverter.Serialize(exception);
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
                    e = (Exception)MessagePayloadDataConverter.ErrorConverter.Deserialize(this.Result, type);
                }
                catch
                {
                }

                if (e == null)
                {
                    // Could not deserialize. Let's just wrap it legibly,
                    // to help developers figure out what happened
                    e = new FunctionFailedException($"Actor operation threw {this.ExceptionType}, content = {this.Result}");
                }

                throw e;
            }
            else if (this.Result == null)
            {
                return default(T);
            }
            else
            {
                return MessagePayloadDataConverter.Default.Deserialize<T>(this.Result);
            }
        }
    }
}
