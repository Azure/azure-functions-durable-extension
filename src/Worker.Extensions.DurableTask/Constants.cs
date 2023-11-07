// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal static class Constants
{
    // TODO: Need to add call to action, i.e., pointer to some documentation.
    public const string IllegalAwaitErrorMessage =
        "An invalid asynchronous invocation was detected. This can be caused by awaiting non-durable tasks " +
        "in an orchestrator function's implementation or by middleware that invokes asynchronous code.";

    public const string HttpTaskActivityReservedName = "BuiltIn::HttpActivity";
}