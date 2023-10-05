// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Core;
using IsolatedEntities.Tests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsolatedEntities;

/// <summary>
/// A collection containing all the unit tests.
/// </summary>
static class All
{
    public static IEnumerable<Test> GetAllTests()
    {
        yield return new SetAndGet();
        yield return new CallCounter();
        yield return new BatchedEntitySignals(100);
        yield return new SignalAndCall(typeof(StringStore));
        yield return new SignalAndCall(typeof(StringStore2));
        yield return new SignalAndCall(typeof(StringStore3));
        yield return new CallAndDelete(typeof(StringStore));
        yield return new CallAndDelete(typeof(StringStore2));
        yield return new CallAndDelete(typeof(StringStore3));
        yield return new SignalThenPoll(direct: true, delayed: false);
        yield return new SignalThenPoll(direct: true, delayed: true);
        yield return new SignalThenPoll(direct: false, delayed: false);
        yield return new SignalThenPoll(direct: false, delayed: true);
        yield return new SelfScheduling();
        yield return new FireAndForget(null);
        yield return new FireAndForget(0);
        yield return new FireAndForget(5);
        yield return new SingleLockedTransfer();
        yield return new MultipleLockedTransfers(2);
        yield return new MultipleLockedTransfers(5);
        yield return new MultipleLockedTransfers(100);
        yield return new LargeEntity();
        yield return new CallFaultyEntity();
        yield return new CallFaultyEntityBatches();
        yield return new EntityQueries1();
        yield return new EntityQueries2();
        yield return new CleanOrphanedLock();
        yield return new InvalidEntityId(InvalidEntityId.Location.ClientGet);
        yield return new InvalidEntityId(InvalidEntityId.Location.ClientSignal);
        yield return new InvalidEntityId(InvalidEntityId.Location.OrchestrationCall);
        yield return new InvalidEntityId(InvalidEntityId.Location.OrchestrationSignal);
    }

}
