using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc;
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
            FunctionsDurableTaskClient client = new FunctionsDurableTaskClient(durableClient, queryString: null, baseUrl: baseUrl);
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
            string BaseUrl = "http://localhost:7071/runtime/webhooks/durabletask";
            FunctionsDurableTaskClient client = this.GetTestFunctionsDurableTaskClient(BaseUrl);
            string instanceId = "testInstanceIdWithHostBaseUrl";

            dynamic payload = client.CreateHttpManagementPayload(instanceId);

            AssertHttpManagementPayload(payload, BaseUrl, instanceId);
        }

        /// <summary>
        /// Test that the `CreateHttpManagementPayload` method returns the expected payload structure with HttpRequestData.
        /// </summary>
        [Fact]
        public void CreateHttpManagementPayload_WithHttpRequestData()
        {
            string requestUrl = "http://localhost:7075/orchestrators/E1_HelloSequence";
            FunctionsDurableTaskClient client = this.GetTestFunctionsDurableTaskClient();
            string instanceId = "testInstanceIdWithRequest";
            var context = new TestFunctionContext();
            var request = new TestHttpRequestData(context, new Uri(requestUrl));

            dynamic payload = client.CreateHttpManagementPayload(instanceId, request);

            AssertHttpManagementPayload(payload, "http://localhost:7075/runtime/webhooks/durabletask", instanceId);
        }

        private static void AssertHttpManagementPayload(dynamic payload, string BaseUrl, string instanceId)
        {
            Assert.Equal(instanceId, payload.id);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}", payload.purgeHistoryDeleteUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/raiseEvent/{{eventName}}", payload.sendEventPostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}", payload.statusQueryGetUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/terminate?reason={{{{text}}}}", payload.terminatePostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/suspend?reason={{{{text}}}}", payload.suspendPostUri);
            Assert.Equal($"{BaseUrl}/instances/{instanceId}/resume?reason={{{{text}}}}", payload.resumePostUri);
        }
    }

    /// <summary>
    /// A minimal implementation of FunctionContext for testing purposes.
    /// It's used to create a TestHttpRequestData instance, which requires a FunctionContext.
    /// </summary>
    public class TestFunctionContext : FunctionContext
    {
        public override string InvocationId => throw new NotImplementedException();
        public override string FunctionId => throw new NotImplementedException();
        public override TraceContext TraceContext => throw new NotImplementedException();
        public override BindingContext BindingContext => throw new NotImplementedException();
        public override RetryContext RetryContext => throw new NotImplementedException();
        public override IServiceProvider InstanceServices { get; set; } = new EmptyServiceProvider();
        public override FunctionDefinition FunctionDefinition => throw new NotImplementedException();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IInvocationFeatures Features => throw new NotImplementedException();
    }

    /// <summary>
    /// A minimal implementation of IServiceProvider for testing purposes.
    /// </summary>
    public class EmptyServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType) => null;
    }

    // <summary>
    /// A test implementation of HttpRequestData used for unit testing.
    /// This class allows us to create instances of HttpRequestData, which is normally abstract.
    /// </summary>
    public class TestHttpRequestData : HttpRequestData
    {
        public TestHttpRequestData(FunctionContext functionContext, Uri url) : base(functionContext)
        {
            Url = url;
        }

        public override Stream Body => throw new NotImplementedException();
        public override HttpHeadersCollection Headers => throw new NotImplementedException();
        public override IReadOnlyCollection<IHttpCookie> Cookies => throw new NotImplementedException();
        public override Uri Url { get; }
        public override IEnumerable<ClaimsIdentity> Identities => throw new NotImplementedException();
        public override string Method => throw new NotImplementedException();
        public override HttpResponseData CreateResponse() => throw new NotImplementedException();
    }
}