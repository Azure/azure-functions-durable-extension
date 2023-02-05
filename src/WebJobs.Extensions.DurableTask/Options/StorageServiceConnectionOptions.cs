// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    internal abstract class StorageServiceConnectionOptions
    {
        public string AccountName { get; set; }

        public virtual Uri ServiceUri => string.IsNullOrEmpty(this.AccountName)
            ? null
            : new Uri(
                string.Format(CultureInfo.InvariantCulture, "https://{0}.{1}.core.windows.net", this.AccountName, this.ServiceName),
                UriKind.Absolute);

        protected abstract string ServiceName { get; }
    }
}
