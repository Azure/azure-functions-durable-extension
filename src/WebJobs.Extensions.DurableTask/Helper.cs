// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class Helper
    {
        internal static string GetHubName(DurableTaskOptions options)
        {
            if (!string.IsNullOrEmpty(options.HubNameAppSetting))
            {
                string hubName = Environment.GetEnvironmentVariable(options.HubNameAppSetting);
                return !string.IsNullOrEmpty(hubName) ? hubName : options.HubName;
            }
            else
            {
                return options.HubName;
            }
        }
    }
}
