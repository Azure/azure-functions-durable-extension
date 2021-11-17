// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ClientIdentityOptions
    {
        public string TenantId { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string Credential { get; set; }

        public bool UseManagedIdentity => string.Equals(this.Credential, "managedidentity", StringComparison.OrdinalIgnoreCase);
    }
}
