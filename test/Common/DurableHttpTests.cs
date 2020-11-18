// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Primitives;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableHttpTests : IDisposable
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;
        private readonly bool useTestLogger = IsLogFriendlyPlatform();
        private readonly LogEventTraceListener eventSourceListener;

        public DurableHttpTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
            this.eventSourceListener = new LogEventTraceListener();
            this.StartLogCapture();
        }

        public void Dispose()
        {
            this.eventSourceListener.Dispose();
        }

        private void OnEventSourceListenerTraceLog(object sender, LogEventTraceListener.TraceLogEventArgs e)
        {
            this.output.WriteLine($"      ETW: {e.ProviderName} [{e.Level}] : {e.Message}");
        }

        private void StartLogCapture()
        {
            if (this.useTestLogger)
            {
                var traceConfig = new Dictionary<string, TraceEventLevel>
                {
                    { "DurableTask-AzureStorage", TraceEventLevel.Informational },
                    { "7DA4779A-152E-44A2-A6F2-F80D991A5BEE", TraceEventLevel.Warning }, // DurableTask.Core
                };

                this.eventSourceListener.OnTraceLog += this.OnEventSourceListenerTraceLog;

                string sessionName = "DTFxTrace" + Guid.NewGuid().ToString("N");
                this.eventSourceListener.CaptureLogs(sessionName, traceConfig);
            }
        }

        private static bool IsLogFriendlyPlatform()
        {
            return !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
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
            Assert.Empty(customHeaderValues);
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
    ""$type"": ""Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.DurableHttpTests+MockTokenSource, WebJobs.Extensions.DurableTask.Tests.V2"",
    ""testToken"": ""dummy token"",
    ""options"": {
      ""authorityhost"": ""https://dummy.login.microsoftonline.com/"",
      ""tenantid"": ""tenant_id""
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
      ""tenantid"": ""tenant_id""
    }
   },
  ""asynchronousPatternEnabled"": true
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
  ""asynchronousPatternEnabled"": true
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
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(400));

                var output = status?.Output;
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
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(400));

                var output = status?.Output;
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
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(400));

                var output = status?.Output;
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
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(400));

                var output = status?.Output;
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

                var output = status?.Output;
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

                var output = status?.Output;

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

                var output = status?.Output;

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

                var output = status?.Output;

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
            testHeaders.Add("Retry-After", "3");
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

                var output = status?.Output;
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

                var output = status?.Output;
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

                var output = status?.Output;
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

                var output = status?.Output;
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

                var output = status?.Output;
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

                var output = status?.Output;
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
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
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
                var output = status?.Output;
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
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
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
                var output = status?.Output;
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
            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandlerForTestingTokenSource();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Asynchronous_TokenWithOptions),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
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
                var output = status?.Output;
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
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
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
                var output = status?.Output;
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
            string headerValue = req.Headers.GetValues("Authorization").FirstOrDefault();
            return string.Equals(headerValue, "Bearer dummy test token");
        }

        /// <summary>
        /// End-to-end test which checks if the CallHttpAsync Orchestrator returns an OK (200) status code
        /// when a Bearer Token is added to the DurableHttpRequest object and follows the
        /// asynchronous pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableHttpAsync_Asynchronous_AddsBearerToken(string storageProvider)
        {
            HttpMessageHandler httpMessageHandler = MockAsynchronousHttpMessageHandlerForTestingTokenSource();

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableHttpAsync_Asynchronous_AddsBearerToken),
                enableExtendedSessions: false,
                storageProviderType: storageProvider,
                durableHttpMessageHandler: new DurableHttpMessageHandlerFactory(httpMessageHandler)))
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
                var output = status?.Output;

                DurableHttpResponse response = output.ToObject<DurableHttpResponse>();

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                await host.StopAsync();
            }
        }

        private static HttpMessageHandler MockAsynchronousHttpMessageHandlerForTestingTokenSource()
        {
            Dictionary<string, string> asyncTestHeaders = new Dictionary<string, string>();
            asyncTestHeaders.Add("Location", "https://www.dummy-location-url.com");

            HttpResponseMessage okHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage forbiddenHttpResponseMessage = CreateTestHttpResponseMessage(HttpStatusCode.Forbidden);
            HttpResponseMessage acceptedHttpResponseMessage = CreateTestHttpResponseMessage(
                                                                                               statusCode: HttpStatusCode.Accepted,
                                                                                               headers: asyncTestHeaders);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => HasBearerToken(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    okHttpResponseMessage,
                }).Dequeue);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req => !HasBearerToken(req)), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new Queue<HttpResponseMessage>(new[]
                {
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    acceptedHttpResponseMessage,
                    forbiddenHttpResponseMessage,
                }).Dequeue);

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
    }
}
