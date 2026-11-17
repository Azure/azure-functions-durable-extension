// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Primitives;
using Xunit;
#if WORKER_POLLING_TESTS
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;
using PollingContext = Microsoft.DurableTask.TaskOrchestrationContextExtensionMethods;
#else
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using PollingContext = Microsoft.Azure.WebJobs.Extensions.DurableTask.DurableOrchestrationContext;
#endif

namespace DurableTask.Tests
{
    public class PollingHeaderPolicyTests
    {
#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Theory]
        [InlineData("https://poll.example/status", true)]
        [InlineData("HTTPS://POLL.EXAMPLE:443/status", true)]
        [InlineData("https://poll.example:8443/status", false)]
        [InlineData("http://poll.example/status", false)]
        [InlineData("https://other.example/status", false)]
        [InlineData("https://poll.example.other.example/status", false)]
        public void SelectedHeadersFollowExactOrigin(string location, bool allowed)
        {
            DurableHttpRequest request = CreateRequest();
            request.CrossOriginPollingHeaders = Policy();

            DurableHttpRequest poll = PollingContext.CreateLocationPollRequest(request, location);

            Assert.NotNull(poll.Headers);
            Assert.Equal(allowed ? 1 : 0, poll.Headers.Count);
            if (allowed)
            {
                Assert.Equal(new[] { "one", "two" }, poll.Headers["X-Selected"].ToArray());
            }

            Assert.Null(poll.TokenSource);
            Assert.NotNull(request.Headers);
            Assert.Equal(6, request.Headers.Count);
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Fact]
        public void SameOriginAndConfigurationAreIndependentCopies()
        {
            DurableHttpRequest request = CreateRequest();
            IDictionary<string, string[]> policy = Policy();
            request.CrossOriginPollingHeaders = policy;
            policy["https://poll.example"][0] = "X-Other";
            request.CrossOriginPollingHeaders["https://poll.example"][0] = "X-Other";

            DurableHttpRequest poll = PollingContext.CreateLocationPollRequest(request, "/status");
            Assert.NotNull(poll.Headers);
            Assert.Equal(5, poll.Headers.Count);
            Assert.True(poll.Headers.ContainsKey("Authorization"));
            Assert.True(poll.Headers.ContainsKey("Cookie"));
            Assert.False(poll.Headers.ContainsKey("x-functions-key"));
#if !WORKER_POLLING_TESTS
            Assert.Same(request.TokenSource, poll.TokenSource);
#endif
            poll.Headers.Clear();
            Assert.NotNull(request.Headers);
            Assert.Equal(6, request.Headers.Count);

            DurableHttpRequest selected = PollingContext.CreateLocationPollRequest(request, "https://poll.example/status");
            Assert.NotNull(selected.Headers);
            Assert.Single(selected.Headers);
            Assert.Equal("one", selected.Headers["X-Selected"][0]);
            selected.Headers["X-Selected"].ToArray()[0] = "changed";
            Assert.Equal("one", request.Headers["X-Selected"][0]);
            selected.Headers["X-Selected"] = "changed";
            Assert.Equal("one", request.Headers["X-Selected"][0]);
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Theory]
        [InlineData("https://poll.example/first", 1)]
        [InlineData("https://other.example/next", 0)]
        [InlineData("https://start.example/next", 5)]
        public void RepeatedPollingUsesOriginalPolicy(string nextLocation, int expectedCount)
        {
            DurableHttpRequest request = CreateRequest();
            request.CrossOriginPollingHeaders = Policy();
            DurableHttpRequest first = PollingContext.CreateLocationPollRequest(request, "https://poll.example/first");
            DurableHttpRequest next = PollingContext.CreateLocationPollRequest(request, nextLocation);

            Assert.NotNull(first.Headers);
            Assert.NotNull(next.Headers);
            Assert.Single(first.Headers);
            Assert.Equal(expectedCount, next.Headers.Count);
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Theory]
        [InlineData("http://poll.example", "X-Selected")]
        [InlineData("https://*.example", "X-Selected")]
        [InlineData("https://poll.example/path", "X-Selected")]
        [InlineData("https://poll.example/path/..", "X-Selected")]
        [InlineData(" https://poll.example ", "X-Selected")]
        [InlineData("https://poll.example:0", "X-Selected")]
        [InlineData("https://poll.example?query=1", "X-Selected")]
        [InlineData("https://poll.example#fragment", "X-Selected")]
        [InlineData("https://user@poll.example", "X-Selected")]
        [InlineData("/relative", "X-Selected")]
        [InlineData("https://poll.example", "*")]
        [InlineData("https://poll.example", "")]
        [InlineData("https://poll.example", "invalid name")]
        [InlineData("https://poll.example", "X-Selected\r\nOther")]
        public void InvalidConfigurationIsRejected(string origin, string name)
        {
            DurableHttpRequest request = CreateRequest();
            Assert.Throws<ArgumentException>(() => request.CrossOriginPollingHeaders =
                new Dictionary<string, string[]> { { origin, new[] { name } } });
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Fact]
        public void NullListsAndDuplicateOriginsAreRejected()
        {
            DurableHttpRequest request = CreateRequest();
            Assert.Throws<ArgumentException>(() => request.CrossOriginPollingHeaders =
                new Dictionary<string, string[]> { { "https://poll.example", null! } });
            Assert.Throws<ArgumentException>(() => request.CrossOriginPollingHeaders =
                new Dictionary<string, string[]>
                {
                    { "https://poll.example", new[] { "X-Selected" } },
                    { "https://POLL.example:443/", new[] { "X-Other" } },
                });
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Theory]
        [InlineData("https://poll.example:8443", "https://poll.example:8443/status", true)]
        [InlineData("https://poll.example:8443", "https://poll.example/status", false)]
        [InlineData("https://[::1]:8443", "https://[::1]:8443/status", true)]
        public void ExplicitPortsAndIpOrigins(string origin, string location, bool allowed)
        {
            DurableHttpRequest request = CreateRequest();
            request.CrossOriginPollingHeaders = new Dictionary<string, string[]> { { origin, new[] { "X-Selected" } } };

            DurableHttpRequest poll = PollingContext.CreateLocationPollRequest(request, location);

            Assert.NotNull(poll.Headers);
            Assert.Equal(allowed ? 1 : 0, poll.Headers.Count);
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Fact]
        public void EmptyAndMissingPoliciesKeepRestrictionsWhenEnabled()
        {
            DurableHttpRequest request = CreateRequest();
            DurableHttpRequest unconfigured = PollingContext.CreateLocationPollRequest(request, "https://poll.example/status");
            Assert.NotNull(unconfigured.Headers);
            Assert.Empty(unconfigured.Headers);
            request.CrossOriginPollingHeaders = new Dictionary<string, string[]>();
            DurableHttpRequest empty = PollingContext.CreateLocationPollRequest(request, "https://poll.example/status");
            Assert.NotNull(empty.Headers);
            Assert.Empty(empty.Headers);
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Theory]
        [InlineData(false, "https://poll.example/status")]
        [InlineData(true, "https://poll.example/status")]
        [InlineData(true, "https://other.example/status")]
        [InlineData(true, "http://poll.example/status")]
        [InlineData(true, "https://poll.example:8443/status")]
        public void DisabledFlagPreservesLegacyForwarding(bool configured, string location)
        {
            DurableHttpRequest request = CreateRequest();
            request.EnableCrossOriginHeaderFiltering = false;
            if (configured)
            {
                request.CrossOriginPollingHeaders = Policy();
            }

            DurableHttpRequest poll = PollingContext.CreateLocationPollRequest(request, location);

            Assert.NotNull(poll.Headers);
            Assert.Equal(3, poll.Headers.Count);
            Assert.Equal(new[] { "one", "two" }, poll.Headers["X-Selected"].ToArray());
            Assert.Equal("other", poll.Headers["X-Other"]);
            Assert.Equal("application/json", poll.Headers["Accept"]);
            Assert.Null(poll.TokenSource);
            Assert.DoesNotContain("enableCrossOriginHeaderFiltering", Serialize(request));
            Assert.DoesNotContain("disablePollingRedirects", Serialize(poll));
            poll.Headers.Clear();
            Assert.NotNull(request.Headers);
            Assert.Equal(6, request.Headers.Count);
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
#endif
        [Fact]
        public void RequestAndPollingMetadataRoundTrip()
        {
            DurableHttpRequest request = CreateRequest();
            request.EnableCrossOriginHeaderFiltering = false;
            Assert.DoesNotContain("crossOriginPollingHeaders", Serialize(request));
            Assert.DoesNotContain("disablePollingRedirects", Serialize(request));
            Assert.DoesNotContain("enableCrossOriginHeaderFiltering", Serialize(request));
            request.EnableCrossOriginHeaderFiltering = true;
            request.CrossOriginPollingHeaders = Policy();
#if WORKER_POLLING_TESTS
            using JsonDocument document = JsonDocument.Parse(Serialize(request));
            DurableHttpRequest restored = CreateRequest();
            restored.CrossOriginPollingHeaders = document.RootElement.GetProperty("crossOriginPollingHeaders")
                .Deserialize<Dictionary<string, string[]>>();
            restored.EnableCrossOriginHeaderFiltering = document.RootElement.GetProperty("enableCrossOriginHeaderFiltering").GetBoolean();
#else
            DurableHttpRequest restored = Deserialize(Serialize(request));
#endif
            DurableHttpRequest poll = PollingContext.CreateLocationPollRequest(restored, "https://poll.example/status");
            Assert.NotNull(poll.Headers);
            Assert.Single(poll.Headers);
            Assert.DoesNotContain("disablePollingRedirects", Serialize(poll));
#if WORKER_POLLING_TESTS
            using JsonDocument pollDocument = JsonDocument.Parse(Serialize(poll));
            Assert.Equal(JsonValueKind.Null, pollDocument.RootElement.GetProperty("tokenSource").ValueKind);
#else
            DurableHttpRequest restoredPoll = Deserialize(Serialize(poll));
            Assert.NotNull(restoredPoll.Headers);
            Assert.Single(restoredPoll.Headers);
            Assert.Null(restoredPoll.TokenSource);
#endif
        }

#if !WORKER_POLLING_TESTS
        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
        [Theory]
        [InlineData("")]
        [InlineData(",\"enableCrossOriginHeaderFiltering\":false")]
        public void LegacyWireRequestsPreserveDefaults(string flag)
        {
            string json = "{\"method\":\"GET\",\"uri\":\"https://start.example/start\",\"headers\":{\"X-Other\":\"value\",\"Authorization\":\"Bearer test\",\"Cookie\":\"test=value\",\"x-functions-key\":\"test-key\"}" + flag + "}";
            DurableHttpRequest request = Deserialize(json);
            Assert.False(request.EnableCrossOriginHeaderFiltering);

            DurableHttpRequest poll = PollingContext.CreateLocationPollRequest(request, "https://poll.example/status");

            Assert.Single(poll.Headers);
            Assert.Equal("value", poll.Headers["X-Other"]);
            Assert.DoesNotContain("enableCrossOriginHeaderFiltering", Serialize(request));
            Assert.DoesNotContain("disablePollingRedirects", Serialize(poll));
        }

        [Trait("Category", Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.PlatformSpecificHelpers.TestCategory)]
        [Fact]
        public void InvalidWireConfigurationIsRejected()
        {
            const string json = "{\"method\":\"GET\",\"uri\":\"https://start.example\",\"crossOriginPollingHeaders\":{\"http://poll.example\":[\"X-Selected\"]}}";
            JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => Deserialize(json));
            Assert.IsType<ArgumentException>(exception.InnerException);
        }
#endif

