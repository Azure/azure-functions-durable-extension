using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FunctionsDurableTaskClient />.
    /// </summary>
    public class FunctionsDurableTaskClientTests
    {
        private FunctionsDurableTaskClient GetTestFunctionsDurableTaskClient(string? baseUrl = null)
        {
            // construct mock client

            // The DurableTaskClient demands a string parameter in it's constructor, so we pass it in
            string clientName = string.Empty;
            Mock<DurableTaskClient> durableClientMock = new(clientName);

            Task completedTask = Task.CompletedTask;
            durableClientMock.Setup(x => x.TerminateInstanceAsync(
                It.IsAny<string>(), It.IsAny<TerminateInstanceOptions>(), It.IsAny<CancellationToken>())).Returns(completedTask);

            DurableTaskClient durableClient = durableClientMock.Object;
            FunctionsDurableTaskClient client = new FunctionsDurableTaskClient(durableClient, queryString: null, httpBaseUrl: baseUrl);
            return client;
        }

        /// <summary>
        /// Test that the `TerminateInstnaceAsync` can be invoked without exceptions.
        /// Exceptions are a risk since we inherit from an abstract class where default implementations are not provided.
        /// </summary>
        [Fact]
        public async void TerminateDoesNotThrow()
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
            mockHttpRequestData.SetupGet(r => r.Url).Returns(new Uri(requestUrl));

            HttpManagementPayload payload = client.CreateHttpManagementPayload(instanceId, mockHttpRequestData.Object);

            AssertHttpManagementPayload(payload, "http://localhost:7075/runtime/webhooks/durabletask", instanceId);
        }

        private static void AssertHttpManagementPayload(HttpManagementPayload payload, string BaseUrl, string instanceId)
        {
            Assert.Equal(instanceId, payload.Id);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}", payload.PurgeHistoryDeleteUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/raiseEvent/{{eventName}}", payload.SendEventPostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}", payload.StatusQueryGetUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/terminate?reason={{{{text}}}}", payload.TerminatePostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/suspend?reason={{{{text}}}}", payload.SuspendPostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/resume?reason={{{{text}}}}", payload.ResumePostUri);
        }
    }
}
