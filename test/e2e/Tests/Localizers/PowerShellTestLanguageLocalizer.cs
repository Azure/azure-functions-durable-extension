// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class PowerShellTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> _powerShellLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "One or more errors occurred. (Task 'RaiseException' (#0) failed with an unhandled exception:" },
        { "RethrownActivityException.ErrorMessage", "Orchestrator function 'RethrowActivityException' failed: " },
        { "ExternalEvent.CompletedInstance.ErrorName", "HttpResponseException" },
        { "ExternalEvent.CompletedInstance.ErrorMessage", "Response status code does not indicate success: 410 (Gone)." },
        { "ExternalEvent.InvalidInstance.ErrorName", "HttpResponseException" },
        { "ExternalEvent.InvalidInstance.ErrorMessage", "Response status code does not indicate success: 404 (Not Found)." },
    };

    public string GetLocalizedStringValue(string key)
    {
        return this.powerShellLocalizedStrings.GetValueOrDefault(key, "");
    }
}
