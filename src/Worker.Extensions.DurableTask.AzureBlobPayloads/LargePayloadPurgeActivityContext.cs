// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.DurableTask;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

// Functions activity bindings supply the input and instance ID, but not an SDK TaskActivityContext.
// The shared SDK activities require one, so this adapter supplies only the required name and instance ID.
internal sealed class LargePayloadPurgeActivityContext(TaskName name, string instanceId) : TaskActivityContext
{
    public override TaskName Name { get; } = name;

    public override string InstanceId { get; } = instanceId;
}
