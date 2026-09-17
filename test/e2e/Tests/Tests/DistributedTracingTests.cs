// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit.Abstractions;
using Xunit;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class DistributedTracingTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;
    private readonly ActivityListener activityListener;

    public DistributedTracingTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;

        // Initialize the ActivityListener here
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "DistributedTracingTests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { /* Handle activity started */ },
            ActivityStopped = activity => { /* Handle activity stopped */ }
        };
        ActivitySource.AddActivityListener(this.activityListener);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Distributed tracing is currently not working in DTS
    [Trait("PowerShell", "Skip")] // Distributed tracing is currently not implemented in PowerShell
    [Trait("Python", "Skip")] // Distributed tracing is not currently implemented in Python
    [Trait("Node", "Skip")] // Remove after durable-functions releases native invocation-context propagation
    [Trait("Java", "Skip")] // Distributed tracing is not currently implemented in Java
    public async Task DistributedTracingTest()
    {
        // Start Activity
        ActivitySource activitySource = new ActivitySource("DistributedTracingTests");
        using Activity? activity = activitySource.StartActivity("HttpTriggerTests");

        Assert.NotNull(activity);
        const string TraceState = "congo=t61rcWkgMzE";
        activity.TraceStateString = TraceState;

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=DistributedTracing");

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);
        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        string output = orchestrationDetails.Output;

        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Node)
        {
            JsonNode? invocationTraceContext = JsonNode.Parse(output);
            Assert.NotNull(invocationTraceContext);
            AssertActiveInvocationContext(invocationTraceContext["orchestration"], activity.TraceId, TraceState);
            AssertActiveInvocationContext(invocationTraceContext["activity"], activity.TraceId, TraceState);
        }
        else
        {
            Assert.True(ActivityContext.TryParse(output, null, out ActivityContext activityContext));
            Assert.Equal(activity.TraceId, activityContext.TraceId);
        }
    }

    private static void AssertActiveInvocationContext(
        JsonNode? invocationTraceContext,
        ActivityTraceId expectedTraceId,
        string expectedTraceState)
    {
        Assert.NotNull(invocationTraceContext);
        string? traceParent = invocationTraceContext["traceParent"]?.GetValue<string>();
        string? traceState = invocationTraceContext["traceState"]?.GetValue<string>();
        string? activeTraceId = invocationTraceContext["activeTraceId"]?.GetValue<string>();
        string? activeSpanId = invocationTraceContext["activeSpanId"]?.GetValue<string>();

        Assert.False(string.IsNullOrEmpty(traceParent));
        Assert.True(ActivityContext.TryParse(traceParent, traceState, out ActivityContext activityContext));
        Assert.Equal(expectedTraceId, activityContext.TraceId);
        Assert.NotEqual(default, activityContext.SpanId);
        Assert.Equal(ActivityTraceFlags.Recorded, activityContext.TraceFlags);
        Assert.Equal(expectedTraceState, traceState);
        Assert.Equal(activityContext.TraceId.ToString(), activeTraceId);
        Assert.Equal(activityContext.SpanId.ToString(), activeSpanId);
    }
}