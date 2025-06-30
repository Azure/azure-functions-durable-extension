// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class PowerShellTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> powerShellLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "One or more errors occurred. (Task 'RaiseException' (#0) failed with an unhandled exception:" },
        { "RethrownActivityException.ErrorMessage", "Orchestrator function 'RethrowActivityException' failed: " }
    };

    public string GetLocalizedStringValue(string key)
    {
        return this.powerShellLocalizedStrings.GetValueOrDefault(key, "");
    }
}
