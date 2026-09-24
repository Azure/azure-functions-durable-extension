// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Moq.Protected;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class TaskOrchestrationContextExtensionMethodsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallHttpAsync_PollingAttemptsAreDeterministicAndPerCall(bool replay)
    {
        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var context = new Mock<TaskOrchestrationContext> { CallBase = true };
        context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(loggerFactory.Object);
        context.SetupGet(c => c.IsReplaying).Returns(replay);
        context.SetupGet(c => c.CurrentUtcDateTime).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        context.Setup(c => c.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var inputs = new List<JsonElement>();
        context.Setup(c => c.CallActivityAsync<DurableHttpResponse>(
                It.Is<TaskName>(name => name.Name == "BuiltIn::HttpActivity"), It.IsAny<object>(), It.IsAny<TaskOptions>()))
            .Returns((TaskName name, object input, TaskOptions options) =>
            {
                inputs.Add(JsonSerializer.SerializeToElement(Assert.IsType<DurableHttpRequest>(input)));
                return Task.FromResult(new DurableHttpResponse(inputs.Count % 3 == 0 ? HttpStatusCode.OK : HttpStatusCode.Accepted)
                {
                    Headers = new Dictionary<string, StringValues> { ["Location"] = "/status?code=secret", ["Retry-After"] = "0" },
                });
            });
        var request = new DurableHttpRequest(HttpMethod.Post, new Uri("https://example.com/start"))
        {
            AsynchronousPatternEnabled = true,
        };

        for (int call = 0; call < 2; call++)
        {
            DurableHttpResponse response = await context.Object.CallHttpAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(new[] { 0, 1, 2, 0, 1, 2 }, inputs.Select(input =>
            input.TryGetProperty("pollingAttempt", out JsonElement attempt) ? attempt.GetInt32() : 0));
        Assert.Equal("https://example.com/status?code=secret", inputs[1].GetProperty("uri").GetString());
        Assert.False(JsonSerializer.SerializeToElement(request).TryGetProperty("pollingAttempt", out _));
        context.Verify(c => c.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        logger.Verify(log => log.Log(
            LogLevel.Information, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, type) => state.ToString() == "Polling HTTP status at location: https://example.com/status"),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(replay ? 0 : 4));
    }

    [Theory]
    [InlineData("https://username:password@poll.example.com:9443/status/a%20b?code=secret&sig=signature#fragment", "https://poll.example.com:9443/status/a%20b")]
    [InlineData("/status/a%20b?code=secret#fragment", "https://example.com:8443/status/a%20b")]
    [InlineData("../status/a%2Fb?code=secret#fragment", "https://example.com:8443/status/a%2Fb")]
    [InlineData("https://[::1]:9443/status%20name?code=secret#fragment", "https://[::1]:9443/status%20name")]
    public async Task CallHttpAsync_DoesNotLogRawLocation(string location, string expectedEndpoint)
    {
        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var context = new Mock<TaskOrchestrationContext> { CallBase = true };
        context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(loggerFactory.Object);
        context.Setup(c => c.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Assert.Empty(logger.Invocations);
                return Task.CompletedTask;
            });
        context.SetupSequence(c => c.CallActivityAsync<DurableHttpResponse>(
                It.IsAny<TaskName>(), It.IsAny<object>(), It.IsAny<TaskOptions>()))
            .ReturnsAsync(new DurableHttpResponse(HttpStatusCode.Accepted)
            {
                Headers = new Dictionary<string, StringValues>
                {
                    ["Location"] = location,
                    ["Retry-After"] = "0",
                },
            })
            .Returns(() =>
            {
                // The worker log describes scheduling, before the poll has produced a response.
                var log = Assert.Single(logger.Invocations, invocation => invocation.Method.Name == nameof(ILogger.Log));
                Assert.Equal(LogLevel.Information, log.Arguments[0]);
                Assert.Equal("Polling HTTP status at location: " + expectedEndpoint, log.Arguments[2].ToString());
                var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object>>>(log.Arguments[2]);
                foreach (string sensitive in new[] { "username", "password", "secret", "signature", "fragment", "?" })
                {
                    Assert.DoesNotContain(sensitive, JsonSerializer.Serialize(state));
                }

                return Task.FromResult(new DurableHttpResponse(HttpStatusCode.OK));
            });

        await context.Object.CallHttpAsync(new DurableHttpRequest(HttpMethod.Get, new Uri("https://example.com:8443/start"))
        {
            AsynchronousPatternEnabled = true,
        });

        loggerFactory.Verify(factory => factory.CreateLogger("Microsoft.Azure.Functions.Worker.Extensions.DurableTask.CallHttp"), Times.Once);
        logger.Verify(log => log.Log(
            It.IsAny<LogLevel>(), It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("secret")),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Accepted, false)]
    public async Task CallHttpAsync_WithoutPolling_DoesNotLogPolling(HttpStatusCode statusCode, bool asynchronousPatternEnabled)
    {
        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var context = new Mock<TaskOrchestrationContext> { CallBase = true };
        context.Protected().SetupGet<ILoggerFactory>("LoggerFactory").Returns(loggerFactory.Object);
        context.Setup(c => c.CallActivityAsync<DurableHttpResponse>(
                It.IsAny<TaskName>(), It.IsAny<object>(), It.IsAny<TaskOptions>()))
            .ReturnsAsync(new DurableHttpResponse(statusCode));

        DurableHttpResponse response = await context.Object.CallHttpAsync(
            new DurableHttpRequest(HttpMethod.Get, new Uri("https://example.com/start?code=secret"))
            {
                AsynchronousPatternEnabled = asynchronousPatternEnabled,
            });

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Empty(logger.Invocations);
        context.Verify(c => c.CallActivityAsync<DurableHttpResponse>(
            It.IsAny<TaskName>(), It.IsAny<object>(), It.IsAny<TaskOptions>()), Times.Once);
        context.Verify(c => c.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetFunctionContext_WithNullContext_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TaskOrchestrationContextExtensionMethods.GetFunctionContext(null!));
    }

    [Fact]
    public void GetFunctionContext_WithFunctionsOrchestrationContext_ShouldReturnFunctionContext()
    {
        FunctionContext expectedFunctionContext = CreateMockFunctionContext();
        TaskOrchestrationContext innerContext = CreateMockTaskOrchestrationContext();
        FunctionsOrchestrationContext functionsContext = CreateFunctionsOrchestrationContext(innerContext, expectedFunctionContext);

        FunctionContext? result = functionsContext.GetFunctionContext();

        Assert.NotNull(result);
        Assert.Same(expectedFunctionContext, result);
    }

    [Fact]
    public void GetFunctionContext_WithNonFunctionsOrchestrationContext_ShouldReturnNull()
    {
        TaskOrchestrationContext context = CreateMockTaskOrchestrationContext();

        FunctionContext? result = context.GetFunctionContext();

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that <see cref="TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest"/>
    /// strips the Authorization and Cookie headers when a 202 Location header redirects the poll to
    /// a different origin. This guards against a credential-leak vector where an attacker-controlled
    /// first-hop server redirects the async polling loop to a host they control.
    /// </summary>
    [Fact]
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
            asynchronousPatternEnabled: true);

        DurableHttpRequest poll = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original,
            "https://attacker.example.com/steal");

        Assert.Equal(new Uri("https://attacker.example.com/steal"), poll.Uri);
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
    public void CreateLocationPollRequest_SameOrigin_ForwardsHeaders()
    {
        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            { "Authorization", "Bearer original-token" },
            { "Cookie", "session=abc123" },
            { "x-functions-key", "secret-key" },
            { "Accept", "application/json" },
        };

        var original = new DurableHttpRequest(
            method: HttpMethod.Get,
            uri: new Uri("https://management.azure.com/start"),
            headers: headers,
            asynchronousPatternEnabled: true);

        DurableHttpRequest poll = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original,
            "https://management.azure.com/poll");

        Assert.NotNull(poll.Headers);
        Assert.Equal("Bearer original-token", poll.Headers["Authorization"]);
        Assert.Equal("session=abc123", poll.Headers["Cookie"]);
        Assert.False(poll.Headers.ContainsKey("x-functions-key"));
        Assert.Equal("application/json", poll.Headers["Accept"]);
    }

    /// <summary>
    /// Verifies that headers on the poll request are a defensive copy: stripping credentials
    /// on the new request must not mutate the original request's headers (the poll loop reuses
    /// the original request as the basis for each iteration).
    /// </summary>
    [Fact]
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
            headers: headers,
            asynchronousPatternEnabled: true);

        TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original,
            "https://attacker.example.com/steal");

        Assert.NotNull(original.Headers);
        Assert.True(original.Headers.ContainsKey("Authorization"));
        Assert.True(original.Headers.ContainsKey("Cookie"));
    }

    /// <summary>
    /// Verifies the same-origin policy used to decide whether to forward credentials across
    /// a 202 Location redirect. Origin is scheme + host + port, with case-insensitive host
    /// comparison. Asserted through the <see cref="TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest"/>
    /// helper by observing whether the Authorization header is forwarded.
    /// </summary>
    [Theory]
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
            headers: headers,
            asynchronousPatternEnabled: true);

        DurableHttpRequest poll = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
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

    private static FunctionContext CreateMockFunctionContext()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        serviceCollection.AddSingleton(Options.Create(new DurableTaskWorkerOptions()));
        serviceCollection.AddSingleton(Options.Create(new JsonSerializerOptions()));
        IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        var mockFunctionContext = new Mock<FunctionContext>();
        mockFunctionContext.Setup(c => c.InstanceServices).Returns(serviceProvider);

        return mockFunctionContext.Object;
    }

    private static TaskOrchestrationContext CreateMockTaskOrchestrationContext()
    {
        var mockContext = new Mock<TaskOrchestrationContext>();
        mockContext.Setup(c => c.Name).Returns(new TaskName("TestOrchestration"));
        mockContext.Setup(c => c.InstanceId).Returns("test-instance-id");
        mockContext.Setup(c => c.CurrentUtcDateTime).Returns(DateTime.UtcNow);
        mockContext.Setup(c => c.IsReplaying).Returns(false);
        mockContext.Setup(c => c.Properties).Returns(new Dictionary<string, object?>());

        return mockContext.Object;
    }

    private static FunctionsOrchestrationContext CreateFunctionsOrchestrationContext(
        TaskOrchestrationContext innerContext,
        FunctionContext functionContext)
    {
        return new FunctionsOrchestrationContext(innerContext, functionContext);
    }
}
