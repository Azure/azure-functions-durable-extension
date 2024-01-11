// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableNetIsolated.Untyped;

/// <remarks>
/// The set of functions in this class demonstrate the two different ways input can be resolved for an orchestration.
/// First is through a parameter. No attribute is needed, we will resolve a parameter which is not otherwise handled and assume
/// it is the input. This is the preferred way.
/// Second is through <see cref="TaskOrchestrationContext.GetInput{T}" />. This may be deprecated in the future.
/// </remarks>
public static class DirectInput
{
    private const string WithInputName = "DirectInputWithInputParameter";
    private const string NoInputName = "DirectInputNoInputParameter";
    private const string ActivityName = "DirectInputActivity";

    [Function(nameof(DirectInput))]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(DirectInput));
        MyInput? input = await req.ReadFromJsonAsync<MyInput>();
        string orchestrationName = input!.DirectInput ? WithInputName : NoInputName;
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(orchestrationName, input);
        logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);
        return client.CreateCheckStatusResponse(req, instanceId);
    }

    [Function(WithInputName)]
    public static Task<string> WithInputParameter(
        [OrchestrationTrigger] TaskOrchestrationContext context, MyInput input, FunctionContext functionContext)
    {
        return InputOrchestrationImpl(context, input, functionContext);
    }

    [Function(NoInputName)]
    public static Task<string> NoInputParameter(
        [OrchestrationTrigger] TaskOrchestrationContext context, FunctionContext functionContext)
    {
        MyInput input = context.GetInput<MyInput>()!;
        return InputOrchestrationImpl(context, input, functionContext);
    }

    [Function(ActivityName)]
    public static string Activity([ActivityTrigger] MyInput input, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(ActivityName);
        logger.LogInformation("Received input {input}", input);
        return $"{input.PropA}, {input.PropB}";
    }

    private static async Task<string> InputOrchestrationImpl(
        TaskOrchestrationContext context, MyInput input, FunctionContext functionContext)
    {
        string result = await context.CallActivityAsync<string>(ActivityName, input) + ", ";
        result += await context.CallActivityAsync<string>(ActivityName, new MyInput(functionContext.FunctionId, 1, true));
        return result;
    }

    public record MyInput(string PropA, int PropB, bool DirectInput);
}
