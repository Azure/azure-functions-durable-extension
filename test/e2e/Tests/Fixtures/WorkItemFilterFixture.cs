// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

/// <summary>
/// Fixture that starts the function host with work-item filtering enabled.
/// Used by <see cref="WorkItemFilterTests"/> to scope the
/// <c>workItemFilteringEnabled</c> setting to only those tests.
/// </summary>
public class WorkItemFilterFixture : FunctionAppFixture
{
    public WorkItemFilterFixture(IMessageSink messageSink)
        : base(messageSink)
    {
        this.functionAppProcess.AdditionalEnvironmentVariables = new Dictionary<string, string>
        {
            // A separate emulator prevents filtered-worker state from stalling unfiltered workers.
            ["DURABLE_TASK_SCHEDULER_CONNECTION_STRING"] = "Endpoint=http://localhost:8083;Authentication=None",
            ["AzureFunctionsJobHost__extensions__durableTask__storageProvider__workItemFilteringEnabled"] = "true",
        };
    }
}
