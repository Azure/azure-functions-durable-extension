using System.Net;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;

namespace Microsoft.Azure.Functions.Worker.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FunctionsDurableTaskClient />.
    /// </summary>
    public class FunctionsDurableTaskClientTests
    {
        private FunctionsDurableTaskClient GetTestFunctionsDurableTaskClient(string? baseUrl = null, OrchestrationMetadata? orchestrationMetadata = null)
        {
            // construct mock client

            // The DurableTaskClient demands a string parameter in it's constructor, so we pass it in
            string clientName = string.Empty;
            Mock<DurableTaskClient> durableClientMock = new(clientName);

            Task completedTask = Task.CompletedTask;
            durableClientMock.Setup(x => x.TerminateInstanceAsync(
                It.IsAny<string>(), It.IsAny<TerminateInstanceOptions>(), It.IsAny<CancellationToken>())).Returns(completedTask);

            if (orchestrationMetadata != null)
            {
                durableClientMock.Setup(x => x.GetInstancesAsync(orchestrationMetadata.InstanceId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orchestrationMetadata);
            }

            DurableTaskClient durableClient = durableClientMock.Object;
            FunctionsDurableTaskClient client = new FunctionsDurableTaskClient(durableClient, queryString: null, httpBaseUrl: baseUrl);
            return client;
        }

        /// <summary>
        /// Test that the `TerminateInstnaceAsync` can be invoked without exceptions.
        /// Exceptions are a risk since we inherit from an abstract class where default implementations are not provided.
        /// </summary>
        [Fact]
        public async Task TerminateDoesNotThrow()
        {
            FunctionsDurableTaskClient client = GetTestFunctionsDurableTaskClient();

            string instanceId = string.Empty;
            object output = string.Empty;
            TerminateInstanceOptions options = new TerminateInstanceOptions();
            CancellationToken token = CancellationToken.None;

            // call terminate API with every possible parameter combination
            // if we don't encounter any unimplemented exceptions from the abstract class,
            // then the test passes

            await client.TerminateInstanceAsync(instanceId, token);

            await client.TerminateInstanceAsync(instanceId, output);
            await client.TerminateInstanceAsync(instanceId, output, token);

            await client.TerminateInstanceAsync(instanceId);
            await client.TerminateInstanceAsync(instanceId, options);
            await client.TerminateInstanceAsync(instanceId, options, token);
        }

        /// <summary>
        /// Test that the `CreateHttpManagementPayload` method returns the expected payload structure without HttpRequestData.
        /// </summary>
        [Fact]
        public void CreateHttpManagementPayload_WithBaseUrl()
        {
            const string BaseUrl = "http://localhost:7071/runtime/webhooks/durabletask";
            FunctionsDurableTaskClient client = this.GetTestFunctionsDurableTaskClient(BaseUrl);
            string instanceId = "testInstanceIdWithHostBaseUrl";

            HttpManagementPayload payload = client.CreateHttpManagementPayload(instanceId);

            AssertHttpManagementPayload(payload, BaseUrl, instanceId);
        }

        /// <summary>
        /// Test that the `CreateHttpManagementPayload` method returns the expected payload structure with HttpRequestData.
        /// </summary>
        [Fact]
        public void CreateHttpManagementPayload_WithHttpRequestData()
        {
            const string requestUrl = "http://localhost:7075/orchestrators/E1_HelloSequence";
            FunctionsDurableTaskClient client = this.GetTestFunctionsDurableTaskClient();
            string instanceId = "testInstanceIdWithRequest";

            // Create mock HttpRequestData object.
            var mockFunctionContext = new Mock<FunctionContext>();
            var mockHttpRequestData = new Mock<HttpRequestData>(mockFunctionContext.Object);
            var headers = new HttpHeadersCollection();
            mockHttpRequestData.SetupGet(r => r.Headers).Returns(headers);
            mockHttpRequestData.SetupGet(r => r.Url).Returns(new Uri(requestUrl));

            HttpManagementPayload payload = client.CreateHttpManagementPayload(instanceId, mockHttpRequestData.Object);

            AssertHttpManagementPayload(payload, "http://localhost:7075/runtime/webhooks/durabletask", instanceId);
        }

        /// <summary>
        /// Test that the `WaitForCompletionOrCreateCheckStatusResponseAsync` method returns the expected response when the orchestration is completed.
        /// The expected response should include OrchestrationMetadata in the body with an HttpStatusCode.OK.
        /// </summary>
        [Fact]
        public async Task TestWaitForCompletionOrCreateCheckStatusResponseAsync_WhenCompleted()
        {
            string instanceId = "test-instance-id-completed";
            var expectedResult = new OrchestrationMetadata("TestCompleted", instanceId)
            {
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RuntimeStatus = OrchestrationRuntimeStatus.Completed,
                SerializedCustomStatus = "TestCustomStatus",
                SerializedInput = "TestInput",
                SerializedOutput = "TestOutput"
            };

            var client = this.GetTestFunctionsDurableTaskClient( orchestrationMetadata: expectedResult);

            HttpRequestData request = this.MockHttpRequestAndResponseData();

            HttpResponseData response = await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Reset stream position for reading
            response.Body.Position = 0;
            var orchestratorMetadata = await System.Text.Json.JsonSerializer.DeserializeAsync<dynamic>(response.Body);

            // Assert the response content is not null and check the content is correct.
            Assert.NotNull(orchestratorMetadata);
            AssertOrhcestrationMetadata(expectedResult, orchestratorMetadata);
        }

        /// <summary>
        /// Test that the `WaitForCompletionOrCreateCheckStatusResponseAsync` method returns expected response when the orchestrator didn't finish within
        /// the timeout period. The response body should contain a HttpManagementPayload with HttpStatusCode.Accepted.
        /// </summary>
        [Fact]
        public async Task TestWaitForCompletionOrCreateCheckStatusResponseAsync_WhenRunning()
        {
            string instanceId = "test-instance-id-running";
            var expectedResult = new OrchestrationMetadata("TestRunning", instanceId)
            {
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RuntimeStatus = OrchestrationRuntimeStatus.Running,
            };

            var client = this.GetTestFunctionsDurableTaskClient(orchestrationMetadata: expectedResult);

            HttpRequestData request = this.MockHttpRequestAndResponseData();
            HttpResponseData response;
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                response = await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId, cancellation: cts.Token);
            };

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            // Reset stream position for reading
            response.Body.Position = 0;
            HttpManagementPayload? payload;
            using (var reader = new StreamReader(response.Body))
            {
                payload = JsonConvert.DeserializeObject<HttpManagementPayload>(await reader.ReadToEndAsync());
            }

            // Assert the response content is not null and check the content is correct.
            Assert.NotNull(payload);
            AssertHttpManagementPayload(payload, "https://localhost:7075/runtime/webhooks/durabletask", instanceId);
        }

        /// <summary>
        /// Tests the `WaitForCompletionOrCreateCheckStatusResponseAsync` method to ensure it returns the correct HTTP status code
        /// based on the `returnInternalServerErrorOnFailure` parameter when the orchestration has failed.
        /// </summary>
        [Theory]
        [InlineData(true, HttpStatusCode.InternalServerError)]
        [InlineData(false, HttpStatusCode.OK)]
        public async Task TestWaitForCompletionOrCreateCheckStatusResponseAsync_WhenFailed(bool returnInternalServerErrorOnFailure, HttpStatusCode expected)
        {
            string instanceId = "test-instance-id-failed";
            var expectedResult = new OrchestrationMetadata("TestFailed", instanceId)
            {
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                RuntimeStatus = OrchestrationRuntimeStatus.Failed,
                SerializedOutput = "Microsoft.DurableTask.TaskFailedException: Task 'SayHello' (#0) failed with an unhandled exception: Exception while executing function: Functions.SayHello",
                SerializedInput = null
            };

            var client = this.GetTestFunctionsDurableTaskClient(orchestrationMetadata: expectedResult);

            HttpRequestData request = this.MockHttpRequestAndResponseData();

            HttpResponseData response = await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId, returnInternalServerErrorOnFailure: returnInternalServerErrorOnFailure);

            Assert.NotNull(response);
            Assert.Equal(expected, response.StatusCode);

            // Reset stream position for reading
            response.Body.Position = 0;
            var orchestratorMetadata = await System.Text.Json.JsonSerializer.DeserializeAsync<dynamic>(response.Body);

            // Assert the response content is not null and check the content is correct.
            Assert.NotNull(orchestratorMetadata);
            AssertOrhcestrationMetadata(expectedResult, orchestratorMetadata);
        }

        /// <summary>
        /// Tests the `GetBaseUrlFromRequest` can return the right base URL from the HttpRequestData with different forwarding or proxies.
        /// This test covers the following scenarios:
        /// - Using the "Forwarded" header
        /// - Using "X-Forwarded-Proto" and "X-Forwarded-Host" headers
        /// - Using only "X-Forwarded-Host" with default protocol
        /// - no headers
        /// </summary>
        [Theory]
        [InlineData("Forwarded", "proto=https;host=forwarded.example.com","","", "https://forwarded.example.com/runtime/webhooks/durabletask")]
        [InlineData("X-Forwarded-Proto", "https", "X-Forwarded-Host", "xforwarded.example.com", "https://xforwarded.example.com/runtime/webhooks/durabletask")]
        [InlineData("", "", "X-Forwarded-Host", "test.net", "https://test.net/runtime/webhooks/durabletask")]
        [InlineData("", "", "", "", "https://localhost:7075/runtime/webhooks/durabletask")] // Default base URL for empty headers
        public void TestHttpRequestDataForwardingHandling(string header1, string? value1, string header2, string value2, string expectedBaseUrl)
        {
            var headers = new HttpHeadersCollection();
            if (!string.IsNullOrEmpty(header1))
            {
                headers.Add(header1, value1);
            }
            if (!string.IsNullOrEmpty(header2))
            {
                headers.Add(header2, value2);
            }

            var request = this.MockHttpRequestAndResponseData(headers);
            var client = this.GetTestFunctionsDurableTaskClient();

            var payload = client.CreateHttpManagementPayload("testInstanceId", request);
            AssertHttpManagementPayload(payload, expectedBaseUrl, "testInstanceId");
        }



        private static void AssertHttpManagementPayload(HttpManagementPayload payload, string BaseUrl, string instanceId)
        {
            Assert.Equal(instanceId, payload.Id);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}", payload.PurgeHistoryDeleteUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/restart", payload.RestartPostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/raiseEvent/{{eventName}}", payload.SendEventPostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}", payload.StatusQueryGetUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/terminate?reason={{{{text}}}}", payload.TerminatePostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/suspend?reason={{{{text}}}}", payload.SuspendPostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/resume?reason={{{{text}}}}", payload.ResumePostUri);
        }

        private static void AssertOrhcestrationMetadata(OrchestrationMetadata expectedResult, dynamic actualResult)
        {
            Assert.Equal(expectedResult.Name, actualResult.GetProperty("Name").GetString());
            Assert.Equal(expectedResult.InstanceId, actualResult.GetProperty("InstanceId").GetString());
            Assert.Equal(expectedResult.CreatedAt, actualResult.GetProperty("CreatedAt").GetDateTime());
            Assert.Equal(expectedResult.LastUpdatedAt, actualResult.GetProperty("LastUpdatedAt").GetDateTime());
            Assert.Equal(expectedResult.RuntimeStatus.ToString(), actualResult.GetProperty("RuntimeStatus").GetString());
            Assert.Equal(expectedResult.SerializedInput, actualResult.GetProperty("SerializedInput").GetString());
            Assert.Equal(expectedResult.SerializedOutput, actualResult.GetProperty("SerializedOutput").GetString());
            Assert.Equal(expectedResult.SerializedCustomStatus, actualResult.GetProperty("SerializedCustomStatus").GetString());
        }

        // Mocks the required HttpRequestData and HttpResponseData for testing purposes.
        // This method sets up a mock HttpRequestData with a predefined URL and a mock HttpResponseDatav with a default status code and body. 
        // The headers of HttpRequestData can be provided as an optional parameter, otherwise an empty HttpHeadersCollection is used.
        private HttpRequestData MockHttpRequestAndResponseData(HttpHeadersCollection? headers = null)
        {
            var mockObjectSerializer = new Mock<ObjectSerializer>();
            
            // Setup the SerializeAsync method
            mockObjectSerializer.Setup(s => s.SerializeAsync(It.IsAny<Stream>(), It.IsAny<object?>(), It.IsAny<Type>(), It.IsAny<CancellationToken>()))
                .Returns<Stream, object?, Type, CancellationToken>(async (stream, value, type, token) =>
            {
                await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, type, cancellationToken: token);
            });

            var workerOptions = new WorkerOptions
            {
                Serializer = mockObjectSerializer.Object
            };
            var mockOptions = new Mock<IOptions<WorkerOptions>>();
            mockOptions.Setup(o => o.Value).Returns(workerOptions);

            // Mock the service provider
            var mockServiceProvider = new Mock<IServiceProvider>();

            // Set up the service provider to return the mock IOptions<WorkerOptions>
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<WorkerOptions>)))
                .Returns(mockOptions.Object);

            // Set up the service provider to return the mock ObjectSerializer
            mockServiceProvider.Setup(sp => sp.GetService(typeof(ObjectSerializer)))
                .Returns(mockObjectSerializer.Object);

            // Create a mock FunctionContext and assign the service provider
            var mockFunctionContext = new Mock<FunctionContext>();
            mockFunctionContext.SetupGet(c => c.InstanceServices).Returns(mockServiceProvider.Object);
            var mockHttpRequestData = new Mock<HttpRequestData>(mockFunctionContext.Object);
            
            // Set up the URL property.
            mockHttpRequestData.SetupGet(r => r.Url).Returns(new Uri("https://localhost:7075/orchestrators/E1_HelloSequence"));

            // If headers are provided, use them, otherwise create a new empty HttpHeadersCollection
            headers ??= new HttpHeadersCollection();

            // Setup the Headers property to return the empty headers
            mockHttpRequestData.SetupGet(r => r.Headers).Returns(headers);
            
            var mockHttpResponseData = new Mock<HttpResponseData>(mockFunctionContext.Object)
            {
                DefaultValue = DefaultValue.Mock
            };

            // Enable setting StatusCode and Body as mutable properties
            mockHttpResponseData.SetupProperty(r => r.StatusCode, HttpStatusCode.OK);
            mockHttpResponseData.SetupProperty(r => r.Body, new MemoryStream());

            // Setup CreateResponse to return the configured HttpResponseData mock
            mockHttpRequestData.Setup(r => r.CreateResponse())
                .Returns(mockHttpResponseData.Object);

            return mockHttpRequestData.Object;
        }
    }
}
