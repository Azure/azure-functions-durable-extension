// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// 
    /// </summary>
    public interface ISerializerSettingsFactory
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        JsonSerializerSettings CreateJsonSerializerSettings();
    }
}
