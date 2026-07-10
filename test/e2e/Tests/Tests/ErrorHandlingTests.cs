// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Microsoft.DurableTask;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class ErrorHandlingTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public ErrorHandlingTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    [Trait("MSSQL", "Skip")] // This test fails for MSSQL unless this bug is fixed: https://github.com/microsoft/durabletask-mssql/issues/287
    [Trait("DTS", "Skip")] // DTS will fail this test unless this bug is fixed: https://msazure.visualstudio.com/Antares/_workitems/edit/31779638
    public async Task OrchestratorWithUncaughtActivityException_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=RethrowActivityException");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        
        Assert.StartsWith(this.fixture.functionLanguageLocalizer?.GetLocalizedStringValue("RethrownActivityException.ErrorMessage"), orchestrationDetails.Output);
        Assert.Contains("This activity failed", orchestrationDetails.Output);
    }

    [Fact]
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL/Dotnet Isolated, see https://github.com/microsoft/durabletask-mssql/issues/205
    [Trait("DTS", "Skip")] // DTS will fail this test unless this bug is fixed: https://msazure.visualstudio.com/Antares/_workitems/edit/31779638
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    public async Task OrchestratorWithUncaughtEntityException_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=ThrowEntityOrchestration");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        
        Assert.StartsWith(this.fixture.functionLanguageLocalizer.GetLocalizedStringValue("RethrownEntityException.ErrorMessage"), orchestrationDetails.Output);
        // Bug: https://github.com/Azure/azure-functions-durable-js/issues/642
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.Node)
        {
            Assert.Contains("This entity failed", orchestrationDetails.Output);
        }
    }

    [Fact]
    [Trait("PowerShell-MSSQL", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/98
    [Trait("PowerShell-DTS", "Skip")] // Same bug as above
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/562
    [Trait("Node-DTS", "Skip")] // Bug: https://msazure.visualstudio.com/Antares/_workitems/edit/33910424
    public async Task OrchestratorWithCaughtActivityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=CatchActivityException");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.StartsWith(this.fixture.functionLanguageLocalizer?.GetLocalizedStringValue("CaughtActivityException.ErrorMessage"), orchestrationDetails.Output);
        Assert.Contains("This activity failed", orchestrationDetails.Output);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // FailureDetails is a dotnet-isolated implementation detail
    [Trait("Python", "Skip")] // FailureDetails is a dotnet-isolated implementation detail
    [Trait("Node", "Skip")] // FailureDetails is a dotnet-isolated implementation detail
    public async Task OrchestratorWithCaughtActivityExceptionFailureDetails_ContainRightErrorType()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=CatchActivityExceptionFailureDetails");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        // Deserialize the output to FailureDetails
        var failureDetails = JsonConvert.DeserializeObject<TaskFailureDetails>(orchestrationDetails.Output);

        // Check FailureDetails contains the right error type and error message,
        // Here it should be the same one as the activity function Raise Exception throws.
        Assert.NotNull(failureDetails);
        Assert.Contains("InvalidOperationException", failureDetails.ErrorType);
        Assert.Equal("This activity failed", failureDetails.ErrorMessage);
    }

    [Fact]
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL/Dotnet Isolated, see https://github.com/microsoft/durabletask-mssql/issues/205
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    public async Task OrchestratorWithCaughtEntityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=CatchEntityOrchestration");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.StartsWith(this.fixture.functionLanguageLocalizer.GetLocalizedStringValue("CaughtEntityException.ErrorMessage"), orchestrationDetails.Output);
        // Bug: https://github.com/Azure/azure-functions-durable-js/issues/642
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.Node)
        {
            Assert.Contains("This entity failed", orchestrationDetails.Output);
            Assert.Contains("More information about the failure", orchestrationDetails.Output);

            // For now, we deliberately do not return inner exception details on entity failure. 
            // If this changes in the future, update this test. 
            Assert.DoesNotContain("Inner exception message", orchestrationDetails.Output);
        }
    }

    [Fact]
    [Trait("Node-DTS", "Skip")] // Bug: https://msazure.visualstudio.com/Antares/_workitems/edit/33910424
    public async Task OrchestratorWithRetriedActivityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=RetryActivityFunction");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        // Poll for the expected log line to appear
        await this.fixture.TestLogs.AssertLogExistsAsync(
            x => x.Contains(nameof(InvalidOperationException)) && x.Contains("This activity failed"),
            "Expected log containing 'InvalidOperationException' and 'This activity failed' was not found.");
    }

    [Fact]
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL/Dotnet Isolated, see https://github.com/microsoft/durabletask-mssql/issues/205
    [Trait("DTS", "Skip")] // DTS will fail this test unless this issue is fixed, see https://msazure.visualstudio.com/Antares/_workitems/edit/31778744
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    [Trait("Node-DTS", "Skip")] // Bug: https://msazure.visualstudio.com/Antares/_workitems/edit/33910424
    public async Task OrchestratorWithRetriedEntityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=RetryEntityOrchestration");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Python ||
            this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Node)
        {
            // In the ooproc langagues that use the OOProc shim (old method), we redact exception details for entities.
            // For some reason, this includes redacting these details in Core Tools logs - likely a bug (?)
            // Relevant code: EndToEndTraceHelper.cs ~#545
            await this.fixture.TestLogs.AssertLogExistsAsync(
                x => x.Contains("Function 'counter (Entity)' failed 'get' operation") &&
                     (x.Contains("(Redacted 58 characters)") || x.Contains("(Redacted 34 characters)")),
                "Expected redacted entity failure log was not found.");
        }
        else
        {
            // For entities, these logs are not emitted as one continuous log, but each line of the exception .ToString() is
            // logged individually.
            await this.fixture.TestLogs.AssertLogExistsAsync(
                x => x.Contains(nameof(InvalidOperationException)) && x.Contains("This entity failed"),
                "Expected log containing 'InvalidOperationException' and 'This entity failed' was not found.");
            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("More information about the failure"));
            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(OverflowException)) &&
                                                                  x.Contains("Inner exception message"));
        }
    }

    [Fact]
    [Trait("Python", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/561
    [Trait("Node-DTS", "Skip")] // Bug: https://msazure.visualstudio.com/Antares/_workitems/edit/33910424
    public async Task OrchestratorWithCustomRetriedActivityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=CustomRetryActivityFunction");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        // We want to ensure that multiline exception messages and inner exceptions are preserved.
        // Poll for the first expected log line to appear instead of using a fixed delay.
        await this.fixture.TestLogs.AssertLogExistsAsync(
            x => x.Contains(nameof(InvalidOperationException)) && x.Contains("This activity failed"),
            "Expected log containing 'InvalidOperationException' and 'This activity failed' was not found.");
        Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("More information about the failure"));
        Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(OverflowException)) &&
                                                              x.Contains("Inner exception message"));
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // FailureDetails is a dotnet-isolated implementation detail
    [Trait("Python", "Skip")] // FailureDetails is a dotnet-isolated implementation detail
    [Trait("Java", "Skip")] // Include exception properties at Failure Details for Java is not supported yet.
    public async Task CustomExceptionPropertiesInFailureDetails()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=OrchestrationWithCustomException");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);
        
        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        // Deserialize the output to FailureDetails
        var failureDetails = JsonConvert.DeserializeObject<TaskFailureDetails>(orchestrationDetails.Output);

        // Check FailureDetails contains the right error type and error message
        Assert.NotNull(failureDetails);
        Assert.Contains("BusinessValidationException", failureDetails.ErrorType);
        Assert.Equal("Business logic validation failed", failureDetails.ErrorMessage);

        // Check that custom properties are included
        Assert.NotNull(failureDetails.Properties);

        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Node)
        {
            // The durable-functions JS SDK serializes custom exception properties to JSON
            // and the host parses them as protobuf Values. JSON has no distinct int/long type
            // (numbers round-trip as doubles) and no native DateTime (sent as an ISO-8601 string),
            // so the expected .NET types differ from the dotnet-isolated worker.

            // Verify string property
            Assert.True(failureDetails.Properties.ContainsKey("StringProperty"));
            Assert.Equal("validation-error-123", failureDetails.Properties["StringProperty"]);

            // Verify numeric properties (JSON numbers => double)
            Assert.True(failureDetails.Properties.ContainsKey("IntProperty"));
            Assert.Equal(100L, Convert.ToInt64(failureDetails.Properties["IntProperty"]));

            Assert.True(failureDetails.Properties.ContainsKey("LongProperty"));
            Assert.Equal(999999999L, Convert.ToInt64(failureDetails.Properties["LongProperty"]));

            // Verify DateTime property (sent as ISO-8601 string by the JS SDK)
            Assert.True(failureDetails.Properties.ContainsKey("DateTimeProperty"));
            Assert.Equal(
                new DateTime(2025, 10, 15, 14, 30, 0, DateTimeKind.Utc),
                Convert.ToDateTime(failureDetails.Properties["DateTimeProperty"]).ToUniversalTime());

            // Verify dictionary property (nested object)
            Assert.True(failureDetails.Properties.ContainsKey("DictionaryProperty"));
            var nodeDictProperty = JsonConvert.DeserializeObject<Dictionary<string, object>>(failureDetails.Properties["DictionaryProperty"]!.ToString()!);
            Assert.NotNull(nodeDictProperty);
            Assert.Equal("VALIDATION_FAILED", nodeDictProperty["error_code"]);
            Assert.Equal(3L, Convert.ToInt64(nodeDictProperty["retry_count"]));
            Assert.Equal(true, nodeDictProperty["is_critical"]);

            // Verify list property
            Assert.True(failureDetails.Properties.ContainsKey("ListProperty"));
            var nodeListProperty = JsonConvert.DeserializeObject<List<object>>(failureDetails.Properties["ListProperty"]!.ToString()!);
            Assert.NotNull(nodeListProperty);
            Assert.Equal(4, nodeListProperty.Count);
            Assert.Equal("error1", nodeListProperty[0]);
            Assert.Equal("error2", nodeListProperty[1]);
            Assert.Equal(500L, Convert.ToInt64(nodeListProperty[2]));
            Assert.Null(nodeListProperty[3]);

            // Verify null property
            Assert.True(failureDetails.Properties.ContainsKey("NullProperty"));
            Assert.Null(failureDetails.Properties["NullProperty"]);

            return;
        }

        // Verify string property
        Assert.True(failureDetails.Properties.ContainsKey("StringProperty"));
        Assert.Equal("validation-error-123", failureDetails.Properties["StringProperty"]);
        
        // Verify int property
        Assert.True(failureDetails.Properties.ContainsKey("IntProperty"));
        Assert.Equal((long)100, failureDetails.Properties["IntProperty"]);
        
        // Verify long property
        Assert.True(failureDetails.Properties.ContainsKey("LongProperty"));
        Assert.Equal(999999999L, failureDetails.Properties["LongProperty"]);
        
        // Verify DateTime property

        Assert.True(failureDetails.Properties.ContainsKey("DateTimeProperty"));
        Assert.Equal(new DateTime(2025, 10, 15, 14, 30, 0, DateTimeKind.Utc), failureDetails.Properties["DateTimeProperty"]);

        // Verify dictionary property
        Assert.True(failureDetails.Properties.ContainsKey("DictionaryProperty"));
        var dictProperty = JsonConvert.DeserializeObject<Dictionary<string, object>>(failureDetails.Properties["DictionaryProperty"]!.ToString()!);
        Assert.NotNull(dictProperty);
        Assert.Equal("VALIDATION_FAILED", dictProperty["error_code"]);
        Assert.Equal((long)3, dictProperty["retry_count"]);
        Assert.Equal(true, dictProperty["is_critical"]);
        
        // Verify list property
        Assert.True(failureDetails.Properties.ContainsKey("ListProperty"));
        var listProperty = JsonConvert.DeserializeObject<List<object>>(failureDetails.Properties["ListProperty"]!.ToString()!);
        Assert.NotNull(listProperty);
        Assert.Equal(4, listProperty.Count);
        Assert.Equal("error1", listProperty[0]);
        Assert.Equal("error2", listProperty[1]);
        Assert.Equal((long)500, listProperty[2]);
        Assert.Null(listProperty[3]);
        
        // Verify null property
        Assert.True(failureDetails.Properties.ContainsKey("NullProperty"));
        Assert.Null(failureDetails.Properties["NullProperty"]);
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // NestedOrchestrationWithCustomException is only registered in the BasicNode app.
    [Trait("PowerShell", "Skip")] // FailureDetails is a dotnet-isolated implementation detail
    [Trait("Python", "Skip")] // FailureDetails is a dotnet-isolated implementation detail
    [Trait("Java", "Skip")] // Include exception properties at Failure Details for Java is not supported yet.
    public async Task NestedCustomExceptionPropertiesInFailureDetails()
    {
        // A parent orchestration calls a sub-orchestration, which calls the failing
        // activity. The activity's custom exception properties must survive being
        // nested inside the sub-orchestration's FailureDetails.InnerFailure chain so
        // the parent can still observe them.
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=NestedOrchestrationWithCustomException");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        // Deserialize the output (the sub-orchestration's FailureDetails) returned by the parent.
        var failureDetails = JsonConvert.DeserializeObject<TaskFailureDetails>(orchestrationDetails.Output);
        Assert.NotNull(failureDetails);

        // Walk the InnerFailure chain to find the original activity exception that
        // carries the custom properties.
        static TaskFailureDetails? FindFailureByErrorType(TaskFailureDetails? details, string errorTypeSubstring)
        {
            for (TaskFailureDetails? current = details; current != null; current = current.InnerFailure)
            {
                if (current.ErrorType != null && current.ErrorType.Contains(errorTypeSubstring))
                {
                    return current;
                }
            }

            return null;
        }

        var businessFailure = FindFailureByErrorType(failureDetails, "BusinessValidationException");
        Assert.NotNull(businessFailure);
        Assert.Equal("Business logic validation failed", businessFailure!.ErrorMessage);

        // The custom properties must be preserved on the nested activity failure.
        Assert.NotNull(businessFailure.Properties);

        Assert.True(businessFailure.Properties.ContainsKey("StringProperty"));
        Assert.Equal("validation-error-123", businessFailure.Properties["StringProperty"]);

        Assert.True(businessFailure.Properties.ContainsKey("IntProperty"));
        Assert.Equal(100L, Convert.ToInt64(businessFailure.Properties["IntProperty"]));

        Assert.True(businessFailure.Properties.ContainsKey("LongProperty"));
        Assert.Equal(999999999L, Convert.ToInt64(businessFailure.Properties["LongProperty"]));

        Assert.True(businessFailure.Properties.ContainsKey("NullProperty"));
        Assert.Null(businessFailure.Properties["NullProperty"]);
    }
}
