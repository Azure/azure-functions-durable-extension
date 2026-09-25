// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.DurableTask;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal sealed class LargePayloadPurgeActivityContext(TaskName name, string instanceId) : TaskActivityContext
{
    public override TaskName Name { get; } = name;

    public override string InstanceId { get; } = instanceId;
}
