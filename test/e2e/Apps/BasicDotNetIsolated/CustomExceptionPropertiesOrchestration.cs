// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Microsoft.Azure.Durable.Tests.E2E;

public class CustomExceptionPropertiesOrchestration
{
    [Function(nameof(OrchestrationWithCustomException))]
    public async Task<string> OrchestrationWithCustomException([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Call the activity that will throw an exception
        await context.CallActivityAsync(nameof(BusinessActivity));
        return "Success";
    }

    [Function(nameof(BusinessActivity))]
    public void BusinessActivity([ActivityTrigger] TaskActivityContext context)
    {
        // Throw an exception with custom properties that should be captured
        throw new ArgumentOutOfRangeException(
            paramName: "age",
            actualValue: 150,
            message: "Age must be less than 120");
    }
}
