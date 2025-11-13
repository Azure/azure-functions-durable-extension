// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class PowerShellTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> powerShellLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "One or more errors occurred. (Task 'RaiseException' (#0) failed with an unhandled exception:" },
        { "RethrownActivityException.ErrorMessage", "Orchestrator function 'RethrowActivityException' failed: " },
        { "CaughtEntityException.ErrorMessage", "Test not implemented!" },
        { "RethrownEntityException.ErrorMessage", "Test not implemented!" },
        { "ExternalEvent.CompletedInstance.ErrorName", "HttpResponseException" },
        { "ExternalEvent.CompletedInstance.ErrorMessage", "Response status code does not indicate success: 410 (Gone)." },
        { "ExternalEvent.InvalidInstance.ErrorName", "HttpResponseException" },
        { "ExternalEvent.InvalidInstance.ErrorMessage", "Response status code does not indicate success: 404 (Not Found)." },
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
        return LanguageType.PowerShell;
    }

    public string GetLocalizedStringValue(string key, params object[] args)
    {
        return String.Format(this.powerShellLocalizedStrings.GetValueOrDefault(key, ""), args:args);
    }
}
