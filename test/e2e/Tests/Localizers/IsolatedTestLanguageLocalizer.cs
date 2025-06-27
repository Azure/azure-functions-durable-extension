// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class IsolatedTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> isolatedLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "Task 'RaiseException' (#0) failed with an unhandled exception:" },
        { "RethrownActivityException.ErrorMessage", "Microsoft.DurableTask.TaskFailedException" }
    };

    public string GetLocalizedStringValue(string key)
    {
        return this.isolatedLocalizedStrings.GetValueOrDefault(key, "");
    }
}