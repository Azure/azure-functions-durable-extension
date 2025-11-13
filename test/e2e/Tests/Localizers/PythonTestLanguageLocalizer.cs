// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class PythonTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> pythonLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "Caught exception: Activity function 'raise_exception' failed: " },
        { "RethrownActivityException.ErrorMessage", "Orchestrator function 'RethrowActivityException' failed: Activity function 'raise_exception' failed: " },
        { "CaughtEntityException.ErrorMessage", "This entity failed\r\nMore information about the failure" },
        { "RethrownEntityException.ErrorMessage", "Orchestrator function 'ThrowEntityOrchestration' failed:" },
        { "ExternalEvent.CompletedInstance.ErrorName", "Exception" },
        { "ExternalEvent.CompletedInstance.ErrorMessage", "Instance with ID {0} is gone: either completed or failed" },
        { "ExternalEvent.InvalidInstance.ErrorName", "Exception" },
        { "ExternalEvent.InvalidInstance.ErrorMessage", "No instance with ID {0} found" },
        { "SuspendCompletedInstance.FailureMessage", "" }, // No message as Python's unique behavior causes this to succeed
        { "ResumeCompletedInstance.FailureMessage", "" },
        { "SuspendSuspendedInstance.FailureMessage", "The operation failed with an unexpected status code 500" },
        { "ResumeRunningInstance.FailureMessage", "The operation failed with an unexpected status code 500" },
        { "TerminateCompletedInstance.FailureMessage", "" }, // No message as Python's unique behavior causes this to succeed
        { "TerminateTerminatedInstance.FailureMessage", "" },
        { "TerminateInvalidInstance.FailureMessage", "No instance with ID '{0}' found." },
    };

    public LanguageType GetLanguageType()
    {
        return LanguageType.Python;
    }

    public string GetLocalizedStringValue(string key, params object[] args)
    {
        return String.Format(this.pythonLocalizedStrings.GetValueOrDefault(key, ""), args:args);
    }
}
