// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
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

        private static async Task<string> RunActivityAsync(HttpClient client, TimeSpan? timeout, string path = "request")
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
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("http://localhost/" + path),
                timeout: timeout);
            var activity = new TaskHttpActivityShim(extension, client);
            return await activity.RunAsync(null, extension.MessageDataConverter.Serialize(new[] { request }));
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
