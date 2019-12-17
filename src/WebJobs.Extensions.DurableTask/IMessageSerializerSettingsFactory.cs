// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to build instances of <see cref="JsonSerializerSettings"/> for message serialization.
    /// </summary>
    public interface IMessageSerializerSettingsFactory
    {
        /// <summary>
        /// Creates or retrieves <see cref="JsonSerializerSettings"/> to be used throughout the extension for message serialization.
        /// </summary>
        /// <returns><see cref="JsonSerializerSettings"/> to be used by the Durable Task Extension for message serialization.</returns>
        JsonSerializerSettings CreateJsonSerializerSettings();
    }
}
