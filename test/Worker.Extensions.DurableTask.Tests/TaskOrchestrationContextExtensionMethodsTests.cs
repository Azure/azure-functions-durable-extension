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

namespace Microsoft.Azure.Functions.Worker.Tests;

public class TaskOrchestrationContextExtensionMethodsTests
{
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

    [Fact]
    public void CreateLocationPollRequest_CrossOrigin_StripsAllHeaders()
    {
        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            { "Authorization", "Bearer original-token" },
            { "Cookie", "session=abc123; auth=xyz" },
            { "X-Functions-Key", "secret-key" },
            { "api-key", "api-secret" },
            { "X-Custom-Credential", "custom-secret" },
            { "X-Correlation-Id", "request-id" },
            { "Accept", "application/json" },
        };

        var original = new DurableHttpRequest(
            method: HttpMethod.Get,
            uri: new Uri("https://management.azure.com/some/resource"),
            headers: headers,
            asynchronousPatternEnabled: true);
        original.TokenSource = new ManagedIdentityTokenSource("https://management.azure.com/");
        original.EnableCrossOriginHeaderFiltering = true;

        DurableHttpRequest poll = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original,
            "https://attacker.example.com/steal");

        Assert.Equal(new Uri("https://attacker.example.com/steal"), poll.Uri);
        Assert.NotNull(poll.Headers);
        Assert.Empty(poll.Headers);
        Assert.Null(poll.TokenSource);
        Assert.Equal(HttpMethod.Get, poll.Method);
        Assert.True(poll.AsynchronousPatternEnabled);
    }

    [Fact]
    public void CreateLocationPollRequest_SameOrigin_ForwardsHeaders()
    {
        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            { "Authorization", "Bearer original-token" },
            { "Cookie", "session=abc123" },
            { "X-Functions-Key", "secret-key" },
            { "api-key", "api-secret" },
            { "X-Custom-Credential", "custom-secret" },
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
        Assert.Equal("api-secret", poll.Headers["api-key"]);
        Assert.Equal("custom-secret", poll.Headers["X-Custom-Credential"]);
        Assert.Equal(headers.Count - 1, poll.Headers.Count);
    }

    [Theory]
    [InlineData("https://management.azure.com/poll")]
    [InlineData("https://other.example.com/poll")]
    public void CreateLocationPollRequest_DoesNotMutateOriginalHeaders(string locationUri)
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

        DurableHttpRequest poll = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original,
            locationUri);

        Assert.NotNull(poll.Headers);
        Assert.NotSame(original.Headers, poll.Headers);
        poll.Headers.Clear();
        Assert.NotNull(original.Headers);
        Assert.True(original.Headers.ContainsKey("Authorization"));
        Assert.True(original.Headers.ContainsKey("Cookie"));
    }

    [Theory]
    [InlineData("https://example.com/start", "https://example.com/poll", true)]
    [InlineData("https://Example.COM/start", "https://example.com/poll", true)]
    [InlineData("HTTPS://example.com/start", "https://example.com:443/poll", true)]
    [InlineData("https://b\u00FCcher.example/start", "https://xn--bcher-kva.example/poll", true)]
    [InlineData("https://xn--bcher-kva.example/start", "https://b\u00FCcher.example/poll", true)]
    [InlineData("https://b\u00FCcher.example/start", "https://b\u00FCcher-.example/poll", false)]
    [InlineData("https://example.com/start", "https://example.com:8443/poll", false)]
    [InlineData("https://example.com:443/start", "https://example.com:8443/poll", false)]
    [InlineData("https://example.com/start", "http://example.com/poll", false)]
    [InlineData("https://example.com/start", "https://attacker.example.com/poll", false)]
    [InlineData("https://example.com/start", "/poll", true)]
    [InlineData("https://example.com/start", "poll", true)]
    [InlineData("https://example.com/start", "//other.example.com/poll", false)]
    public void CreateLocationPollRequest_OriginComparison(string originalUri, string locationUri, bool expectHeadersForwarded)
    {
        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            { "X-Custom-Credential", "custom-secret" },
            { "Accept", "application/json" },
            { "Authorization", "Bearer original-token" },
            { "Cookie", "session=abc123" },
        };

        var original = new DurableHttpRequest(
            method: HttpMethod.Get,
            uri: new Uri(originalUri),
            headers: headers,
            asynchronousPatternEnabled: true);
        original.EnableCrossOriginHeaderFiltering = true;

        DurableHttpRequest poll = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original,
            locationUri);

        Assert.NotNull(poll.Headers);
        if (expectHeadersForwarded)
        {
            Assert.Equal(headers, poll.Headers);
        }
        else
        {
            Assert.Empty(poll.Headers);
        }
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("aUtHoRiZaTiOn")]
    [InlineData("cOoKiE")]
    [InlineData("API-KEY")]
    [InlineData("X-Arbitrary-Credential")]
    [InlineData("Accept")]
    public void CreateLocationPollRequest_HeaderNames(string headerName)
    {
        var headers = new Dictionary<string, StringValues>
        {
            { headerName, "test-value" },
            { "X-FuNcTiOnS-kEy", "function-key" },
        };
        var original = new DurableHttpRequest(HttpMethod.Post, new Uri("https://example.com/start"), headers, true);
        original.EnableCrossOriginHeaderFiltering = true;

        DurableHttpRequest sameOrigin = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original, "/poll");
        DurableHttpRequest crossOrigin = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(
            original, "https://other.example.com/poll");

        Assert.NotNull(sameOrigin.Headers);
        Assert.Single(sameOrigin.Headers);
        Assert.Equal("test-value", sameOrigin.Headers[headerName]);
        Assert.NotNull(crossOrigin.Headers);
        Assert.Empty(crossOrigin.Headers);
        Assert.Equal(2, original.Headers!.Count);
        Assert.Equal("function-key", original.Headers["X-FuNcTiOnS-kEy"]);
        Assert.Equal("test-value", original.Headers[headerName]);
    }

    [Theory]
    [InlineData("https://example.com/poll")]
    [InlineData("https://other.example.com/poll")]
    public void CreateLocationPollRequest_WithoutHeaders(string locationUri)
    {
        var original = new DurableHttpRequest(HttpMethod.Post, new Uri("https://example.com/start"));

        DurableHttpRequest poll = TaskOrchestrationContextExtensionMethods.CreateLocationPollRequest(original, locationUri);

        Assert.True(poll.Headers is null || poll.Headers.Count == 0);
        Assert.Null(original.Headers);
    }

    [Theory]
    [InlineData("https://other.example.com/poll/2", false, false)]
    [InlineData("https://third.example.com/poll", false, false)]
    [InlineData("https://example.com/poll", true, false)]
    [InlineData("https://other.example.com/poll/2", true, true)]
    [InlineData("https://third.example.com/poll", false, true)]
    [InlineData("https://example.com/poll", true, true)]
    public async Task CallHttpAsync_RepeatedPolling_UsesOriginalOrigin(string nextLocation, bool expectHeadersForwarded, bool configured)
    {
        var headers = new Dictionary<string, StringValues>
        {
            { "X-Custom-Credential", "custom-secret" },
            { "Accept", "application/json" },
        };
        var original = new DurableHttpRequest(HttpMethod.Post, new Uri("https://example.com/start"), headers, true);
        original.EnableCrossOriginHeaderFiltering = true;
        if (configured)
        {
            original.CrossOriginPollingHeaders = new Dictionary<string, string[]>
            {
                { "https://other.example.com", new[] { "x-custom-credential" } },
            };
        }

        var responses = new Queue<DurableHttpResponse>(new[]
        {
            new DurableHttpResponse(HttpStatusCode.Accepted)
            {
                Headers = new Dictionary<string, StringValues> { { "Location", "https://other.example.com/poll/1" } },
            },
            new DurableHttpResponse(HttpStatusCode.Accepted)
            {
                Headers = new Dictionary<string, StringValues> { { "Location", nextLocation } },
            },
            new DurableHttpResponse(HttpStatusCode.OK),
        });
        var requests = new List<DurableHttpRequest>();
        var context = new Mock<TaskOrchestrationContext>();
        context.Setup(c => c.CreateReplaySafeLogger(It.IsAny<string>())).Returns(NullLogger.Instance);
        context.Setup(c => c.Properties).Returns(new Dictionary<string, object?>());
        context.Setup(c => c.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Setup(c => c.CallActivityAsync<DurableHttpResponse>(
            Constants.HttpTaskActivityReservedName, It.IsAny<object>(), It.IsAny<TaskOptions>()))
            .Callback<TaskName, object?, TaskOptions?>((_, input, _) => requests.Add(Assert.IsType<DurableHttpRequest>(input)))
            .ReturnsAsync(() => responses.Dequeue());

        DurableHttpResponse response = await context.Object.CallHttpAsync(original);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, requests.Count);
        Assert.Same(original, requests[0]);
        DurableHttpRequest firstPoll = requests[1];
        DurableHttpRequest nextPoll = requests[2];
        Assert.Equal(new Uri("https://other.example.com/poll/1"), firstPoll.Uri);
        Assert.Equal(new Uri(nextLocation), nextPoll.Uri);
        Assert.NotNull(firstPoll.Headers);
        Assert.Equal(configured ? 1 : 0, firstPoll.Headers.Count);
        Assert.NotNull(nextPoll.Headers);
        if (expectHeadersForwarded)
        {
            if (nextPoll.Uri.Host == original.Uri.Host)
            {
                Assert.Equal(headers, nextPoll.Headers);
            }
            else
            {
                Assert.Single(nextPoll.Headers);
                Assert.Equal("custom-secret", nextPoll.Headers["X-Custom-Credential"]);
            }
        }
        else
        {
            Assert.Empty(nextPoll.Headers);
        }

        Assert.Equal(headers, original.Headers);
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
