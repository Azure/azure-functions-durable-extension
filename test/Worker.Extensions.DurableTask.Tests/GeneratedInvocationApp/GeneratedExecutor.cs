// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Invocation;

namespace DurableTask.GeneratedInvocationApp;

public static class GeneratedExecutor
{
    public static IFunctionExecutor Create(IFunctionActivator activator) => new DirectFunctionExecutor(activator);
}
