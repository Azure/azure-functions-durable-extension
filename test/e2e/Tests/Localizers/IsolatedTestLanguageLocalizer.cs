// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class IsolatedTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> _isolatedLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "Task 'RaiseException' (#0) failed with an unhandled exception:" },
        { "RethrownActivityException.ErrorMessage", "Microsoft.DurableTask.TaskFailedException" },
        { "ExternalEvent.CompletedInstance.ErrorName", "FailedPrecondition" },
        { "ExternalEvent.CompletedInstance.ErrorMessage", "The orchestration instance with the provided instance id is not running." },
        { "ExternalEvent.InvalidInstance.ErrorName", "NotFound" },
        { "ExternalEvent.InvalidInstance.ErrorMessage", "No instance with ID 'instance-does-not-exist-test' was found" },
        // Unclear error message - see https://github.com/Azure/azure-functions-durable-extension/issues/3027, will update this code when that bug is fixed
        { "SuspendCompletedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "ResumeCompletedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "SuspendSuspendedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "ResumeRunningInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "TerminateCompletedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "TerminateTerminatedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "TerminateInvalidInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
    };

    public LanguageType GetLanguageType()
    {
        return LanguageType.DotnetIsolated;
    }

    public string GetLocalizedStringValue(string key)
    {
        return this.isolatedLocalizedStrings.GetValueOrDefault(key, "");
    }
}