        private static DurableHttpRequest CreateRequest()
        {
            var headers = new Dictionary<string, StringValues>
            {
                { "X-Selected", new StringValues(new[] { "one", "two" }) },
                { "X-Other", "other" },
                { "Accept", "application/json" },
                { "aUtHoRiZaTiOn", "Bearer test" },
                { "cOoKiE", "test=value" },
                { "X-FuNcTiOnS-kEy", "test-key" },
            };
#if WORKER_POLLING_TESTS
            return new DurableHttpRequest(HttpMethod.Post, new Uri("https://start.example/start"), headers)
            {
                EnableCrossOriginHeaderFiltering = true,
                TokenSource = new Microsoft.Azure.Functions.Worker.Extensions.DurableTask.ManagedIdentityTokenSource("https://start.example/"),
            };
#else
            return new DurableHttpRequest(
                HttpMethod.Post,
                new Uri("https://start.example/start"),
                headers,
                tokenSource: new ManagedIdentityTokenSource("https://start.example/"))
            {
                EnableCrossOriginHeaderFiltering = true,
            };
#endif
        }

        private static IDictionary<string, string[]> Policy()
        {
            return new Dictionary<string, string[]>
            {
                { "https://poll.example", new[] { "x-selected", "AUTHORIZATION", "COOKIE", "x-functions-key" } },
            };
        }

        private static string Serialize(DurableHttpRequest request)
        {
#if WORKER_POLLING_TESTS
            return JsonSerializer.Serialize(request);
#else
            return JsonConvert.SerializeObject(request);
#endif
        }

#if !WORKER_POLLING_TESTS
        private static DurableHttpRequest Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<DurableHttpRequest>(json);
        }
#endif
    }
}
