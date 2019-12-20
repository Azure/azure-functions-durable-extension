// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to build instances of <see cref="JsonSerializerSettings"/> for error serialization.
    /// </summary>
    public interface IErrorSerializerSettingsFactory
    {
        /// <summary>
        /// Creates or retrieves <see cref="JsonSerializerSettings"/> to be used throughout the extension for error serialization.
        /// </summary>
        /// <returns><see cref="JsonSerializerSettings"/> to be used by the Durable Task Extension for error serialization.</returns>
        JsonSerializerSettings CreateJsonSerializerSettings();
    }
}