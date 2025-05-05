// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit.Abstractions;
using Xunit;
using System.Diagnostics;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class DistributedTracingTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ActivityListener _activityListener;

    public DistributedTracingTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;

        // Initialize the ActivityListener here
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "DistributedTracingTests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => { /* Handle activity started */ },
            ActivityStopped = activity => { /* Handle activity stopped */ }
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Distributed tracing is currently not working in DTS
    public async Task DistributedTracingTest()
    {
        // Start Activity
        ActivitySource activitySource = new ActivitySource("DistributedTracingTests");
        using Activity? activity = activitySource.StartActivity("HttpTriggerTests");

        Assert.NotNull(activity);

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("DistributedTracing_HttpStart", "");

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);
        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        string output = orchestrationDetails.Output;
        ActivityContext.TryParse(output, null, out ActivityContext activityContext);

        Assert.Equal(activity?.TraceId.ToString(), activityContext.TraceId.ToString());
    }
}