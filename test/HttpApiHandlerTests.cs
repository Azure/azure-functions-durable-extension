using System;
using System.Diagnostics;
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
        [Fact]
        private void CreateCheckStatusResponse_Throws_Exception_When_NotificationUrl_Missing()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtension(), null);
            var ex = Assert.Throws<InvalidOperationException>(() => httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage(), string.Empty, null));
            Assert.Equal("Webhooks are not configured", ex.Message);
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Throws_Exception_When_Bad_Timeout_Request()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtension() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            const int timeout = 0;
            const int retryInterval = 100;
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri($"{TestConstants.RequestUri}?timeout={timeout}&retryInterval={retryInterval}")
                }, 
                TestConstants.InstanceId, 
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                }));
            Assert.Equal($"Total timeout {timeout} should be bigger than retry timeout {retryInterval}", ex.Message);
        }

        [Fact]
        public async Task CreateCheckStatusResponse_Returns_Corrent_HTTP_202_Response()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtension() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            var httpResponseMessage = httpApiHandler.CreateCheckStatusResponse(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(TestConstants.RequestUri)
                }, 
                TestConstants.InstanceId, 
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                });
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.Accepted);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var status = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(status["id"], TestConstants.InstanceId);
            Assert.Equal(status["statusQueryGetUri"], 
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/7b59154ae666471993659902ed0ba742?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["sendEventPostUri"], 
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/7b59154ae666471993659902ed0ba742/raiseEvent/{eventName}?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["terminatePostUri"], 
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/7b59154ae666471993659902ed0ba742/terminate?reason={text}&taskHub=SampleHubVS&connection=Storage&code=mykey");
        }

        [Fact]

        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_After_Timeout()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            const int timeout = 100;
            const int retryInterval = 10;
            var stopWatch = Stopwatch.StartNew();
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri($"{TestConstants.RequestUri}?timeout={timeout}&retryInterval={retryInterval}")
                }, 
                TestConstants.RandomInstanceId, 
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                });
            stopWatch.Stop();
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.Accepted);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var status = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(status["id"], TestConstants.RandomInstanceId);
            Assert.Equal(status["statusQueryGetUri"], 
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["sendEventPostUri"], 
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749/raiseEvent/{eventName}?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["terminatePostUri"], 
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749/terminate?reason={text}&taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.True(stopWatch.Elapsed > TimeSpan.FromSeconds(30));
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            const int timeout = 100;
            const int retryInterval = 10;
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri($"{TestConstants.RequestUri}?timeout={timeout}&retryInterval={retryInterval}")
                }, 
                TestConstants.IntanceIdFactComplete, 
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                });
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.OK);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<string>(content);
            Assert.Equal(value, "Hello Tokyo!");
        }


        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_After_Few_Iterations()
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            const int timeout = 30;
            const int retryInterval = 8;
            var stopwatch = Stopwatch.StartNew();
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri($"{TestConstants.RequestUri}?timeout={timeout}&retryInterval={retryInterval}")
                }, 
                TestConstants.InstanceIdIterations, 
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                });
            stopwatch.Stop();
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.OK);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<string>(content);
            Assert.Equal(value, "Hello Tokyo!");
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_Defaults_When_Runtime_Status_is_Failed()
        {
            await CheckRuntimeStatus(TestConstants.InstanceIdFailed, OrchestrationRuntimeStatus.Failed);
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_Defaults_When_Runtime_Status_is_Terminated()
        {
            await CheckRuntimeStatus(TestConstants.InstanceIdTerminated, OrchestrationRuntimeStatus.Terminated);
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_Defaults_When_Runtime_Status_is_Canceled()
        {
            await CheckRuntimeStatus(TestConstants.InstanceIdCanceled, OrchestrationRuntimeStatus.Canceled);
        }

        private async Task CheckRuntimeStatus(string instanceId, OrchestrationRuntimeStatus runtimeStatus)
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            const int timeout = 30;
            const int retryInterval = 8;
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri($"{TestConstants.RequestUri}?timeout={timeout}&retryInterval={retryInterval}")
                }, 
                instanceId, 
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                });
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.OK);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(response["runtimeStatus"], runtimeStatus.ToString());
        }


        [Fact]

        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Double_Timeout_Settings()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Timeout_Settings($"{TestConstants.RequestUri}?timeout=15.5&retryInterval=1.5", 15);
        }

        [Fact]

        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Missing_Timeout_Settings()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Timeout_Settings($"{TestConstants.RequestUri}?retryInterval=1.5", 10);
        }

        [Fact]

        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Missing_RetryInterval_Settings()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Timeout_Settings($"{TestConstants.RequestUri}?timeout=15.5", 15);
        }

        [Fact]

        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Missing_Settings()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Timeout_Settings($"{TestConstants.RequestUri}", 10);
        }




        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_With_Timeout_Settings(string url, int timeout)
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            var stopWatch = Stopwatch.StartNew();
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(url)
                },
                TestConstants.RandomInstanceId,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                });
            stopWatch.Stop();
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.Accepted);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var status = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(status["id"], TestConstants.RandomInstanceId);
            Assert.Equal(status["statusQueryGetUri"],
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["sendEventPostUri"],
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749/raiseEvent/{eventName}?taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.Equal(status["terminatePostUri"],
                "http://localhost:7071/admin/extensions/DurableTaskExtension/instances/9b59154ae666471993659902ed0ba749/terminate?reason={text}&taskHub=SampleHubVS&connection=Storage&code=mykey");
            Assert.True(stopWatch.Elapsed > TimeSpan.FromSeconds(timeout));
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Double_Timeout_Setting()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Timeout_Setting($"{TestConstants.RequestUri}?timeout=5&retryInterval=1.5", 5);
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Missing_Timeout_Setting()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Timeout_Setting($"{TestConstants.RequestUri}?retryInterval=3", 10);
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Missing_RetryInterval_Setting()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Timeout_Setting($"{TestConstants.RequestUri}?timeout=15.7", 15);
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Missing_Settings()
        {
            await WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Timeout_Setting($"{TestConstants.RequestUri}", 10);
        }

        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_With_Timeout_Setting(string url, int timeout)
        {
            var httpApiHandler = new HttpApiHandler(new DurableTaskExtensionMock() { NotificationUrl = new Uri(TestConstants.NotificationUrl) }, null);
            var stopwatch = Stopwatch.StartNew();
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(url)
                },
                TestConstants.InstanceIdIterations,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName
                });
            stopwatch.Stop();
            Assert.Equal(httpResponseMessage.StatusCode, HttpStatusCode.OK);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<string>(content);
            Assert.Equal(value, "Hello Tokyo!");
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(timeout));
        }
    }
}
