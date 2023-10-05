// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsolatedEntities;

class BatchedEntitySignals : Test
{
    readonly int numIterations;

    public BatchedEntitySignals(int numIterations)
    {
        this.numIterations = numIterations;
    }

    public override async Task RunAsync(TestContext context)
    {
        var entityId = new EntityInstanceId(nameof(BatchEntity), Guid.NewGuid().ToString().Substring(0,8));

        // send a number of signals immediately after each other
        List<Task> tasks = new List<Task>();
        for (int i = 0; i < numIterations; i++)
        {
            tasks.Add(context.Client.Entities.SignalEntityAsync(entityId, string.Empty, i));
        }

        await Task.WhenAll(tasks);

        var result = await context.WaitForEntityStateAsync<List<BatchEntity.Entry>>(
            entityId,
            timeout: default,
            list => list.Count == this.numIterations ? null : $"waiting for {this.numIterations - list.Count} signals");

        Assert.Equal(new BatchEntity.Entry(0, 0), result[0]);
        Assert.Equal(this.numIterations, result.Count);

        for (int i = 0; i < numIterations - 1; i++)
        {
            if (result[i].batch == result[i + 1].batch)
            {
                Assert.Equal(result[i].operation + 1, result[i + 1].operation);
            }
            else
            {
                Assert.Equal(result[i].batch + 1, result[i + 1].batch);
                Assert.Equal(0, result[i + 1].operation);
            }
        }

        // there should always be some batching going on
        int numBatches = result.Last().batch + 1;
        Assert.True(numBatches < numIterations);
        context.Logger.LogInformation($"completed {numIterations} operations in {numBatches} batches");
    }
}
