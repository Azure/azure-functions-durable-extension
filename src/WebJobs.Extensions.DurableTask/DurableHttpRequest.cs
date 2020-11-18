// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Request used to make an HTTP call through Durable Functions.
    /// </summary>
    public class DurableHttpRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableHttpRequest"/> class.
        /// </summary>
        /// <param name="method">Method used for HTTP request.</param>
        /// <param name="uri">Uri used to make the HTTP request.</param>
        /// <param name="headers">Headers added to the HTTP request.</param>
        /// <param name="content">Content added to the body of the HTTP request.</param>
        /// <param name="tokenSource">AAD authentication attached to the HTTP request.</param>
        /// <param name="asynchronousPatternEnabled">Specifies whether the DurableHttpRequest should handle the asynchronous pattern.</param>
        public DurableHttpRequest(
            HttpMethod method,
            Uri uri,
            IDictionary<string, StringValues> headers = null,
            string content = null,
            ITokenSource tokenSource = null,
            bool asynchronousPatternEnabled = true)
        {
            this.Method = method;
            this.Uri = uri;
            this.Headers = HttpHeadersConverter.CreateCopy(headers);
            this.Content = content;
            this.TokenSource = tokenSource;
            this.AsynchronousPatternEnabled = asynchronousPatternEnabled;
        }

        /// <summary>
        /// HttpMethod used in the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("method")]
        [JsonConverter(typeof(HttpMethodConverter))]
        public HttpMethod Method { get; }

        /// <summary>
        /// Uri used in the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("uri")]
        public Uri Uri { get; }

        /// <summary>
        /// Headers passed with the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("headers")]
        [JsonConverter(typeof(HttpHeadersConverter))]
        public IDictionary<string, StringValues> Headers { get; }

        /// <summary>
        /// Content passed with the HTTP request made by the Durable Function.
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; }

        /// <summary>
        /// Mechanism for attaching an OAuth token to the request.
        /// </summary>
        [JsonProperty("tokenSource")]
        [JsonConverter(typeof(TokenSourceConverter))]
        public ITokenSource TokenSource { get; }

        /// <summary>
        /// Specifies whether the Durable HTTP APIs should automatically
        /// handle the asynchronous HTTP pattern.
        /// </summary>
        [JsonProperty("asynchronousPatternEnabled")]
        public bool AsynchronousPatternEnabled { get; }

        private class HttpMethodConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(HttpMethod);
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    return new HttpMethod((string)JToken.Load(reader));
                }

                // Default for JSON that's either missing or not understood
                return HttpMethod.Get;
            }

            public override void WriteJson(
                JsonWriter writer,
                object value,
                JsonSerializer serializer)
            {
                HttpMethod method = (HttpMethod)value ?? HttpMethod.Get;
                writer.WriteValue(method.ToString());
            }
        }

        private class TokenSourceConverter : JsonConverter
        {
            private static JsonSerializer tokenSerializer;

            private enum TokenSourceType
            {
                None = 0,
                AzureManagedIdentity = 1,
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType is ITokenSource;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var safeTokenSerializer = GetTokenSourceSerializer(serializer);

                JToken json = JToken.ReadFrom(reader);
                if (json.Type == JTokenType.Null)
                {
                    return null;
                }

                JObject jsonObject = (JObject)json;
                if (jsonObject.TryGetValue("kind", out JToken kindValue))
                {
                    if (Enum.TryParse((string)kindValue, out TokenSourceType tokenSourceKind) &&
                        tokenSourceKind == TokenSourceType.AzureManagedIdentity)
                    {
                        string resourceString = (string)jsonObject.GetValue("resource", StringComparison.Ordinal);

                        if (jsonObject.TryGetValue("options", out JToken optionsToken))
                        {
                            ManagedIdentityOptions managedIdentityOptions = optionsToken.ToObject<JObject>().ToObject<ManagedIdentityOptions>();
                            return new ManagedIdentityTokenSource(resourceString, managedIdentityOptions);
                        }

                        return new ManagedIdentityTokenSource(resourceString);
                    }

                    throw new NotSupportedException($"The token source kind '{kindValue.ToString(Formatting.None)}' is not supported.");
                }
                else if (jsonObject.TryGetValue("$type", StringComparison.Ordinal, out JToken clrTypeValue))
                {
                    Type runtimeType = Type.GetType((string)clrTypeValue, throwOnError: true);
                    return jsonObject.ToObject(runtimeType, safeTokenSerializer);
                }
                else
                {
                    // Don't know how to deserialize this - use default behavior (this may fail)
                    return jsonObject.ToObject(objectType);
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is ManagedIdentityTokenSource tokenSource)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("kind");
                    writer.WriteValue(TokenSourceType.AzureManagedIdentity.ToString());
                    writer.WritePropertyName("resource");
                    writer.WriteValue(tokenSource.Resource);

                    if (tokenSource.Options != null)
                    {
                        writer.WritePropertyName("options");
                        writer.WriteRawValue(JsonConvert.SerializeObject(tokenSource.Options));
                    }

                    writer.WriteEndObject();
                }
                else
                {
                    // Don't know how to serialize this - use default behavior, forcing TypeNameHandling.Objects to correctly serialize ITokenSource
                    var safeTokenSerializer = GetTokenSourceSerializer(serializer);
                    safeTokenSerializer.Serialize(writer, value);
                }
            }

            private static JsonSerializer GetTokenSourceSerializer(JsonSerializer serializer)
            {
                if (tokenSerializer != null)
                {
                    return tokenSerializer;
                }

                if (serializer.TypeNameHandling == TypeNameHandling.Objects
                    || serializer.TypeNameHandling == TypeNameHandling.All)
                {
                    tokenSerializer = serializer;
                    return tokenSerializer;
                }

                // Make sure these are all the settings when updating Newtonsoft.Json
                tokenSerializer = new JsonSerializer
                {
                    Context = serializer.Context,
                    Culture = serializer.Culture,
                    ContractResolver = serializer.ContractResolver,
                    ConstructorHandling = serializer.ConstructorHandling,
                    CheckAdditionalContent = serializer.CheckAdditionalContent,
                    DateFormatHandling = serializer.DateFormatHandling,
                    DateFormatString = serializer.DateFormatString,
                    DateParseHandling = serializer.DateParseHandling,
                    DateTimeZoneHandling = serializer.DateTimeZoneHandling,
                    DefaultValueHandling = serializer.DefaultValueHandling,
                    EqualityComparer = serializer.EqualityComparer,
                    FloatFormatHandling = serializer.FloatFormatHandling,
                    Formatting = serializer.Formatting,
                    FloatParseHandling = serializer.FloatParseHandling,
                    MaxDepth = serializer.MaxDepth,
                    MetadataPropertyHandling = serializer.MetadataPropertyHandling,
                    MissingMemberHandling = serializer.MissingMemberHandling,
                    NullValueHandling = serializer.NullValueHandling,
                    ObjectCreationHandling = serializer.ObjectCreationHandling,
                    PreserveReferencesHandling = serializer.PreserveReferencesHandling,
                    ReferenceResolver = serializer.ReferenceResolver,
                    ReferenceLoopHandling = serializer.ReferenceLoopHandling,
                    StringEscapeHandling = serializer.StringEscapeHandling,
                    TraceWriter = serializer.TraceWriter,

                    // Enforcing TypeNameHandling.Objects to make sure ITokenSource gets serialized/deserialized correctly
                    TypeNameHandling = TypeNameHandling.Objects,
                };

                foreach (var converter in serializer.Converters)
                {
                    tokenSerializer.Converters.Add(converter);
                }

                return tokenSerializer;
            }
        }
    }
}
