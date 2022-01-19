// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Determines event names to use for messages sent to and from entities.
    /// </summary>
    internal static class EntityMessageEventNames
    {
        public static string RequestMessageEventName => "op";

        public static string ReleaseMessageEventName => "release";

        public static string ContinueMessageEventName => "continue";

        public static string ScheduledRequestMessageEventName(DateTime scheduledUtc) => $"op@{scheduledUtc:o}";

        public static string ResponseMessageEventName(Guid requestId) => requestId.ToString();

        public static bool IsRequestMessage(string eventName) => eventName.StartsWith("op");

        public static bool IsReleaseMessage(string eventName) => eventName == "release";
    }
}
