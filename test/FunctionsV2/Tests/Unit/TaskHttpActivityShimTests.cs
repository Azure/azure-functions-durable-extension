// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TaskHttpActivityShimTests
    {
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PollingRedirectBehaviorFollowsRestrictionFlag(bool restricted)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            int received = 0;
            Task server = ServeAsync();
            using HttpClient pollingClient = new DurableHttpClientFactory().CreatePollingClient(new DurableHttpMessageHandlerFactory());
            using HttpClient normalClient = new DurableHttpClientFactory().GetClient(new DurableHttpMessageHandlerFactory());
            var original = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri($"http://127.0.0.1:{port}/start"),
                new System.Collections.Generic.Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "X-Selected", "value" } })
            {
                EnableCrossOriginHeaderFiltering = restricted,
            };
            DurableHttpRequest request = DurableOrchestrationContext.CreateLocationPollRequest(original, "/poll");

            try
            {
                string result = await RunActivityAsync(normalClient, null, request: request, pollingClientFactory: () => pollingClient)
                    .WaitAsync(cancellation.Token);
                Assert.Equal(
                    restricted ? HttpStatusCode.Redirect : HttpStatusCode.OK,
                    JsonConvert.DeserializeObject<DurableHttpResponse>(result).StatusCode);
                Assert.Equal(restricted ? 1 : 2, received);
            }
            finally
            {
                cancellation.Cancel();
                await server;
            }

            async Task ServeAsync()
            {
                try
                {
                    while (true)
                    {
                        using TcpClient connection = await listener.AcceptTcpClientAsync(cancellation.Token);
                        using NetworkStream stream = connection.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                        string line = await reader.ReadLineAsync(cancellation.Token);
                        Assert.NotNull(line);
                        bool selectedHeaderReceived = false;
                        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellation.Token)))
                        {
                            selectedHeaderReceived |= line.Equals("X-Selected: value", StringComparison.OrdinalIgnoreCase);
                        }

                        Assert.True(selectedHeaderReceived);
                        int count = Interlocked.Increment(ref received);
                        string response = count == 1
                            ? $"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{port}/next\r\n"
                            : "HTTP/1.1 200 OK\r\n";
                        byte[] bytes = Encoding.ASCII.GetBytes(response + "Content-Length: 0\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(bytes, cancellation.Token);
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConfiguredPollingRejectsCustomHandlers()
        {
            using var handler = new CallbackHandler((_, _) => Task.FromResult(CreateResponse()));
            Assert.Throws<NotSupportedException>(() => new DurableHttpClientFactory().CreatePollingClient(
                new DurableHttpMessageHandlerFactory(handler)));
            Assert.Throws<NotSupportedException>(() => new DurableHttpClientFactory().CreatePollingClient(
                new CustomHandlerFactory()));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ConfiguredPollingDoesNotFallBackToNormalClient()
        {
            using var handler = new CallbackHandler((_, _) => throw new InvalidOperationException("Unexpected transport."));
            using HttpClient client = CreateClient(handler);
            var original = new DurableHttpRequest(HttpMethod.Get, new Uri("https://start.example/start"))
            {
                EnableCrossOriginHeaderFiltering = true,
            };
            DurableHttpRequest request = DurableOrchestrationContext.CreateLocationPollRequest(original, "https://poll.example/status");
            await Assert.ThrowsAsync<NotSupportedException>(() => RunActivityAsync(client, null, request: request));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DisabledRestrictionUsesCustomTransport(bool configured)
        {
            bool sent = false;
            using var handler = new CallbackHandler((request, _) =>
            {
                sent = true;
                Assert.True(request.Headers.Contains("X-Selected"));
                return Task.FromResult(CreateResponse());
            });
            using HttpClient client = CreateClient(handler);
            var original = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("https://start.example/start"),
                new System.Collections.Generic.Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "X-Selected", "value" } });
            Assert.False(original.EnableCrossOriginHeaderFiltering);
            if (configured)
            {
                original.CrossOriginPollingHeaders = new System.Collections.Generic.Dictionary<string, string[]>
                {
                    { "https://poll.example", new[] { "X-Selected" } },
                };
            }

            DurableHttpRequest poll = DurableOrchestrationContext.CreateLocationPollRequest(original, "https://poll.example/status");
            string result = await RunActivityAsync(
                client,
                null,
                request: poll,
                pollingClientFactory: () => throw new InvalidOperationException("Unexpected polling transport."));

            Assert.True(sent);
            Assert.Equal(HttpStatusCode.OK, JsonConvert.DeserializeObject<DurableHttpResponse>(result).StatusCode);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FactoryCachesClientWithoutTimeout()
        {
            var factory = new DurableHttpClientFactory();
            var handlerFactory = new DurableHttpMessageHandlerFactory();
            using HttpClient client = factory.GetClient(handlerFactory);

            Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
            Assert.Same(client, factory.GetClient(handlerFactory));
            Assert.NotEmpty(client.DefaultRequestHeaders.UserAgent);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null)]
        [InlineData(-1)]
        [InlineData(600000)]
        public async Task RequestCompletesWithoutChangingSharedClientTimeout(int? timeoutMilliseconds)
        {
            using var handler = new CallbackHandler((_, token) =>
            {
                Assert.True(token.CanBeCanceled);
                Assert.False(token.IsCancellationRequested);
                return Task.FromResult(CreateResponse());
            });
            using HttpClient client = CreateClient(handler);
            TimeSpan? timeout = timeoutMilliseconds.HasValue ? TimeSpan.FromMilliseconds(timeoutMilliseconds.Value) : null;

            string result = await RunActivityAsync(client, timeout);

            Assert.Equal(HttpStatusCode.OK, JsonConvert.DeserializeObject<DurableHttpResponse>(result).StatusCode);
            Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(0)]
        [InlineData(100)]
        public async Task ExpiredExplicitTimeoutPreservesCancellationCause(int timeoutMilliseconds)
        {
            using var handler = new CallbackHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return CreateResponse();
            });
            using HttpClient client = CreateClient(handler);
            TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

            TaskFailureException failure = await Assert.ThrowsAsync<TaskFailureException>(
                () => RunActivityAsync(client, timeout).WaitAsync(TimeSpan.FromSeconds(10)));

            var timeoutException = Assert.IsType<TimeoutException>(failure.InnerException);
            var cancellation = Assert.IsAssignableFrom<OperationCanceledException>(timeoutException.InnerException);
            Assert.True(cancellation.CancellationToken.IsCancellationRequested);
            Assert.Contains($"Reached user specified timeout: {timeout}.", timeoutException.Message);
            JObject cause = JObject.Parse(failure.Details);
            Assert.Equal(typeof(TimeoutException).FullName, cause["ClassName"]);
            Assert.NotEqual(JTokenType.Null, cause["InnerException"].Type);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null)]
        [InlineData(-1)]
        [InlineData(600000)]
        public async Task UnrelatedCancellationIsNotReportedAsTimeout(int? timeoutMilliseconds)
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            var cancellation = new OperationCanceledException("Unrelated handler cancellation", cancellationSource.Token);
            using var handler = new CallbackHandler((_, _) => Task.FromException<HttpResponseMessage>(cancellation));
            using HttpClient client = CreateClient(handler);
            TimeSpan? timeout = timeoutMilliseconds.HasValue ? TimeSpan.FromMilliseconds(timeoutMilliseconds.Value) : null;

            OperationCanceledException actual = await Assert.ThrowsAsync<OperationCanceledException>(
                () => RunActivityAsync(client, timeout));

            Assert.Same(cancellation, actual);
            Assert.DoesNotContain("Reached user specified timeout", actual.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ConcurrentRequestTimeoutsAreIndependent()
        {
            var longRequestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseLongRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken longRequestToken = default;
            using var handler = new CallbackHandler(async (request, token) =>
            {
                if (request.RequestUri.AbsolutePath == "/long")
                {
                    longRequestToken = token;
                    longRequestStarted.SetResult(true);
                    await releaseLongRequest.Task.WaitAsync(token);
                    return CreateResponse();
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return CreateResponse();
            });
            using HttpClient client = CreateClient(handler);
            Task<string> longRequest = RunActivityAsync(client, TimeSpan.FromMinutes(10), "long");
            try
            {
                await longRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                TaskFailureException failure = await Assert.ThrowsAsync<TaskFailureException>(
                    () => RunActivityAsync(client, TimeSpan.FromMilliseconds(100), "short").WaitAsync(TimeSpan.FromSeconds(10)));

                Assert.IsType<TimeoutException>(failure.InnerException);
                Assert.False(longRequest.IsCompleted);
                Assert.False(longRequestToken.IsCancellationRequested);
                Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
            }
            finally
            {
                releaseLongRequest.TrySetResult(true);
                await longRequest.WaitAsync(TimeSpan.FromSeconds(10));
            }

            Assert.Equal(HttpStatusCode.OK, JsonConvert.DeserializeObject<DurableHttpResponse>(await longRequest).StatusCode);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(-2)]
        [InlineData(4294967295)]
        public async Task InvalidRequestTimeoutIsRejectedBeforeSending(long timeoutMilliseconds)
        {
            bool sent = false;
            using var handler = new CallbackHandler((_, _) =>
            {
                sent = true;
                return Task.FromResult(CreateResponse());
            });
            using HttpClient client = CreateClient(handler);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => RunActivityAsync(client, TimeSpan.FromMilliseconds(timeoutMilliseconds)));

            Assert.False(sent);
        }

        private static HttpClient CreateClient(HttpMessageHandler handler)
        {
            return new DurableHttpClientFactory().GetClient(new DurableHttpMessageHandlerFactory(handler));
        }

        private static HttpResponseMessage CreateResponse()
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }

        private static async Task<string> RunActivityAsync(
            HttpClient client,
            TimeSpan? timeout,
            string path = "request",
            DurableHttpRequest request = null,
            Func<HttpClient> pollingClientFactory = null)
        {
            var options = new DurableTaskOptions();
            options.StorageProvider["type"] = "Emulator";
            using var extension = new DurableTaskExtension(
                new OptionsWrapper<DurableTaskOptions>(options),
                NullLoggerFactory.Instance,
                TestHelpers.GetTestNameResolver(),
                new[] { new EmulatorDurabilityProviderFactory() },
                new TestHostShutdownNotificationService(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService());
            request ??= new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("http://localhost/" + path),
                timeout: timeout);
            var activity = new TaskHttpActivityShim(extension, client, pollingClientFactory);
            return await activity.RunAsync(null, extension.MessageDataConverter.Serialize(new[] { request }));
        }

        private sealed class CustomHandlerFactory : IDurableHttpMessageHandlerFactory
        {
            public HttpMessageHandler CreateHttpMessageHandler()
            {
                throw new InvalidOperationException("Unexpected factory invocation.");
            }
        }

        private sealed class CallbackHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback;

            public CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
            {
                this.callback = callback;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return this.callback(request, cancellationToken);
            }
        }
    }
}
