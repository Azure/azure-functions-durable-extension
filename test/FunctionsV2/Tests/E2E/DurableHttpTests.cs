// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Primitives;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class DurableHttpTests : IDisposable
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;

        private static readonly IMessageSerializerSettingsFactory MockTokenSourceSerializerSettings =
            new MockTokenSourceSerializerSettingsFactory();

        private static int mockSynchronousHttpMessageHandlerCount;

        public DurableHttpTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        public void Dispose()
        {
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DeserializeCallActivity()
        {
            // {
            //   "method": "POST",
            //   "uri": "https://example.com",
            //   "headers": {
            //     "Content-Type": "application/json",
            //     "Accept": [
            //       "application/json",
            //       "application/xml"
            //     ],
            //     "x-ms-foo": []
            //   },
            //   "content": "5"
            // }
            var json = new JObject(
                new JProperty("method", "POST"),
                new JProperty("uri", "https://example.com"),
                new JProperty("headers", new JObject(
                    new JProperty("Content-Type", "application/json"),
                    new JProperty("Accept", new JArray(
                        "application/json",
                        "application/xml")),
                    new JProperty("x-ms-foo", new JArray()))),
                new JProperty("content", "5"));

            DurableHttpRequest request = JsonConvert.DeserializeObject<DurableHttpRequest>(json.ToString());
            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://example.com"), request.Uri);
            Assert.Equal("5", request.Content);
            Assert.Equal(3, request.Headers.Count);

            Assert.True(request.Headers.TryGetValue("Content-Type", out StringValues contentTypeValues));
            Assert.Single(contentTypeValues);
            Assert.Equal("application/json", contentTypeValues[0]);

            Assert.True(request.Headers.TryGetValue("Accept", out StringValues acceptValues));
            Assert.Equal(2, acceptValues.Count);
            Assert.Equal("application/json", acceptValues[0]);
            Assert.Equal("application/xml", acceptValues[1]);

            Assert.True(request.Headers.TryGetValue("x-ms-foo", out StringValues customHeaderValues));
            Assert.Equal(0, customHeaderValues.Count);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void SerializeManagedIdentityOptions()
        {
            // Part 1: Check if ManagedIdentityOptions is correctly serialized with TestDurableHttpRequest
            var expectedTestDurableHttpRequestJson = @"
{
  ""HttpMethod"": {
    ""Method"": ""GET""
  },
  ""Uri"": ""https://www.dummy-url.com"",
  ""Headers"": {
    ""Accept"": ""application/json""
  },
  ""Content"": null,
  ""TokenSource"": {
    ""$type"": ""Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.DurableHttpTests+MockTokenSource, WebJobs.Extensions.DurableTask.Tests." + PlatformSpecificHelpers.VersionSuffix + @""",
    ""testToken"": ""dummy token"",
    ""options"": {
      ""authorityhost"": ""https://dummy.login.microsoftonline.com/"",
      ""tenantid"": ""tenant_id"",
      ""clientid"": null
    }
  },
  ""AsynchronousPatternEnabled"": true
}";

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Accept", "application/json");

            ManagedIdentityOptions options = new ManagedIdentityOptions();
            options.AuthorityHost = new Uri("https://dummy.login.microsoftonline.com/");
            options.TenantId = "tenant_id";

            MockTokenSource mockTokenSource = new MockTokenSource("dummy token", options);

            TestDurableHttpRequest request = new TestDurableHttpRequest(
                httpMethod: HttpMethod.Get,
                headers: headers,
                tokenSource: mockTokenSource);

            string serializedTestDurableHttpRequest = JsonConvert.SerializeObject(request);

            Assert.True(JToken.DeepEquals(JObject.Parse(expectedTestDurableHttpRequestJson), JObject.Parse(serializedTestDurableHttpRequest)));

            // Part 2: Check if ManagedIdentityOptions is correctly serialized with DurableHttpRequest
            var expectedDurableHttpRequestJson = @"
{
  ""method"": ""GET"",
  ""uri"": ""https://www.dummy-url.com"",
  ""headers"": {
    ""Accept"": ""application/json""
  },
  ""content"": null,
  ""tokenSource"": {
    ""kind"": ""AzureManagedIdentity"",
    ""resource"": ""dummy url"",
    ""options"": {
      ""authorityhost"": ""https://dummy.login.microsoftonline.com/"",
      ""tenantid"": ""tenant_id"",
      ""clientid"": null
    }
   },
  ""asynchronousPatternEnabled"": true,
  ""retryOptions"": null,
  ""timeout"": null
}";
            ManagedIdentityTokenSource managedIdentityTokenSource = new ManagedIdentityTokenSource("dummy url", options);
            TestDurableHttpRequest testDurableHttpRequest = new TestDurableHttpRequest(
                httpMethod: HttpMethod.Get,
                headers: headers,
                tokenSource: managedIdentityTokenSource);

            DurableHttpRequest durableHttpRequest = TestOrchestrations.ConvertTestRequestToDurableHttpRequest(testDurableHttpRequest);
            string serializedDurableHttpRequest = JsonConvert.SerializeObject(durableHttpRequest);

            Assert.True(JToken.DeepEquals(JObject.Parse(expectedDurableHttpRequestJson), JObject.Parse(serializedDurableHttpRequest)));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void SerializeDurableHttpRequestWithoutManagedIdentityOptions()
        {
            var expectedDurableHttpRequestJson = @"
{
  ""method"": ""GET"",
  ""uri"": ""https://www.dummy-url.com"",
  ""headers"": {
    ""Accept"": ""application/json""
  },
  ""content"": null,
  ""tokenSource"": {
    ""kind"": ""AzureManagedIdentity"",
    ""resource"": ""dummy url""
  },
  ""asynchronousPatternEnabled"": true,
  ""retryOptions"": null,
  ""timeout"": null
}";

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Accept", "application/json");

            ManagedIdentityTokenSource managedIdentityTokenSource = new ManagedIdentityTokenSource("dummy url");
            TestDurableHttpRequest testDurableHttpRequest = new TestDurableHttpRequest(
                httpMethod: HttpMethod.Get,
                headers: headers,
                tokenSource: managedIdentityTokenSource);

            DurableHttpRequest durableHttpRequest = TestOrchestrations.ConvertTestRequestToDurableHttpRequest(testDurableHttpRequest);
            string serializedDurableHttpRequest = JsonConvert.SerializeObject(durableHttpRequest);

            Assert.True(JToken.DeepEquals(JObject.Parse(expectedDurableHttpRequestJson), JObject.Parse(serializedDurableHttpRequest)));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DeserializeManagedIdentityOptions()
        {
            // Part 1: Check if ManagedIdentityOptions is correctly serialized with TestDurableHttpRequest
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Accept", "application/json");

            ManagedIdentityOptions options = new ManagedIdentityOptions();
            options.AuthorityHost = new Uri("https://dummy.login.microsoftonline.com/");
            options.TenantId = "tenant_id";

            MockTokenSource mockTokenSource = new MockTokenSource("dummy token", options);

            TestDurableHttpRequest request = new TestDurableHttpRequest(
                httpMethod: HttpMethod.Get,
                headers: headers,
                tokenSource: mockTokenSource);

            string serializedTestDurableHttpRequest = JsonConvert.SerializeObject(request);
            TestDurableHttpRequest deserializedTestDurableHttpRequest = JsonConvert.DeserializeObject<TestDurableHttpRequest>(serializedTestDurableHttpRequest);

            MockTokenSource deserializedMockTokenSource = deserializedTestDurableHttpRequest.TokenSource as MockTokenSource;
            Assert.Equal("https://dummy.login.microsoftonline.com/", deserializedMockTokenSource.GetOptions().AuthorityHost.ToString());
            Assert.Equal("tenant_id", deserializedMockTokenSource.GetOptions().TenantId);

            // Part 2: Check if ManagedIdentityOptions is correctly serialized with DurableHttpRequest
            ManagedIdentityTokenSource managedIdentityTokenSource = new ManagedIdentityTokenSource("dummy url", options);
            TestDurableHttpRequest testDurableHttpRequest = new TestDurableHttpRequest(
                httpMethod: HttpMethod.Get,
                headers: headers,
                tokenSource: managedIdentityTokenSource);

            DurableHttpRequest durableHttpRequest = TestOrchestrations.ConvertTestRequestToDurableHttpRequest(testDurableHttpRequest);
            string serializedDurableHttpRequest = JsonConvert.SerializeObject(durableHttpRequest);
            DurableHttpRequest deserializedDurableHttpRequest = JsonConvert.DeserializeObject<DurableHttpRequest>(serializedDurableHttpRequest);

            ManagedIdentityTokenSource deserializedManagedIdentityTokenSource = deserializedDurableHttpRequest.TokenSource as ManagedIdentityTokenSource;
            Assert.Equal("https://dummy.login.microsoftonline.com/", deserializedManagedIdentityTokenSource.Options.AuthorityHost.ToString());
            Assert.Equal("tenant_id", deserializedManagedIdentityTokenSource.Options.TenantId);
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_SynchronousAPI_Returns200(string storageProvider)
        {
            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandler(testHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_SynchronousAPI_Returns200),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test that DurableHttpRequest serialized by Worker.Extensions.DurableTask can be correctly deserialized by WebJobs.Extensions.DurableTask.
        /// This validates cross-extension compatibility for the token credential feature.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DeserializeWorkerDurableHttpRequestCorrectly()
        {
            // Raw input from Worker.Extensions.DurableTask with options
            string rawInputFromWorkerExtensions = @"{""method"":""GET"",""uri"":""https://httpbin.org/get"",""headers"":null,""content"":null,""tokenSource"":{""kind"":""AzureManagedIdentity"",""resource"":""https://graph.microsoft.com/.default"",""options"":{""authorityhost"":""https://login.microsoftonline.com/"",""tenantid"":""test-tenant-id""}},""asynchronousPatternEnabled"":false,""retryOptions"":null,""timeout"":null}";

            // Deserialize the raw input directly to DurableHttpRequest
            DurableHttpRequest durableHttpRequest = JsonConvert.DeserializeObject<DurableHttpRequest>(rawInputFromWorkerExtensions);

            // Validate the deserialized DurableHttpRequest
            Assert.NotNull(durableHttpRequest);
            Assert.Equal(HttpMethod.Get, durableHttpRequest.Method);
            Assert.Equal(new Uri("https://httpbin.org/get"), durableHttpRequest.Uri);
            Assert.NotNull(durableHttpRequest.Headers); // Headers should be an empty list
            Assert.Empty(durableHttpRequest.Headers);
            Assert.Null(durableHttpRequest.Content);
            Assert.False(durableHttpRequest.AsynchronousPatternEnabled);
            Assert.Null(durableHttpRequest.HttpRetryOptions);
            Assert.Null(durableHttpRequest.Timeout);

            // Validate the TokenSource was correctly deserialized with options
            Assert.NotNull(durableHttpRequest.TokenSource);
            Assert.IsType<ManagedIdentityTokenSource>(durableHttpRequest.TokenSource);

            ManagedIdentityTokenSource tokenSource = durableHttpRequest.TokenSource as ManagedIdentityTokenSource;
            Assert.Equal("https://graph.microsoft.com/.default", tokenSource.Resource);
            Assert.NotNull(tokenSource.Options);
            Assert.Equal(new Uri("https://login.microsoftonline.com/"), tokenSource.Options.AuthorityHost);
            Assert.Equal("test-tenant-id", tokenSource.Options.TenantId);
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code
        /// when a DurableHttpRequest timeout value is set and the request completes within the timeout.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_TimeoutNotReached(string storageProvider)
        {
            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerWithTimeout(testHttpResponseMessage, TimeSpan.FromMilliseconds(2000));

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_TimeoutNotReached),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    timeout: TimeSpan.FromMilliseconds(5000));

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator fails  when the
        /// HTTP request times out and the CallHttpAsync API throws a TimeoutException.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_TimeoutException(string storageProvider)
        {
            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerWithTimeoutException(TimeSpan.FromMilliseconds(2000));

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_TimeoutException),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    timeout: TimeSpan.FromMilliseconds(1000));

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                Assert.Contains("Orchestrator function 'CallHttpAsyncOrchestrator' failed: The operation was canceled. Reached user specified timeout: 00:00:01", output.ToString());
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator retries when the
        /// HTTP request times out and the CallHttpAsync API throws a TimeoutException.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_RetryExceededWithHttpException(string storageProvider)
        {
            mockSynchronousHttpMessageHandlerCount = 0;

            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerWithHttpRequestException();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_RetryExceededWithHttpException),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Accept", "application/json" },
                };
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    firstRetryInterval: TimeSpan.FromSeconds(1),
                    maxNumberOfAttempts: 3);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                Assert.Equal(3, mockSynchronousHttpMessageHandlerCount);
                Assert.Contains("No such host is known.", output.ToString());
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator retries when the
        /// HTTP request times out and the CallHttpAsync API throws a TimeoutException.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_RetryExceededWithHttpNotFoundStatusCode(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerWithHttp404();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_RetryExceededWithHttpNotFoundStatusCode),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Accept", "application/json" },
                };
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    firstRetryInterval: TimeSpan.FromSeconds(1),
                    maxNumberOfAttempts: 3)
                {
                    StatusCodesToRetry = new List<HttpStatusCode> { HttpStatusCode.NotFound },
                };

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                Assert.Equal(3, mockSynchronousHttpMessageHandlerCount);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator retries when the
        /// HTTP request times out and the CallHttpAsync API throws a TimeoutException.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_RetryExceededWithHttpNotFoundViaEnsureSuccessException(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerWithHttp404();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_RetryExceededWithHttpNotFoundViaEnsureSuccessException),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Accept", "application/json" },
                };
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    firstRetryInterval: TimeSpan.FromSeconds(1),
                    maxNumberOfAttempts: 3)
                {
                    StatusCodesToRetry = new List<HttpStatusCode> { HttpStatusCode.NotFound },
                };

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                Assert.Equal(3, mockSynchronousHttpMessageHandlerCount);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator retries when the
        /// HTTP request times out and the CallHttpAsync API throws a TimeoutException.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_SuccessWithNoRetryWithHttpNotFoundIfNotSpecified(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerWithHttp404();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_SuccessWithNoRetryWithHttpNotFoundIfNotSpecified),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Accept", "application/json" },
                };
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    firstRetryInterval: TimeSpan.FromSeconds(1),
                    maxNumberOfAttempts: 3)
                {
                    StatusCodesToRetry = new List<HttpStatusCode> { HttpStatusCode.Forbidden },
                };

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.Equal(1, mockSynchronousHttpMessageHandlerCount);

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator fails  when the
        /// target url doesn't exist and throws an HttpRequestException.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_HttpRequestException(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerWithHttpRequestException();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_HttpRequestException),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                Assert.Equal(1, mockSynchronousHttpMessageHandlerCount);
                Assert.Contains("No such host is known.", output.ToString());
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the UserAgent header is set in the HttpResponseMessage.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_CheckUserAgentHeader(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockHttpMessageHandlerCheckUserAgent();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_CheckUserAgentHeader),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the UserAgent header is set in the HttpResponseMessage.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_CheckRequestAcceptHeaders(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockHttpMessageHandlerCheckAcceptHeader();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_CheckRequestAcceptHeaders),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an Accepted (202)
        /// when the asynchronous pattern is disabled.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_AsynchronousPatternDisabled(string storageProvider)
        {
            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Accepted);
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandler(testHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_AsynchronousPatternDisabled),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);
                testRequest.AsynchronousPatternEnabled = false;

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns a Not Found (404) status code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_SynchronousAPI_ReturnsNotFound(string storageProvider)
        {
            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.NotFound);
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandler(testHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_SynchronousAPI_ReturnsNotFound),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(40));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Headers and Content.
        /// from the response have relevant information. This test has multiple response
        /// header values.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_MultipleHeadersAndContentTest(string storageProvider)
        {
            string[] httpResponseHeaders = { "test.host.com", "test.response.com" };
            StringValues stringValues = new StringValues(httpResponseHeaders);
            Dictionary<string, StringValues> testHeaders = new Dictionary<string, StringValues>();
            testHeaders.Add("Host", stringValues);

            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessageMultHeaders(
                                                                                        statusCode: HttpStatusCode.OK,
                                                                                        headers: testHeaders,
                                                                                        content: "test content");

            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandler(testHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_MultipleHeadersAndContentTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                // Uri uri = new Uri("https://dummy-test-url.com");
                // var request = new DurableHttpRequest(HttpMethod.Get, uri);
                // StringValues stringValues = new StringValues("application/json");
                // request.Headers.Add("Accept", stringValues);

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);

                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                var hostHeaders = response.Headers["Host"];
                bool hasHostValueOne = response.Headers["Host"].Contains("test.host.com");
                bool hasHostValueTwo = response.Headers["Host"].Contains("test.response.com");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(hasHostValueOne && hasHostValueTwo);
                Assert.Contains("test content", response.Content);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Headers and Content.
        /// from the response have relevant information. This test has multiple response
        /// headers with varying amount of header values.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_MultipleHeaderValuesTest(string storageProvider)
        {
            Dictionary<string, StringValues> testHeaders = new Dictionary<string, StringValues>();

            string[] httpResponseHeaders = { "test.host.com", "test.response.com" };
            StringValues stringValues = new StringValues(httpResponseHeaders);
            testHeaders.Add("Host", stringValues);

            string[] cacheResponseHeaders = { "GET", "POST", "HEAD", "OPTIONS" };
            StringValues cacheStringValues = new StringValues(cacheResponseHeaders);
            testHeaders.Add("Cache-Control", cacheStringValues);

            string[] accessControlHeaders = { "X-customHeader1", "X-customHeader2", "X-customHeader3", "X-customHeader4", "X-customHeader5" };
            StringValues accessControlStringValues = new StringValues(accessControlHeaders);
            testHeaders.Add("Access-Control-Expose-Headers", accessControlStringValues);

            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessageMultHeaders(
                statusCode: HttpStatusCode.OK,
                headers: testHeaders,
                content: "test content");

            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandler(testHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_MultipleHeaderValuesTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);

                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                var hostHeaders = response.Headers["Host"];
                bool hasHostValueOne = hostHeaders.Contains("test.host.com");
                bool hasHostValueTwo = hostHeaders.Contains("test.response.com");

                var cacheHeaders = response.Headers["Cache-Control"].First();
                bool hasCacheValueOne = cacheHeaders.Contains("GET");
                bool hasCacheValueTwo = cacheHeaders.Contains("POST");
                bool hasCacheValueThree = cacheHeaders.Contains("HEAD");
                bool hasCacheValueFour = cacheHeaders.Contains("OPTIONS");

                var accessHeaders = response.Headers["Access-Control-Expose-Headers"];
                bool hasAccessValueOne = accessHeaders.Contains("X-customHeader1");
                bool hasAccessValueTwo = accessHeaders.Contains("X-customHeader2");
                bool hasAccessValueThree = accessHeaders.Contains("X-customHeader3");
                bool hasAccessValueFour = accessHeaders.Contains("X-customHeader4");
                bool hasAccessValueFive = accessHeaders.Contains("X-customHeader5");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(hasHostValueOne && hasHostValueTwo);
                Assert.True(hasCacheValueOne && hasCacheValueTwo && hasCacheValueThree && hasCacheValueFour);
                Assert.True(hasAccessValueOne && hasAccessValueTwo && hasAccessValueThree && hasAccessValueFour && hasAccessValueFive);

                Assert.Contains("test content", response.Content);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Headers and Content.
        /// from the response have relevant information. This test has one response header
        /// with one response header value.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_OneHeaderAndContentTest(string storageProvider)
        {
            string[] httpResponseHeaders = { "test.host.com" };
            StringValues stringValues = new StringValues(httpResponseHeaders);
            Dictionary<string, StringValues> testHeaders = new Dictionary<string, StringValues>();
            testHeaders.Add("Host", stringValues);

            HttpResponseMessage testHttpResponseMessage = CreateTestHttpResponseMessageMultHeaders(
                                                                                        statusCode: HttpStatusCode.OK,
                                                                                        headers: testHeaders,
                                                                                        content: "test content");

            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandler(testHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_OneHeaderAndContentTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                // Uri uri = new Uri("https://dummy-test-url.com");
                // var request = new DurableHttpRequest(HttpMethod.Get, uri);
                // StringValues stringValues = new StringValues("application/json");
                // request.Headers.Add("Accept", stringValues);

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);

                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                var hostHeaders = response.Headers["Host"];
                bool hasHostValueOne = response.Headers["Host"].Contains("test.host.com");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(hasHostValueOne);
                Assert.Contains("test content", response.Content);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator works with a
        /// Retry-After header.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_AsynchronousAPI_RetryAfterTest(string storageProvider)
        {
            Dictionary<string, string> testHeaders = new Dictionary<string, string>();
            testHeaders.Add("Retry-After", "1");
            testHeaders.Add("Location", "https://www.dummy-url.com");

            HttpResponseMessage acceptedHttpResponseMessage = CreateTestHttpResponseMessage(
                                                                                        statusCode: HttpStatusCode.Accepted,
                                                                                        headers: testHeaders);
            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandlerWithRetryAfter(acceptedHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_AsynchronousAPI_RetryAfterTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(240));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Async functionality
        /// waits until an OK response is returned.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_AsynchronousAPI_ReturnsOK200(string storageProvider)
        {
            Dictionary<string, string> asyncTestHeaders = new Dictionary<string, string>();
            asyncTestHeaders.Add("Location", "https://www.dummy-location-url.com");

            HttpResponseMessage acceptedHttpResponseMessage = CreateTestHttpResponseMessage(
                statusCode: HttpStatusCode.Accepted,
                headers: asyncTestHeaders);

            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandler(acceptedHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_AsynchronousAPI_ReturnsOK200),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Async functionality
        /// works with Content-Type of application/json.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task CallHttpAsync_SynchronousAPI_ReqContentTest(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockHttpMessageHandlerContentType();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.CallHttpAsync_SynchronousAPI_ReqContentTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                string requestBody = "{\"key\": \"value\",\"key\": \"value\",\"values\": {\"key\": \"value\",\"key\": \"value\",\"key\": true,}}";

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Content-Type", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    content: requestBody);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Async functionality
        /// returns an OK response when body content is passed to the HTTP request, but the
        /// Content-Type is not specified.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_SynchronousAPI_NoContentTypeTest(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockHttpMessageHandlerContentType();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_SynchronousAPI_NoContentTypeTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                string requestBody = "test request body";

                Dictionary<string, string> headers = new Dictionary<string, string>();
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    content: requestBody);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Async functionality
        /// works when the Content-Type is "application/x-www-form-urlencoded".
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_SynchronousAPI_UrlEncodedTest(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockHttpMessageHandlerContentType();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_SynchronousAPI_UrlEncodedTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                string requestBody = "Test request body";
                requestBody = string.Format(
                    "site={0}&content={1}",
                    Uri.EscapeDataString("https://www.dummy-url.com"),
                    Uri.EscapeDataString("Test request body"));

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Content-Type", "application/x-www-form-urlencoded");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    content: requestBody);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator Async functionality
        /// waits until an OK response is returned with a long running process.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_AsynchronousAPI_LongRunning(string storageProvider)
        {
            Dictionary<string, string> asyncTestHeaders = new Dictionary<string, string>();
            asyncTestHeaders.Add("Location", "https://www.dummy-location-url.com");

            HttpResponseMessage acceptedHttpResponseMessage = CreateTestHttpResponseMessage(
                                                                                               statusCode: HttpStatusCode.Accepted,
                                                                                               headers: asyncTestHeaders);
            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandlerLongRunning(acceptedHttpResponseMessage);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_AsynchronousAPI_LongRunning),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                httpAsyncSleepTime: 1000,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var client = await host.StartOrchestratorAsync(functionName, testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(40000));

                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if multiple CallHttpAsync Orchestrator Async calls
        /// all return an OK response status code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttp_AsyncAPI_MultipleCalls(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandlerForMultipleRequestsTwo();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttp_AsyncAPI_MultipleCalls),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
            {
                await host.StartAsync();

                // First request
                Dictionary<string, string> headersOne = new Dictionary<string, string>();
                headersOne.Add("Accept", "application/json");
                TestDurableHttpRequest testRequestOne = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    uri: "https://www.dummy-url.com/AsyncRequestOne",
                    headers: headersOne);

                string functionNameOne = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var clientOne = await host.StartOrchestratorAsync(functionNameOne, testRequestOne, this.output);
                var statusOne = await clientOne.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));
                var outputOne = statusOne?.Output;
                DurableHttpResponse responseOne = outputOne.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, responseOne.StatusCode);

                // Second request
                Dictionary<string, string> headersTwo = new Dictionary<string, string>();
                headersTwo.Add("Accept", "application/json");
                TestDurableHttpRequest testRequestTwo = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    uri: "https://www.dummy-url.com/AsyncRequestTwo",
                    headers: headersTwo);

                string functionName = nameof(TestOrchestrations.CallHttpAsyncOrchestrator);
                var clientTwo = await host.StartOrchestratorAsync(functionName, testRequestTwo, this.output);
                var statusTwo = await clientTwo.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));
                var outputTwo = statusTwo?.Output;
                DurableHttpResponse responseTwo = outputTwo.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, responseTwo.StatusCode);

                await host.StopAsync();
            }
        }

        private static HttpMessageHandler MockAsynchronousHttpMessageHandlerForMultipleRequestsTwo()
        {
            Dictionary<string, string> asyncTestHeadersOne = new Dictionary<string, string>();
            asyncTestHeadersOne.Add("Location", "https://www.dummy-location-url.com/AsyncRequestOne");

            Dictionary<string, string> asyncTestHeadersTwo = new Dictionary<string, string>();
            asyncTestHeadersTwo.Add("Location", "https://www.dummy-location-url.com/AsyncRequestTwo");

            HttpResponseMessage acceptedHttpResponseMessageOne = CreateTestHttpResponseMessage(
                                                                                              statusCode: HttpStatusCode.Accepted,
                                                                                              headers: asyncTestHeadersOne);
            HttpResponseMessage acceptedHttpResponseMessageTwo = CreateTestHttpResponseMessage(
                                                                                             statusCode: HttpStatusCode.Accepted,
                                                                                             headers: asyncTestHeadersTwo);
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => UriContainsGivenString(req, "AsyncRequestOne")), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessageOne,
                    acceptedHttpResponseMessageOne,
                    acceptedHttpResponseMessageOne,
                    acceptedHttpResponseMessageOne,
                    okHttpResponseMessage,
                }).Dequeue);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => UriContainsGivenString(req, "AsyncRequestTwo")), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
               {
                    acceptedHttpResponseMessageTwo,
                    acceptedHttpResponseMessageTwo,
                    acceptedHttpResponseMessageTwo,
                    acceptedHttpResponseMessageTwo,
                    okHttpResponseMessage,
               }).Dequeue);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => !UriContainsGivenString(req, "AsyncRequestOne") && !UriContainsGivenString(req, "AsyncRequestTwo")), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(forbiddenResponseMessage);

            return handlerMock.Object;
        }

        private static bool UriContainsGivenString(HttpRequestMessage req, string uriEnd)
        {
            return req.RequestUri.ToString().EndsWith(uriEnd);
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code
        /// when a Bearer Token is added to the DurableHttpRequest object.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_AddsBearerToken(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerForTestingTokenSource();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_AddsBearerToken),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler),
                serializerSettings: MockTokenSourceSerializerSettings))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                MockTokenSource mockTokenSource = new MockTokenSource("dummy test token");

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    tokenSource: mockTokenSource);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));
                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code
        /// when the MockTokenSource object takes in a ManagedIdentityOptions object and
        /// a Bearer Token is added to the DurableHttpRequest object.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Synchronous_TokenWithOptions(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockSynchronousHttpMessageHandlerForTestingTokenSource();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Synchronous_TokenWithOptions),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler),
                serializerSettings: MockTokenSourceSerializerSettings))
            {
                await host.StartAsync();

                ManagedIdentityOptions credentialOptions = new ManagedIdentityOptions();
                credentialOptions.AuthorityHost = new Uri("https://dummy.login.microsoftonline.com/");
                credentialOptions.TenantId = "tenant_id";

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                MockTokenSource mockTokenSource = new MockTokenSource("dummy test token", credentialOptions);

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    tokenSource: mockTokenSource);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));
                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code
        /// when the MockTokenSource object takes in a ManagedIdentityOptions object,
        /// a Bearer Token is added to the DurableHttpRequest object, and follows the
        /// asynchronous pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Asynchronous_TokenWithOptions(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandlerForTestingTokenSource(crossOrigin: false);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Asynchronous_TokenWithOptions),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler),
                serializerSettings: MockTokenSourceSerializerSettings))
            {
                await host.StartAsync();

                ManagedIdentityOptions credentialOptions = new ManagedIdentityOptions();
                credentialOptions.AuthorityHost = new Uri("https://dummy.login.microsoftonline.com/");
                credentialOptions.TenantId = "tenant_id";

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                MockTokenSource mockTokenSource = new MockTokenSource("dummy test token", credentialOptions);

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    tokenSource: mockTokenSource);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));
                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code
        /// when a Bearer Token is added to the DurableHttpRequest object.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttp_AsyncAPI_PollIgnoresFunctionsKey(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockHttpMessageHandlerWithFunctionHeaderVerification();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttp_AsyncAPI_PollIgnoresFunctionsKey),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                httpAsyncSleepTime: 1000,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler),
                serializerSettings: MockTokenSourceSerializerSettings))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                headers.Add("x-functions-key", "function-level-key");
                MockTokenSource mockTokenSource = new MockTokenSource("dummy test token");

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    tokenSource: mockTokenSource);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));
                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);
                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        private static HttpMessageHandler MockSynchronousHttpMessageHandlerForTestingTokenSource()
        {
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => HasBearerToken(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(okHttpResponseMessage);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => !HasBearerToken(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(forbiddenHttpResponseMessage);

            return handlerMock.Object;
        }

        private static bool HasBearerToken(HttpRequestMessage req)
        {
            if (!req.Headers.TryGetValues("Authorization", out var values))
            {
                return false;
            }

            string headerValue = values.FirstOrDefault();
            return string.Equals(headerValue, "Bearer dummy test token");
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code
        /// when a Bearer Token is added to the DurableHttpRequest object and follows the
        /// asynchronous pattern with a same-origin Location redirect. The bearer token should
        /// be forwarded to the poll requests.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Asynchronous_AddsBearerToken(string storageProvider)
            => await this.RunAsyncBearerTokenTest(storageProvider, nameof(this.DurableHttpAsync_Asynchronous_AddsBearerToken), crossOrigin: false);

        /// <summary>
        /// End-to-end test which checks that when a 202 Location redirect goes to a different
        /// origin, the bearer token (TokenSource) is NOT forwarded to the poll requests. The mock
        /// handler returns OK only when the poll request does NOT carry a bearer token, proving
        /// that cross-origin credential stripping works correctly.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Asynchronous_CrossOrigin_StripsBearerToken(string storageProvider)
            => await this.RunAsyncBearerTokenTest(storageProvider, nameof(this.DurableHttpAsync_Asynchronous_CrossOrigin_StripsBearerToken), crossOrigin: true);

        private async Task RunAsyncBearerTokenTest(string storageProvider, string testName, bool crossOrigin)
        {
            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandlerForTestingTokenSource(crossOrigin);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler),
                serializerSettings: MockTokenSourceSerializerSettings))
            {
                await host.StartAsync();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Accept", "application/json");
                MockTokenSource mockTokenSource = new MockTokenSource("dummy test token");

                TestDurableHttpRequest testRequest = new TestDurableHttpRequest(
                    httpMethod: HttpMethod.Get,
                    headers: headers,
                    tokenSource: mockTokenSource);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallHttpAsyncOrchestrator), testRequest, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 90));
                Assert.NotNull(status);
                var output = status.Output;
                Assert.NotNull(output);

                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Creates a mock handler for testing bearer-token forwarding during async 202 polling.
        /// When <paramref name="crossOrigin"/> is false, the Location redirect is same-origin and
        /// the bearer token is expected on poll requests (OK if present, Forbidden if absent).
        /// When true, the Location redirect is cross-origin and the bearer token should be stripped
        /// (OK if absent, Forbidden if present).
        /// </summary>
        private static HttpMessageHandler MockAsynchronousHttpMessageHandlerForTestingTokenSource(bool crossOrigin)
        {
            string locationUrl = crossOrigin
                ? "https://www.cross-origin-url.com/status"
                : "https://www.dummy-url.com/status";

            Dictionary<string, string> asyncTestHeaders = new Dictionary<string, string>();
            asyncTestHeaders.Add("Location", locationUrl);

            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);
            HttpResponseMessage acceptedHttpResponseMessage = CreateTestHttpResponseMessage(
                                                                                               statusCode: HttpStatusCode.Accepted,
                                                                                               headers: asyncTestHeaders);

            // Requests with token: for same-origin, all requests (initial + polls) carry the
            // token, so we return 202 four times then OK. For cross-origin, only the initial
            // request carries the token; if a poll also carries it, the token leaked and the
            // dequeue will throw (failing the test).
            var withTokenQueue = crossOrigin
                ? new Queue<HttpResponseMessage>(new[] { acceptedHttpResponseMessage })
                : new Queue<HttpResponseMessage>(new[]
                  {
                      acceptedHttpResponseMessage,
                      acceptedHttpResponseMessage,
                      acceptedHttpResponseMessage,
                      acceptedHttpResponseMessage,
                      okHttpResponseMessage,
                  });

            // Requests without token: for cross-origin polls this is expected (OK);
            // for same-origin this means the token was incorrectly stripped (Forbidden).
            HttpResponseMessage withoutTokenResponse = crossOrigin
                ? okHttpResponseMessage
                : forbiddenHttpResponseMessage;

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => HasBearerToken(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(withTokenQueue.Dequeue);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => !HasBearerToken(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(withoutTokenResponse);

            return handlerMock.Object;
        }

        private static HttpMessageHandler CreateAsynchronousHttpMessageHandlerForMultipleRequests()
        {
            Dictionary<string, string> asyncTestHeadersOne = new Dictionary<string, string>();
            asyncTestHeadersOne.Add("Location", "https://www.dummy-location-url.com/AsyncRequestOne");

            Dictionary<string, string> asyncTestHeadersTwo = new Dictionary<string, string>();
            asyncTestHeadersTwo.Add("Location", "https://www.dummy-location-url.com/AsyncRequestTwo");

            HttpResponseMessage acceptedHttpResponseMessageOne = CreateTestHttpResponseMessage(
                                                                                               statusCode: HttpStatusCode.Accepted,
                                                                                               headers: asyncTestHeadersOne);

            HttpResponseMessage acceptedHttpResponseMessageTwo = CreateTestHttpResponseMessage(
                                                                                              statusCode: HttpStatusCode.Accepted,
                                                                                              headers: asyncTestHeadersTwo);

            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().EndsWith("AsyncRequestOne")), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessageOne,
                    acceptedHttpResponseMessageOne,
                    acceptedHttpResponseMessageOne,
                    acceptedHttpResponseMessageOne,
                    okHttpResponseMessage,
                }).Dequeue);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().EndsWith("AsyncRequestTwo")), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessageTwo,
                    acceptedHttpResponseMessageTwo,
                    acceptedHttpResponseMessageTwo,
                    acceptedHttpResponseMessageTwo,
                    okHttpResponseMessage,
                }).Dequeue);

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockHttpMessageHandlerWithFunctionHeaderVerification()
        {
            Dictionary<string, string> asyncTestHeadersOne = new Dictionary<string, string>();
            asyncTestHeadersOne.Add("Location", "https://www.dummy-location-url.com/poll-status");

            HttpResponseMessage acceptedHttpResponseMessage =
                CreateTestHttpResponseMessage(
                    statusCode: HttpStatusCode.Accepted,
                    headers: asyncTestHeadersOne);

            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(
                        req => string.Equals(req.RequestUri.ToString(), "https://www.dummy-url.com/")
                            && req.Headers.Contains("x-functions-key")),
                    ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(acceptedHttpResponseMessage);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(
                        req => req.RequestUri.ToString().EndsWith("poll-status")
                             && !req.Headers.Contains("x-functions-key")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(okHttpResponseMessage);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(
                        req => req.RequestUri.ToString().EndsWith("poll-status")
                             && req.Headers.Contains("x-functions-key")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(forbiddenResponseMessage);

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockSynchronousHttpMessageHandler(HttpResponseMessage httpResponseMessage)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(httpResponseMessage);

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockSynchronousHttpMessageHandlerWithTimeout(HttpResponseMessage httpResponseMessage, TimeSpan timeoutTimespan)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .Returns(async () =>
               {
                   await Task.Delay(timeoutTimespan);
                   return httpResponseMessage;
               });

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockSynchronousHttpMessageHandlerWithTimeoutException(TimeSpan timeoutTimespan)
        {
            HttpResponseMessage httpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);

            httpResponseMessage.Content = new ExceptionThrowingContent(new OperationCanceledException());

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .Returns(async () =>
               {
                   await Task.Delay(timeoutTimespan);
                   return httpResponseMessage;
               });

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockSynchronousHttpMessageHandlerWithHttpRequestException()
        {
            mockSynchronousHttpMessageHandlerCount = 0;
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() =>
               {
                   // We create a new response every time because by the virtue of completing, the response object gets disposed
                   // so if we reused the same object in our ReturnsAsync() an ObjectDisposedException gets thrown
                   mockSynchronousHttpMessageHandlerCount++;

                   HttpResponseMessage httpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.NotFound);

                   httpResponseMessage.Content = new ExceptionThrowingContent(new HttpRequestException("No such host is known."));

                   return httpResponseMessage;
               });

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockSynchronousHttpMessageHandlerWithHttp404()
        {
            mockSynchronousHttpMessageHandlerCount = 0;
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(() =>
               {
                   // We create a new response every time because by the virtue of completing, the response object gets disposed
                   // so if we reused the same object in our ReturnsAsync() an ObjectDisposedException gets thrown
                   mockSynchronousHttpMessageHandlerCount++;

                   HttpResponseMessage httpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.NotFound);

                   return httpResponseMessage;
               });

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockHttpMessageHandlerCheckUserAgent()
        {
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => req.Headers.UserAgent != null), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(okHttpResponseMessage);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => req.Headers.UserAgent == null), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(forbiddenHttpResponseMessage);

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockHttpMessageHandlerCheckAcceptHeader()
        {
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => req.Headers.Accept != null), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(okHttpResponseMessage);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => req.Headers.Accept == null), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(forbiddenHttpResponseMessage);

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockHttpMessageHandlerContentType()
        {
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => !HasContentTypeHeader(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(okHttpResponseMessage);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => HasContentTypeHeader(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(forbiddenHttpResponseMessage);

            return handlerMock.Object;
        }

        public static bool HasContentTypeHeader(HttpRequestMessage req)
        {
            IEnumerable<string> values = new List<string>();
            bool containsContentType = req.Headers.TryGetValues("Content-Type", out values);
            return containsContentType;
        }

        private static HttpMessageHandler MockAsynchronousHttpMessageHandler(HttpResponseMessage acceptedHttpResponseMessage)
        {
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    okHttpResponseMessage,
                }).Dequeue);

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockAsynchronousHttpMessageHandlerLongRunning(HttpResponseMessage acceptedHttpResponseMessage)
        {
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    okHttpResponseMessage,
                }).Dequeue);

            return handlerMock.Object;
        }

        private static HttpMessageHandler MockAsynchronousHttpMessageHandlerWithRetryAfter(HttpResponseMessage acceptedHttpResponseMessage)
        {
            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    okHttpResponseMessage,
                }).Dequeue);

            return handlerMock.Object;
        }

        private static HttpResponseMessage CreateTestHttpResponseMessage(
            HttpStatusCode statusCode,
            Dictionary<string, string> headers = null,
            string content = "")
        {
            HttpResponseMessage newHttpResponseMessage = new HttpResponseMessage(statusCode);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    newHttpResponseMessage.Headers.Add(header.Key, header.Value);
                }
            }

            string json = JsonConvert.SerializeObject(content);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            newHttpResponseMessage.Content = httpContent;
            return newHttpResponseMessage;
        }

        private static HttpResponseMessage CreateTestHttpResponseMessageMultHeaders(
            HttpStatusCode statusCode,
            Dictionary<string, StringValues> headers = null,
            string content = "")
        {
            HttpResponseMessage newHttpResponseMessage = new HttpResponseMessage(statusCode);
            if (headers != null)
            {
                foreach (KeyValuePair<string, StringValues> header in headers)
                {
                    newHttpResponseMessage.Headers.Add(header.Key, (IEnumerable<string>)header.Value);
                }
            }

            string json = JsonConvert.SerializeObject(content);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            newHttpResponseMessage.Content = httpContent;
            return newHttpResponseMessage;
        }

        /// <summary>
        /// Verifies that <see cref="DurableOrchestrationContext.CreateLocationPollRequest"/> strips
        /// the Authorization and Cookie headers (and the original <see cref="ITokenSource"/>) when a
        /// 202 Location header redirects the poll to a different origin. This guards against a
        /// credential-leak vector where an attacker-controlled first-hop server redirects the async
        /// polling loop to a host they control.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateLocationPollRequest_CrossOrigin_StripsCredentials()
        {
            var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                { "Authorization", "Bearer original-token" },
                { "Cookie", "session=abc123; auth=xyz" },
                { "x-functions-key", "secret-key" },
                { "Accept", "application/json" },
            };

            var original = new DurableHttpRequest(
                method: HttpMethod.Get,
                uri: new Uri("https://management.azure.com/some/resource"),
                headers: headers,
                tokenSource: new ManagedIdentityTokenSource("https://management.azure.com/"));

            DurableHttpRequest poll = DurableOrchestrationContext.CreateLocationPollRequest(
                original,
                "https://attacker.example.com/steal");

            Assert.Equal(new Uri("https://attacker.example.com/steal"), poll.Uri);
            Assert.Null(poll.TokenSource);
            Assert.NotNull(poll.Headers);
            Assert.False(poll.Headers.ContainsKey("Authorization"));
            Assert.False(poll.Headers.ContainsKey("Cookie"));
            Assert.False(poll.Headers.ContainsKey("x-functions-key"));
            Assert.True(poll.Headers.ContainsKey("Accept"));
        }

        /// <summary>
        /// Verifies that headers (including Authorization/Cookie) are forwarded on a same-origin
        /// 202 Location redirect, which is the legitimate async polling pattern.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateLocationPollRequest_SameOrigin_ForwardsHeaders()
        {
            var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                { "Authorization", "Bearer original-token" },
                { "Cookie", "session=abc123" },
                { "Accept", "application/json" },
            };

            var original = new DurableHttpRequest(
                method: HttpMethod.Get,
                uri: new Uri("https://management.azure.com/start"),
                headers: headers);

            DurableHttpRequest poll = DurableOrchestrationContext.CreateLocationPollRequest(
                original,
                "https://management.azure.com/poll");

            Assert.NotNull(poll.Headers);
            Assert.Equal("Bearer original-token", poll.Headers["Authorization"]);
            Assert.Equal("session=abc123", poll.Headers["Cookie"]);
            Assert.Equal("application/json", poll.Headers["Accept"]);
        }

        /// <summary>
        /// Verifies that headers on the poll request are a defensive copy: stripping credentials
        /// on the new request must not mutate the original request's headers (the poll loop reuses
        /// the original request as the basis for each iteration).
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateLocationPollRequest_DoesNotMutateOriginalHeaders()
        {
            var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                { "Authorization", "Bearer original-token" },
                { "Cookie", "session=abc123" },
            };

            var original = new DurableHttpRequest(
                method: HttpMethod.Get,
                uri: new Uri("https://management.azure.com/start"),
                headers: headers);

            DurableOrchestrationContext.CreateLocationPollRequest(
                original,
                "https://attacker.example.com/steal");

            Assert.NotNull(original.Headers);
            Assert.True(original.Headers.ContainsKey("Authorization"));
            Assert.True(original.Headers.ContainsKey("Cookie"));
        }

        /// <summary>
        /// Verifies the same-origin policy used to decide whether to forward credentials across
        /// a 202 Location redirect. Origin is scheme + host + port, with case-insensitive host
        /// comparison. Asserted through <see cref="DurableOrchestrationContext.CreateLocationPollRequest"/>
        /// by observing whether the Authorization header is forwarded.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("https://example.com/start", "https://example.com/poll", true)]
        [InlineData("https://Example.COM/start", "https://example.com/poll", true)]
        [InlineData("https://example.com/start", "https://example.com:8443/poll", false)]
        [InlineData("https://example.com:443/start", "https://example.com:8443/poll", false)]
        [InlineData("https://example.com/start", "http://example.com/poll", false)]
        [InlineData("https://example.com/start", "https://attacker.example.com/poll", false)]
        [InlineData("https://example.com/start", "/poll", true)]
        [InlineData("https://example.com/start", "poll", true)]
        public void CreateLocationPollRequest_OriginComparison(string originalUri, string locationUri, bool expectHeadersForwarded)
        {
            var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                { "Authorization", "Bearer original-token" },
                { "Cookie", "session=abc123" },
            };

            var original = new DurableHttpRequest(
                method: HttpMethod.Get,
                uri: new Uri(originalUri),
                headers: headers);

            DurableHttpRequest poll = DurableOrchestrationContext.CreateLocationPollRequest(
                original,
                locationUri);

            Assert.NotNull(poll.Headers);
            if (expectHeadersForwarded)
            {
                Assert.True(poll.Headers.ContainsKey("Authorization"));
                Assert.True(poll.Headers.ContainsKey("Cookie"));
            }
            else
            {
                Assert.False(poll.Headers.ContainsKey("Authorization"));
                Assert.False(poll.Headers.ContainsKey("Cookie"));
            }
        }

        private class MockTokenSourceSerializerSettingsFactory : IMessageSerializerSettingsFactory
        {
            public JsonSerializerSettings CreateJsonSerializerSettings()
            {
                return new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    DateParseHandling = DateParseHandling.None,
                    SerializationBinder = new MockTokenSourceBinder(),
                };
            }
        }

        private class MockTokenSourceBinder : ISerializationBinder
        {
            public Type BindToType(string assemblyName, string typeName)
            {
                Type allowedType = typeof(MockTokenSource);
                bool allowedAssembly =
                    string.Equals(assemblyName, allowedType.Assembly.FullName, StringComparison.Ordinal) ||
                    string.Equals(assemblyName, allowedType.Assembly.GetName().Name, StringComparison.Ordinal);
                if (allowedAssembly && typeName == allowedType.FullName)
                {
                    return allowedType;
                }

                throw new JsonSerializationException($"Type '{typeName}, {assemblyName}' is not allowed.");
            }

            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                if (serializedType != typeof(MockTokenSource))
                {
                    throw new JsonSerializationException($"Type '{serializedType.FullName}' is not allowed.");
                }

                assemblyName = serializedType.Assembly.FullName;
                typeName = serializedType.FullName;
            }
        }

        [DataContract]
        private class MockTokenSource : ITokenSource
        {
            [DataMember]
            private readonly string testToken;

            [DataMember]
            private readonly ManagedIdentityOptions options;

            public MockTokenSource(string token, ManagedIdentityOptions options = null)
            {
                this.testToken = token;
                this.options = options;
            }

            public Task<string> GetTokenAsync()
            {
                return Task.FromResult(this.testToken);
            }

            public ManagedIdentityOptions GetOptions()
            {
                return this.options;
            }
        }

        private class ExceptionThrowingContent : HttpContent
        {
            private readonly Exception exception;

            public ExceptionThrowingContent(Exception exception)
            {
                this.exception = exception;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return Task.FromException(this.exception);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0L;
                return false;
            }
        }
    }
}
