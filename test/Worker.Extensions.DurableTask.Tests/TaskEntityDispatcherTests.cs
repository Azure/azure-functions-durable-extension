// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using Google.Protobuf;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class TaskEntityDispatcherTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("10", 10)]
    public async Task DispatchAsync_ApplicationException_RollsBackWithoutRetrying(
        string? initialState, int initialValue)
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        using var cache = new ExtendedSessionsCache();
        var inputs = new List<int>();
        var observedStates = new List<int>();
        var request = new P.EntityBatchRequest
        {
            InstanceId = "@counter@failure",
            EntityState = initialState,
            Operations =
            {
                new P.OperationRequest { Operation = "add", Input = "99", RequestId = Guid.NewGuid().ToString() },
                new P.OperationRequest { Operation = "get", RequestId = Guid.NewGuid().ToString() },
            },
        };

        ValueTask<object?> HandleOperation(TaskEntityOperation operation)
        {
            if (operation.Name == "get")
            {
                return new(operation.State.GetState<int>());
            }

            Assert.Equal("add", operation.Name);
            int input = operation.GetInput<int>();
            int state = operation.State.GetState<int>();
            inputs.Add(input);
            observedStates.Add(state);
            operation.State.SetState(state + input);
            operation.Context.SignalEntity(new EntityInstanceId("observer", "failure"), "add", input);

            // Keep the attempt count outside entity state so rollback cannot hide a retry.
            if (inputs.Count == 1)
            {
                throw new InvalidOperationException("application failure");
            }

            return new(operation.State.GetState<int>());
        }

        async Task<P.EntityBatchResult> DispatchAsync(P.EntityBatchRequest batch)
        {
            var dispatcher = new TaskEntityDispatcher(
                Convert.ToBase64String(batch.ToByteArray()), services, cache);
            await dispatcher.DispatchAsync(HandleOperation);
            return P.EntityBatchResult.Parser.ParseFrom(Convert.FromBase64String(dispatcher.Result));
        }

        P.EntityBatchResult result = await DispatchAsync(request);

        Assert.Equal(new[] { 99 }, inputs);
        Assert.Equal(new[] { initialValue }, observedStates);
        Assert.False(result.RequiresState);
        Assert.Null(result.FailureDetails);
        Assert.Equal(initialState, result.EntityState);
        Assert.Empty(result.Actions);
        Assert.Collection(
            result.Results,
            failed =>
            {
                Assert.Equal(P.OperationResult.ResultTypeOneofCase.Failure, failed.ResultTypeCase);
                Assert.Equal(typeof(InvalidOperationException).FullName, failed.Failure.FailureDetails.ErrorType);
                Assert.Equal("application failure", failed.Failure.FailureDetails.ErrorMessage);
            },
            read =>
            {
                Assert.Equal(P.OperationResult.ResultTypeOneofCase.Success, read.ResultTypeCase);
                Assert.Equal(JsonSerializer.Serialize(initialValue), read.Success.Result);
            });

        // Only a new, explicitly submitted operation retries the input.
        var retry = new P.EntityBatchRequest
        {
            InstanceId = request.InstanceId,
            EntityState = result.EntityState,
            Operations =
            {
                new P.OperationRequest { Operation = "add", Input = "99", RequestId = Guid.NewGuid().ToString() },
            },
        };

        P.EntityBatchResult retried = await DispatchAsync(retry);

        Assert.Equal(new[] { 99, 99 }, inputs);
        Assert.Equal(new[] { initialValue, initialValue }, observedStates);
        Assert.Null(retried.FailureDetails);
        Assert.Equal(P.OperationResult.ResultTypeOneofCase.Success, Assert.Single(retried.Results).ResultTypeCase);
        Assert.Equal(JsonSerializer.Serialize(initialValue + 99), retried.EntityState);
        Assert.Single(retried.Actions);
    }
}
