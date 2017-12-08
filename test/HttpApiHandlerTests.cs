using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class HttpApiHandlerTests
    {
        private const string RequestUri = "http://localhost:7071/orchestrators/E1_HelloSequence";
        private const string NotificationUrl = "http://localhost:7071/admin/extensions/DurableTaskExtension?code=mykey";
        private const string InstanceId = "7b59154ae666471993659902ed0ba742";
        private const string InstanceIdIterations = "7b59154ae666471993659902ed0ba749";
        private const string RandomInstanceId = "9b59154ae666471993659902ed0ba749";
        private const string TaskHub = "SampleHubVS";
        private const string ConnectionName = "Storage";


        [Fact]
        private void CreateCheckStatusResponse_Throws_Exception_When_NotificationUrl_Missing()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtension(), null);
            var ex = Assert.Throws<InvalidOperationException>(() => httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage(), string.Empty, null));
            Assert.Equal("Webhooks are not configured", ex.Message);
        }

        [Fact]
        public async Task CreateCheckStatusResponse_Throws_Exception_When_Bad_Timeout_Request()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtension() { NotificationUrl = new Uri(NotificationUrl) }, null);
            var totalTimeout = TimeSpan.FromSeconds(0);
            var retryTimeout = TimeSpan.FromSeconds(100);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage() { RequestUri = new Uri(RequestUri) }, InstanceId, new OrchestrationClientAttribute() { TaskHub = TaskHub, ConnectionName = ConnectionName }, totalTimeout, retryTimeout));
            Assert.Equal($"Total timeout {totalTimeout.TotalSeconds} should be bigger than retry timeout {retryTimeout.TotalSeconds}", ex.Message);
        }

        [Fact]
        public async Task CreateCheckStatusResponse_Returns_Corrent_HTTP_202_Response()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtension() { NotificationUrl = new Uri(NotificationUrl) }, null);
            var httpResponseMessage = httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage() { RequestUri = new Uri(RequestUri) }, InstanceId, new OrchestrationClientAttribute() { TaskHub = TaskHub, ConnectionName = ConnectionName });
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.Accepted);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var status = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(status["id"], InstanceId);
            Assert.Equal(status["statusQueryGetUri"], "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/7b59154ae666471993659902ed0ba742?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["sendEventPostUri"], "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/7b59154ae666471993659902ed0ba742/raiseEvent/{eventName}?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["terminatePostUri"], "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/7b59154ae666471993659902ed0ba742/terminate?reason={text}&taskHub=SampleHubVS&connection=Storage&code=mykey");
        }

        [Fact]

        public async Task CreateCheckStatusResponse_Returns_HTTP_202_Response_After_Timeout()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(NotificationUrl) }, null);
            var httpResponseMessage = await httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage() { RequestUri = new Uri(RequestUri) }, RandomInstanceId, new OrchestrationClientAttribute() { TaskHub = TaskHub, ConnectionName = ConnectionName }, TimeSpan.FromSeconds(1000), TimeSpan.FromSeconds(10));
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.Accepted);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var status = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(status["id"], RandomInstanceId);
            Assert.Equal(status["statusQueryGetUri"], "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["sendEventPostUri"], "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749/raiseEvent/{eventName}?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["terminatePostUri"], "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749/terminate?reason={text}&taskHub=SampleHubVS&connection=Storage&code=mykey");
        }

        [Fact]
        public async Task CreateCheckStatusResponse_Returns_HTTP_200_Response()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(NotificationUrl) }, null);
            var httpResponseMessage = await httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage() { RequestUri = new Uri(RequestUri) }, InstanceId, new OrchestrationClientAttribute() { TaskHub = TaskHub, ConnectionName = ConnectionName }, TimeSpan.FromSeconds(1000), TimeSpan.FromSeconds(10));
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.OK);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<string>(content);
            Assert.Equal(value, "Hello Tokyo!");
        }


        [Fact]
        public async Task CreateCheckStatusResponse_Returns_HTTP_200_Response_After_Few_Iterations()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(NotificationUrl) }, null);
            var httpResponseMessage = await httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage() { RequestUri = new Uri(RequestUri) }, InstanceIdIterations, new OrchestrationClientAttribute() { TaskHub = TaskHub, ConnectionName = ConnectionName }, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(8));
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.OK);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<string>(content);
            Assert.Equal(value, "Hello Tokyo!");
        }

        // TBD : Add tests for covering the Cancel, Failed and Terminated cases
        // TBD : Add tests checking the new way of pausing the execution 


    }
}
