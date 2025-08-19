using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using Xunit;

namespace IsolatedUnitTest.Tests;

public class HelloCitiesOrchestrationTests
{
    private readonly Mock<ILogger> testLogger;
    private readonly Mock<FunctionContext> functionContextMock;

    public HelloCitiesOrchestrationTests()
    {
        testLogger = new Mock<ILogger>();
        functionContextMock = new Mock<FunctionContext>();
    }

    [Fact]
    // Unit test for Orchestrator HelloCitiesOrchestration.HttpCities.
    public async Task HelloCitiesOrchestration_ReturnsExpectedGreetings()
    {
        // Mock TaskOrchestrationContext and setup logger.
        var contextMock = new Mock<TaskOrchestrationContext>();
        contextMock.Setup(x => x.CreateReplaySafeLogger(It.IsAny<string>()))
            .Returns(testLogger.Object);

        // Mock the activity function calls
        contextMock.Setup(x => x.CallActivityAsync<string>(
            It.Is<TaskName>(n => n.Name == nameof(HelloCitiesOrchestration.SayHello)),
            It.Is<string>(n => n == "Tokyo"),
            It.IsAny<TaskOptions>()))
            .ReturnsAsync("Hello Tokyo!");
        contextMock.Setup(x => x.CallActivityAsync<string>(
            It.Is<TaskName>(n => n.Name == nameof(HelloCitiesOrchestration.SayHello)),
            It.Is<string>(n => n == "Seattle"),
            It.IsAny<TaskOptions>()))
            .ReturnsAsync("Hello Seattle!");
        contextMock.Setup(x => x.CallActivityAsync<string>(
            It.Is<TaskName>(n => n.Name == nameof(HelloCitiesOrchestration.SayHello)),
            It.Is<string>(n => n == "London"),
            It.IsAny<TaskOptions>()))
            .ReturnsAsync("Hello London!");

        var result = await HelloCitiesOrchestration.HelloCities(contextMock.Object);

        // Verify the orchestration result.
        Assert.Equal(3, result.Count);
        Assert.Equal("Hello Tokyo!", result[0]);
        Assert.Equal("Hello Seattle!", result[1]);
        Assert.Equal("Hello London!", result[2]);

        // Verify logging.
        testLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Saying hello")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    // Unit test for HelloCitiesOrchestration.SayHello.
    public void SayHello_ReturnsExpectedGreeting()
    {
        const string name = "Tokyo";

        var result = HelloCitiesOrchestration.SayHello(name, functionContextMock.Object);

        // Verify the activity function SayHello returns the right result. 
        Assert.Equal($"Hello {name}!", result);
    }

    [Fact]
    // Unit Test for HelloCitiesOrchestration.HttpStart
    public async Task HttpStart_ReturnsExpectedResponse()
    {
        var instanceId = Guid.NewGuid().ToString();

        // Mock DurableTaskClient. 
        var durableClientMock = new Mock<DurableTaskClient>("testClient");
        durableClientMock
            .Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
            It.IsAny<TaskName>(),
            It.IsAny<object>(),
            It.IsAny<StartOrchestrationOptions>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(instanceId);

        // Mock HttpRequestData that sent to the http trigger. 
        var mockRequest = MockHttpRequestAndResponseData();

        var responseMock = new Mock<HttpResponseData>(functionContextMock.Object);
        responseMock.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.Accepted);

        var result = await HelloCitiesOrchestration.HttpStart(mockRequest, durableClientMock.Object, functionContextMock.Object);

        // Verify the status code.
        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);

        // Reset stream position for reading
        result.Body.Position = 0;
        var serializedResponseBody = await System.Text.Json.JsonSerializer.DeserializeAsync<dynamic>(result.Body);

        // Verify the response returned contains the right data. 
        Assert.Equal(instanceId, serializedResponseBody!.GetProperty("Id").GetString());
    }

    // Method to mock the HttpRequestData.
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
        mockHttpRequestData.SetupGet(r => r.Url).Returns(new Uri("https://localhost:7075/orchestrators/HelloCities"));

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
