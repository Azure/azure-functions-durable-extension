// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TaskHttpActivityShimTests
    {
        private readonly ITestOutputHelper output;

        public TaskHttpActivityShimTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(0, false)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        public async Task LogsSanitizedRequestBeforeEverySend(int pollingAttempt, bool fail)
        {
            string instanceId = Guid.NewGuid().ToString();
            var events = new ConcurrentQueue<EventWrittenEventArgs>();
            using var listener = new HttpRequestEventListener();
            listener.EventWritten += (_, args) =>
            {
                if (args.EventId == 213 && args.Payload.Contains(instanceId))
                {
                    events.Enqueue(args);
                }
            };
            listener.EnableEvents(EtwEventSource.Instance, EventLevel.Informational);
            var loggerProvider = new TestLoggerProvider(this.output);
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            using DurableTaskExtension extension = CreateExtension(loggerFactory, traceInputsAndOutputs: true);
            var request = new DurableHttpRequest(
                HttpMethod.Post,
                new Uri("https://user:password@example.com:8443/jobs/start?code=secret&taskHub=private#fragment"),
                headers: new Dictionary<string, StringValues> { ["Authorization"] = "Bearer token" },
                content: "private body");
            JObject requestJson = JObject.FromObject(request);
            requestJson["pollingAttempt"] = pollingAttempt;
            string input = new JArray(requestJson).ToString();
            int sends = 0;
            using var handler = new CallbackHandler((actual, _) =>
            {
                sends++;
                LogMessage message = loggerProvider.GetAllLogMessages()
                    .Last(log => log.FormattedMessage.Contains("Sending HTTP request"));
                var state = message.State.ToDictionary(pair => pair.Key, pair => pair.Value);
                Assert.Equal(LogLevel.Information, message.Level);
                Assert.Equal(TestHelpers.LogCategory, message.Category);
                Assert.Equal("POST", state["httpMethod"]);
                Assert.Equal("https://example.com:8443/jobs/start", state["requestUri"]);
                Assert.Equal("code, taskHub", state["queryParameterNames"]);
                Assert.Equal(pollingAttempt, state["pollingAttempt"]);
                Assert.Equal(instanceId, state["instanceId"]);
                foreach (string sensitive in new[] { "password", "secret", "private", "fragment", "Bearer token" })
                {
                    Assert.DoesNotContain(sensitive, message.FormattedMessage);
                    Assert.DoesNotContain(sensitive, JsonConvert.SerializeObject(state));
                }

                Assert.Equal(request.Uri, actual.RequestUri);
                Assert.Equal("Bearer token", actual.Headers.Authorization.ToString());
                Assert.Equal(new[] { "Authorization" }, actual.Headers.Select(header => header.Key));
                if (fail)
                {
                    throw new HttpRequestException("network unavailable");
                }

                return Task.FromResult(CreateResponse());
            });
            using var client = new HttpClient(handler);
            var activity = new TaskHttpActivityShim(extension, client);
            var context = new TaskContext(new OrchestrationInstance { InstanceId = instanceId });

            // Retries/redeliveries re-execute the same serialized activity input.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (fail)
                {
                    await Assert.ThrowsAsync<TaskFailureException>(() => activity.RunAsync(context, input));
                }
                else
                {
                    await activity.RunAsync(context, input);
                }
            }

            Assert.Equal(2, sends);
            Assert.Equal(2, loggerProvider.GetAllLogMessages().Count(log => log.FormattedMessage.Contains("Sending HTTP request")));
            Assert.Equal(2, events.Count);
            Assert.All(events, entry =>
            {
                string details = (string)entry.Payload[entry.PayloadNames.IndexOf("Details")];
                Assert.Contains("https://example.com:8443/jobs/start", details);
                Assert.Contains("code, taskHub", details);
                Assert.Contains($"PollingAttempt: {pollingAttempt}", details);
                foreach (string sensitive in new[] { "user", "password", "secret", "private", "fragment", "Bearer token" })
                {
                    Assert.DoesNotContain(sensitive, details);
                }
            });
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("https://example.com", "https://example.com/", "")]
        [InlineData("https://example.com/path?code=a=b&code=x&flag&empty=&=hidden&&", "https://example.com/path", "code, code, flag, empty")]
        [InlineData("https://example.com/a%20b?encoded%26key=secret&line%0Abreak=secret", "https://example.com/a%20b", "encoded%26key, line%0Abreak")]
        [InlineData("https://user:password@[::1]:8443/path?sig=secret#fragment", "https://[::1]:8443/path", "sig")]
        public async Task RequestLogPreservesOnlyEscapedEndpointAndQueryNames(string uri, string expectedUri, string expectedNames)
        {
            var loggerProvider = new TestLoggerProvider(this.output);
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            using DurableTaskExtension extension = CreateExtension(loggerFactory);
            using var handler = new CallbackHandler((_, _) => Task.FromResult(CreateResponse()));
            using var client = new HttpClient(handler);
            var request = new DurableHttpRequest(HttpMethod.Get, new Uri(uri));
            var activity = new TaskHttpActivityShim(extension, client);

            await activity.RunAsync(null, extension.MessageDataConverter.Serialize(new[] { request }));

            LogMessage message = Assert.Single(loggerProvider.GetAllLogMessages(), log => log.FormattedMessage.Contains("Sending HTTP request"));
            var state = message.State.ToDictionary(pair => pair.Key, pair => pair.Value);
            Assert.Equal(expectedUri, state["requestUri"]);
            Assert.Equal(expectedNames, state["queryParameterNames"]);
            Assert.Equal(0, state["pollingAttempt"]);
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

        private static async Task<string> RunActivityAsync(HttpClient client, TimeSpan? timeout, string path = "request")
        {
            using DurableTaskExtension extension = CreateExtension(NullLoggerFactory.Instance);
            var request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri("http://localhost/" + path),
                timeout: timeout);
            var activity = new TaskHttpActivityShim(extension, client);
            return await activity.RunAsync(null, extension.MessageDataConverter.Serialize(new[] { request }));
        }

        private static DurableTaskExtension CreateExtension(ILoggerFactory loggerFactory, bool traceInputsAndOutputs = false)
        {
            var options = new DurableTaskOptions();
            options.Tracing.TraceInputsAndOutputs = traceInputsAndOutputs;
            options.StorageProvider["type"] = "Emulator";
            return new DurableTaskExtension(
                new OptionsWrapper<DurableTaskOptions>(options),
                loggerFactory,
                TestHelpers.GetTestNameResolver(),
                new[] { new EmulatorDurabilityProviderFactory() },
                new TestHostShutdownNotificationService(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService());
        }

        private sealed class HttpRequestEventListener : EventListener
        {
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
