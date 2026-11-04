// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

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
        /// <param name="timeout">TimeSpan used for HTTP request timeout.</param>
        /// <param name="httpRetryOptions">Retry options used for the HTTP request.</param>
        public DurableHttpRequest(
            HttpMethod method,
            Uri uri,
            IDictionary<string, StringValues> headers = null,
            string content = null,
            ITokenSource tokenSource = null,
            bool asynchronousPatternEnabled = true,
            TimeSpan? timeout = null,
            HttpRetryOptions httpRetryOptions = null)
        {
            this.Method = method;
            this.Uri = uri;
            this.Headers = HttpHeadersConverter.CreateCopy(headers);
            this.Content = content;
            this.TokenSource = tokenSource;
            this.AsynchronousPatternEnabled = asynchronousPatternEnabled;
            this.Timeout = timeout;
            this.HttpRetryOptions = httpRetryOptions;
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

        /// <summary>
        /// Defines retry policy for handling of failures in making the HTTP Request. These could be non-successful HTTP status codes
        /// in the response, a timeout in making the HTTP call, or an exception raised from the HTTP Client library.
        /// </summary>
        [JsonProperty("retryOptions")]
        public HttpRetryOptions HttpRetryOptions { get; }

        /// <summary>
        /// The total timeout for the original HTTP request and any
        /// asynchronous polling.
        /// </summary>
        [JsonProperty("timeout")]
        public TimeSpan? Timeout { get; }

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
                        return CreateManagedIdentityTokenSource(jsonObject);
                    }

                    throw new NotSupportedException($"The token source kind '{kindValue.ToString(Formatting.None)}' is not supported.");
                }
                else if (jsonObject.TryGetValue("$type", StringComparison.Ordinal, out JToken clrTypeValue))
                {
                    ParseTypeName((string)clrTypeValue, out string assemblyName, out string typeName);
                    if (string.Equals(typeName, typeof(ManagedIdentityTokenSource).FullName, StringComparison.Ordinal))
                    {
                        return CreateManagedIdentityTokenSource(jsonObject);
                    }

                    ISerializationBinder binder = GetCustomTokenSourceBinder(serializer);
                    Type runtimeType = binder.BindToType(assemblyName, typeName);
                    if (runtimeType == null || !typeof(ITokenSource).IsAssignableFrom(runtimeType) || runtimeType.IsAbstract || runtimeType.IsInterface)
                    {
                        throw new JsonSerializationException($"Type '{typeName}' is not a supported token source.");
                    }

                    jsonObject.Remove("$type");
                    return jsonObject.ToObject(runtimeType, GetTokenSourceSerializer(serializer));
                }

                throw new NotSupportedException("The token source kind is missing.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else if (value.GetType() == typeof(ManagedIdentityTokenSource))
                {
                    var tokenSource = (ManagedIdentityTokenSource)value;
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
                    ISerializationBinder binder = GetCustomTokenSourceBinder(serializer);
                    binder.BindToName(value.GetType(), out string assemblyName, out string typeName);
                    if (string.IsNullOrWhiteSpace(typeName))
                    {
                        throw new JsonSerializationException($"The configured serialization binder did not provide a name for token source type '{value.GetType().FullName}'.");
                    }

                    string serializedTypeName = string.IsNullOrWhiteSpace(assemblyName) ? typeName : $"{typeName}, {assemblyName}";
                    JObject jsonObject = JObject.FromObject(value, GetTokenSourceSerializer(serializer));
                    jsonObject.AddFirst(new JProperty("$type", serializedTypeName));
                    jsonObject.WriteTo(writer);
                }
            }

            private static ManagedIdentityTokenSource CreateManagedIdentityTokenSource(JObject jsonObject)
            {
                string resourceString = (string)jsonObject.GetValue("resource", StringComparison.Ordinal);
                if (jsonObject.TryGetValue("options", out JToken optionsToken))
                {
                    ManagedIdentityOptions managedIdentityOptions = optionsToken.ToObject<ManagedIdentityOptions>();
                    return new ManagedIdentityTokenSource(resourceString, managedIdentityOptions);
                }

                return new ManagedIdentityTokenSource(resourceString);
            }

            private static JsonSerializer GetTokenSourceSerializer(JsonSerializer serializer)
            {
                var tokenSourceSerializer = new JsonSerializer
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
                    TypeNameHandling = TypeNameHandling.None,
                };

                foreach (var converter in serializer.Converters)
                {
                    tokenSourceSerializer.Converters.Add(converter);
                }

                return tokenSourceSerializer;
            }

            private static ISerializationBinder GetCustomTokenSourceBinder(JsonSerializer serializer)
            {
                if (serializer.SerializationBinder == null || serializer.SerializationBinder is DefaultSerializationBinder)
                {
                    throw new JsonSerializationException("Custom token sources require an explicitly configured serialization binder.");
                }

                return serializer.SerializationBinder;
            }

            private static void ParseTypeName(string serializedTypeName, out string assemblyName, out string typeName)
            {
                if (string.IsNullOrWhiteSpace(serializedTypeName))
                {
                    throw new JsonSerializationException("The token source '$type' value is missing.");
                }

                int separatorIndex = FindAssemblySeparator(serializedTypeName);
                typeName = separatorIndex < 0 ? serializedTypeName.Trim() : serializedTypeName.Substring(0, separatorIndex).Trim();
                assemblyName = separatorIndex < 0 ? null : serializedTypeName.Substring(separatorIndex + 1).Trim();
            }

            private static int FindAssemblySeparator(string serializedTypeName)
            {
                int bracketDepth = 0;
                for (int i = 0; i < serializedTypeName.Length; i++)
                {
                    if (serializedTypeName[i] == '[')
                    {
                        bracketDepth++;
                    }
                    else if (serializedTypeName[i] == ']')
                    {
                        bracketDepth--;
                    }
                    else if (serializedTypeName[i] == ',' && bracketDepth == 0)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }
    }
}
