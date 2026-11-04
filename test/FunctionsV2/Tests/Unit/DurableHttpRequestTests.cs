// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableHttpRequestTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_RoundTripsManagedIdentity()
        {
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("https://example.com"),
                tokenSource: new ManagedIdentityTokenSource("https://management.core.windows.net/.default"));

            string json = JsonConvert.SerializeObject(request);
            DurableHttpRequest result = JsonConvert.DeserializeObject<DurableHttpRequest>(json);

            Assert.DoesNotContain("$type", json);
            ManagedIdentityTokenSource tokenSource = Assert.IsType<ManagedIdentityTokenSource>(result.TokenSource);
            Assert.Equal("https://management.core.windows.net/.default", tokenSource.Resource);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_DeserializesLegacyManagedIdentity()
        {
            JObject json = CreateRequestJson(
                new JObject
                {
                    ["$type"] = typeof(ManagedIdentityTokenSource).AssemblyQualifiedName,
                    ["resource"] = "https://management.core.windows.net/.default",
                });

            DurableHttpRequest result = json.ToObject<DurableHttpRequest>();

            ManagedIdentityTokenSource tokenSource = Assert.IsType<ManagedIdentityTokenSource>(result.TokenSource);
            Assert.Equal("https://management.core.windows.net/.default", tokenSource.Resource);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_DeserializesV3140ManagedIdentityHistory()
        {
            JObject json = CreateRequestJson(
                new JObject
                {
                    ["kind"] = "AzureManagedIdentity",
                    ["resource"] = "https://management.core.windows.net/.default",
                    ["options"] = new JObject
                    {
                        ["tenantid"] = "tenant",
                    },
                });

            DurableHttpRequest result = json.ToObject<DurableHttpRequest>();

            ManagedIdentityTokenSource tokenSource = Assert.IsType<ManagedIdentityTokenSource>(result.TokenSource);
            Assert.Equal("https://management.core.windows.net/.default", tokenSource.Resource);
            Assert.Equal("tenant", tokenSource.Options.TenantId);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_RoundTripsCustomImplementation()
        {
            JsonSerializerSettings settings = CreateCustomTokenSourceSettings();
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("https://example.com"),
                tokenSource: new CustomTokenSource
                {
                    Token = "token",
                    Options = new ManagedIdentityOptions { TenantId = "tenant" },
                });

            string json = JsonConvert.SerializeObject(request, settings);
            DurableHttpRequest result = JsonConvert.DeserializeObject<DurableHttpRequest>(json, settings);

            CustomTokenSource tokenSource = Assert.IsType<CustomTokenSource>(result.TokenSource);
            Assert.Equal("token", tokenSource.Token);
            Assert.Equal("tenant", tokenSource.Options.TenantId);
            JObject tokenSourceJson = (JObject)JObject.Parse(json)["tokenSource"];
            Assert.NotNull(tokenSourceJson["$type"]);
            Assert.Null(tokenSourceJson["Options"]["$type"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_DeserializesV3140CustomHistoryWithConfiguredBinder()
        {
            ManagedIdentityOptionsProbe.WasCreated = false;
            JObject json = CreateV3140CustomTokenSourceHistory();
            JsonSerializer serializer = JsonSerializer.Create(CreateCustomTokenSourceSettings());

            DurableHttpRequest result = json.ToObject<DurableHttpRequest>(serializer);

            CustomTokenSource tokenSource = Assert.IsType<CustomTokenSource>(result.TokenSource);
            Assert.Equal("token", tokenSource.Token);
            Assert.IsType<ManagedIdentityOptions>(tokenSource.Options);
            Assert.Equal("tenant", tokenSource.Options.TenantId);
            Assert.False(ManagedIdentityOptionsProbe.WasCreated);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_RejectsV3140CustomHistoryWithoutBinder()
        {
            ManagedIdentityOptionsProbe.WasCreated = false;
            JObject json = CreateV3140CustomTokenSourceHistory();
            JsonSerializer serializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Objects,
                });

            Assert.Throws<JsonSerializationException>(() => json.ToObject<DurableHttpRequest>(serializer));
            Assert.False(ManagedIdentityOptionsProbe.WasCreated);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_RejectsTypeThatDoesNotImplementInterface()
        {
            JObject json = CreateRequestJson(
                new JObject
                {
                    ["$type"] = typeof(UnsupportedType).AssemblyQualifiedName,
                });

            Assert.Throws<JsonSerializationException>(() => json.ToObject<DurableHttpRequest>());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_RejectsCustomImplementationWithoutBinder()
        {
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("https://example.com"),
                tokenSource: new CustomTokenSource());

            Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(request));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_RequiresBinderForDerivedManagedIdentity()
        {
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("https://example.com"),
                tokenSource: new DerivedManagedIdentityTokenSource());

            Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(request));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TokenSource_RoundTripsGenericCustomImplementation()
        {
            JsonSerializerSettings settings = CreateCustomTokenSourceSettings();
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("https://example.com"),
                tokenSource: new GenericCustomTokenSource<ManagedIdentityOptions>
                {
                    Value = new ManagedIdentityOptions { TenantId = "tenant" },
                });

            string json = JsonConvert.SerializeObject(request, settings);
            DurableHttpRequest result = JsonConvert.DeserializeObject<DurableHttpRequest>(json, settings);

            var tokenSource = Assert.IsType<GenericCustomTokenSource<ManagedIdentityOptions>>(result.TokenSource);
            Assert.Equal("tenant", tokenSource.Value.TenantId);
        }

        private static JObject CreateRequestJson(JObject tokenSource)
        {
            return new JObject
            {
                ["method"] = "GET",
                ["uri"] = "https://example.com",
                ["tokenSource"] = tokenSource,
            };
        }

        private static JObject CreateV3140CustomTokenSourceHistory()
        {
            return CreateRequestJson(
                new JObject
                {
                    ["$type"] = typeof(CustomTokenSource).AssemblyQualifiedName,
                    ["Token"] = "token",
                    ["Options"] = new JObject
                    {
                        ["$type"] = typeof(ManagedIdentityOptionsProbe).AssemblyQualifiedName,
                        ["TenantId"] = "tenant",
                    },
                });
        }

        private static JsonSerializerSettings CreateCustomTokenSourceSettings()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomTokenSourceBinder(),
            };
        }

        private class CustomTokenSource : ITokenSource
        {
            public string Token { get; set; }

            public ManagedIdentityOptions Options { get; set; }

            public Task<string> GetTokenAsync()
            {
                return Task.FromResult(this.Token);
            }
        }

        private class ManagedIdentityOptionsProbe : ManagedIdentityOptions
        {
            public ManagedIdentityOptionsProbe()
            {
                WasCreated = true;
            }

            public static bool WasCreated { get; set; }
        }

        private class DerivedManagedIdentityTokenSource : ManagedIdentityTokenSource
        {
            public DerivedManagedIdentityTokenSource()
                : base("https://management.core.windows.net/.default")
            {
            }
        }

        private class GenericCustomTokenSource<T> : ITokenSource
        {
            public T Value { get; set; }

            public Task<string> GetTokenAsync()
            {
                return Task.FromResult(string.Empty);
            }
        }

        private class CustomTokenSourceBinder : ISerializationBinder
        {
            public Type BindToType(string assemblyName, string typeName)
            {
                if (typeName == typeof(CustomTokenSource).FullName)
                {
                    return typeof(CustomTokenSource);
                }

                if (typeName == typeof(DurableHttpRequest).FullName)
                {
                    return typeof(DurableHttpRequest);
                }

                if (typeName == typeof(GenericCustomTokenSource<ManagedIdentityOptions>).FullName)
                {
                    return typeof(GenericCustomTokenSource<ManagedIdentityOptions>);
                }

                throw new JsonSerializationException($"Type '{typeName}' is not allowed.");
            }

            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                if (serializedType != typeof(CustomTokenSource) &&
                    serializedType != typeof(DurableHttpRequest) &&
                    serializedType != typeof(GenericCustomTokenSource<ManagedIdentityOptions>))
                {
                    throw new JsonSerializationException($"Type '{serializedType.FullName}' is not allowed.");
                }

                assemblyName = serializedType.Assembly.FullName;
                typeName = serializedType.FullName;
            }
        }

        private class UnsupportedType
        {
        }
    }
}
