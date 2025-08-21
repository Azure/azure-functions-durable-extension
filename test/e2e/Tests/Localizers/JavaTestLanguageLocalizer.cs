// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class JavaTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> isolatedLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "Task 'RaiseException' (#0) failed with an unhandled exception:" },
        { "RethrownActivityException.ErrorMessage", "com.microsoft.durabletask.TaskFailedException: Task " },
        { "CaughtEntityException.ErrorMessage", "N/A (Test not implemented)" },
        { "RethrownEntityException.ErrorMessage", "N/A (Test not implemented)" },
        { "ExternalEvent.CompletedInstance.ErrorName", "gRPC error: StatusRuntimeException - FAIL" },
        { "ExternalEvent.CompletedInstance.ErrorMessage", "The orchestration instance with the provided instance id is not running." },
        { "ExternalEvent.InvalidInstance.ErrorName", "gRPC error: StatusRuntimeException - NOT_FOUND" },
        { "ExternalEvent.InvalidInstance.ErrorMessage", "No instance with ID '{0}' was found" },
        // Unclear error message - see https://github.com/Azure/azure-functions-durable-extension/issues/3027, will update this code when that bug is fixed
        { "SuspendCompletedInstance.FailureMessage", "UNKNOWN: Exception was thrown by handler." },
        { "ResumeCompletedInstance.FailureMessage", "UNKNOWN: Exception was thrown by handler." },
        { "SuspendSuspendedInstance.FailureMessage", "UNKNOWN: Exception was thrown by handler." },
        { "ResumeRunningInstance.FailureMessage", "UNKNOWN: Exception was thrown by handler." },
        { "TerminateCompletedInstance.FailureMessage", "InvalidOperationException: Cannot terminate the orchestration instance {0} because instance is in the Completed state." },
        { "TerminateTerminatedInstance.FailureMessage", "InvalidOperationException: Cannot terminate the orchestration instance {0} because instance is in the Terminated state." },
        { "TerminateInvalidInstance.FailureMessage", "ArgumentException: No instance with ID '{0}' was found." },
    };

    public LanguageType GetLanguageType()
    {
        return LanguageType.Java;
    }

    public string GetLocalizedStringValue(string key, params object[] args)
    {
        return String.Format(this.isolatedLocalizedStrings.GetValueOrDefault(key, ""), args:args);
    }
}