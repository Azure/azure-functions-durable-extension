// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    internal sealed class AzureStorageAccountOptions : AzureIdentityOptions
    {
        public const string DefaultEndpointSuffix = "core.windows.net";

        public string AccountName { get; set; }

        public Uri BlobServiceUri { get; set; }

        public Uri QueueServiceUri { get; set; }

        public Uri TableServiceUri { get; set; }

        public string ConnectionString { get; set; }
    }
}
