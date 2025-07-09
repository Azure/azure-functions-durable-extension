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
        { "SuspendCompletedInstance.FailureMessage", "Response status code does not indicate success: 410 (Gone)." },
        { "ResumeCompletedInstance.FailureMessage", "Response status code does not indicate success: 410 (Gone)." },
        { "SuspendSuspendedInstance.FailureMessage", "Response status code does not indicate success: 500 (Internal Server Error)." },
        { "ResumeRunningInstance.FailureMessage", "Response status code does not indicate success: 500 (Internal Server Error)." },
        { "TerminateCompletedInstance.FailureMessage", "Response status code does not indicate success: 410 (Gone)." },
        { "TerminateTerminatedInstance.FailureMessage", "Response status code does not indicate success: 410 (Gone)." },
        { "TerminateInvalidInstance.FailureMessage", "Response status code does not indicate success: 404 (Not Found)." },
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
