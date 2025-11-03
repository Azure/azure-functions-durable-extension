// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Minimal options class for Scale package - only contains what's needed for scaling decisions.
    /// </summary>
    public class DurableTaskScaleOptions
    {
        public string HubName { get; set; }

        public IDictionary<string, object> StorageProvider { get; set; } = new Dictionary<string, object>();

        public int? MaxConcurrentOrchestratorFunctions { get; set; }

        public int? MaxConcurrentActivityFunctions { get; set; }

        public static void ResolveAppSettingOptions(DurableTaskScaleOptions options, INameResolver nameResolver)
        {
            if (options.StorageProvider.TryGetValue("connectionName", out object connectionNameObj) && connectionNameObj is string connectionName)
            {
                options.StorageProvider["connectionName"] = nameResolver.Resolve(connectionName);
            }
        }
    }
}
