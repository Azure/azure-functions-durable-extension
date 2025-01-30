using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class PurgeInstancesTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PurgeInstancesTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    // Due to some kind of asynchronous race condition in XUnit, when running these tests in pipelines,
    // the output may be disposed before the message is written. Just ignore these types of errors for now. 
    private void WriteOutput(string message)
    {
        try
        {
            _output.WriteLine(message);
        }
        catch
        {
            // Ignore
        }
    }

    [Theory]
    [InlineData("PurgeOrchestrationHistory", HttpStatusCode.OK, @"^Purged [0-9]* records$")]
    public async Task HttpTriggerTests(string functionName, HttpStatusCode expectedStatusCode, string responseRegex)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, "");
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(responseRegex, actualMessage);
        Assert.Equal(expectedStatusCode, response.StatusCode);
    }
}
