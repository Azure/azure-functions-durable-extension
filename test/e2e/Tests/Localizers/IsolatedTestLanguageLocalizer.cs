// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;
internal class IsolatedTestLanguageLocalizer : ITestLanguageLocalizer
{
    private readonly Dictionary<string, string> isolatedLocalizedStrings = new Dictionary<string, string>
    {
        { "CaughtActivityException.ErrorMessage", "Task 'RaiseException' (#0) failed with an unhandled exception:" },
        { "RethrownActivityException.ErrorMessage", "Microsoft.DurableTask.TaskFailedException" },
        { "CaughtEntityException.ErrorMessage", "Operation 'ThrowFirstTimeOnly' of entity '@counter@MyExceptionEntity' failed:" },
        { "RethrownEntityException.ErrorMessage", "Microsoft.DurableTask.Entities.EntityOperationFailedException" },
        { "ExternalEvent.CompletedInstance.ErrorName", "FailedPrecondition" },
        { "ExternalEvent.CompletedInstance.ErrorMessage", "The orchestration instance with the provided instance id is not running." },
        { "ExternalEvent.InvalidInstance.ErrorName", "NotFound" },
        { "ExternalEvent.InvalidInstance.ErrorMessage", "No instance with ID '{0}' was found" },
        // Unclear error message - see https://github.com/Azure/azure-functions-durable-extension/issues/3027, will update this code when that bug is fixed
        { "SuspendCompletedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "ResumeCompletedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "SuspendSuspendedInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "ResumeRunningInstance.FailureMessage", "Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")" },
        { "TerminateCompletedInstance.FailureMessage", "InvalidOperationException: Cannot terminate the orchestration instance {0} because instance is in the Completed state." },
        { "TerminateTerminatedInstance.FailureMessage", "InvalidOperationException: Cannot terminate the orchestration instance {0} because instance is in the Terminated state." },
        { "TerminateInvalidInstance.FailureMessage", "ArgumentException: No instance with ID '{0}' was found." },
        { "RestartInvalidInstance.ErrorMessage", "An orchestration with the instanceId {0} was not found." },
        { "RestartRunningInstance.ErrorMessage", "An orchestration with the instanceId {0} cannot be restarted." },
    };

    public LanguageType GetLanguageType()
    {
        return LanguageType.DotnetIsolated;
    }

    public string GetLocalizedStringValue(string key, params object[] args)
    {
        return String.Format(this.isolatedLocalizedStrings.GetValueOrDefault(key, ""), args:args);
    }
}