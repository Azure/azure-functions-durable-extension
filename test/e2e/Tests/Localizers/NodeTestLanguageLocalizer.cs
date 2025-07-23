// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class NodeTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> nodeLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "Caught exception: Error: Activity function 'raise_exception' failed:" },
        { "RethrownActivityException.ErrorMessage", "Orchestrator function 'RethrowActivityException' failed: Activity function 'raise_exception' failed: " },
        // Bug: https://github.com/Azure/azure-functions-durable-js/issues/642
        { "CaughtEntityException.ErrorMessage", "Error: [object Object]" },
        { "RethrownEntityException.ErrorMessage", "Orchestrator function 'ThrowEntityOrchestration' failed:" },
        { "ExternalEvent.CompletedInstance.ErrorName", "N/A" }, // Bug: https://github.com/Azure/azure-functions-durable-js/issues/645
        { "ExternalEvent.CompletedInstance.ErrorMessage", "N/A" },
        { "ExternalEvent.InvalidInstance.ErrorName", "Error" },
        { "ExternalEvent.InvalidInstance.ErrorMessage", "No instance with ID '{0}' found" },
        { "SuspendCompletedInstance.FailureMessage", "" }, // No message as Python's unique behavior causes this to succeed
        { "ResumeCompletedInstance.FailureMessage", "" },
        { "SuspendSuspendedInstance.FailureMessage", "Error: The operation failed with an unexpected status code: 500. Details: {{\"Message\":\"Something went wrong while processing your request" },
        { "ResumeRunningInstance.FailureMessage", "Error: The operation failed with an unexpected status code: 500. Details: {{\"Message\":\"Something went wrong while processing your request" },
        { "TerminateCompletedInstance.FailureMessage", "" }, // No message as Python's unique behavior causes this to succeed
        { "TerminateTerminatedInstance.FailureMessage", "" },
        { "TerminateInvalidInstance.FailureMessage", "No instance with ID '{0}' found." },
    };

    public LanguageType GetLanguageType()
    {
        return LanguageType.Node;
    }

    public string GetLocalizedStringValue(string key, params object[] args)
    {
        return String.Format(this.nodeLocalizedStrings.GetValueOrDefault(key, ""), args:args);
    }
}
