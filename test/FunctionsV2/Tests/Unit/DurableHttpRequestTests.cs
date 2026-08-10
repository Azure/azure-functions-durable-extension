// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableHttpRequestTests
    {
        [Fact]
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
        public void TokenSource_RoundTripsCustomImplementation()
        {
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("https://example.com"),
                tokenSource: new CustomTokenSource
                {
                    Token = "token",
                    Options = new ManagedIdentityOptions { TenantId = "tenant" },
                });

            string json = JsonConvert.SerializeObject(request);
            DurableHttpRequest result = JsonConvert.DeserializeObject<DurableHttpRequest>(json);

            CustomTokenSource tokenSource = Assert.IsType<CustomTokenSource>(result.TokenSource);
            Assert.Equal("token", tokenSource.Token);
            Assert.Equal("tenant", tokenSource.Options.TenantId);
            Assert.Equal(1, json.Split(new[] { "\"$type\"" }, StringSplitOptions.None).Length - 1);
        }

        [Fact]
        public void TokenSource_DeserializesLegacyCustomImplementationWithNestedState()
        {
            JObject json = CreateRequestJson(
                new JObject
                {
                    ["$type"] = typeof(CustomTokenSource).AssemblyQualifiedName,
                    ["Token"] = "token",
                    ["Options"] = new JObject
                    {
                        ["$type"] = typeof(ManagedIdentityOptions).AssemblyQualifiedName,
                        ["TenantId"] = "tenant",
                    },
                });

            DurableHttpRequest result = json.ToObject<DurableHttpRequest>();

            CustomTokenSource tokenSource = Assert.IsType<CustomTokenSource>(result.TokenSource);
            Assert.Equal("token", tokenSource.Token);
            Assert.Equal("tenant", tokenSource.Options.TenantId);
        }

        [Fact]
        public void TokenSource_RejectsTypeThatDoesNotImplementInterface()
        {
            JObject json = CreateRequestJson(
                new JObject
                {
                    ["$type"] = typeof(UnsupportedType).AssemblyQualifiedName,
                });

            Assert.Throws<JsonSerializationException>(() => json.ToObject<DurableHttpRequest>());
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

        private class CustomTokenSource : ITokenSource
        {
            public string Token { get; set; }

            public ManagedIdentityOptions Options { get; set; }

            public Task<string> GetTokenAsync()
            {
                return Task.FromResult(this.Token);
            }
        }

        private class UnsupportedType
        {
        }
    }
}
