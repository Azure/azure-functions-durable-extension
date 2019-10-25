// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the Emulator storage provider.
    /// </summary>
    /// <remarks>
    /// At this time, there is no configuration available for the Emulator storage provider.
    /// </remarks>
    public class EmulatorStorageOptions : CommonStorageProviderOptions
    {
        internal override void Validate()
        {
        }

        internal override void ValidateHubName(string hubName)
        {
        }

        internal override void AddToDebugString(StringBuilder builder)
        {
        }
    }
}
