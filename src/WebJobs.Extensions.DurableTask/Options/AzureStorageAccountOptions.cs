// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    internal sealed class AzureStorageAccountOptions
    {
        public const string DefaultEndpointSuffix = "core.windows.net";
        public const string DefaultScheme = "https";

        public string AccountName { get; set; }

        public Uri BlobServiceUri { get; set; }

        public Uri QueueServiceUri { get; set; }

        public Uri TableServiceUri { get; set; }

        internal Uri GetDefaultServiceUri(string service)
        {
            if (string.IsNullOrEmpty(service))
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (string.IsNullOrEmpty(this.AccountName))
            {
                throw new InvalidOperationException($"Please specify account name or explicit uri for '{service}' service.");
            }

            return new Uri(
                FormattableString.Invariant($"{DefaultScheme}://{this.AccountName}.{service}.{DefaultEndpointSuffix}"),
                UriKind.Absolute);
        }
    }
}
