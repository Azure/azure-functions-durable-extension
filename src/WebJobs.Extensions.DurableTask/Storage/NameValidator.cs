// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    // This class is copied from the previous Azure Storage client SDKs
    // The following logic may require updating over time as the Azure Storage team discourages client-side validation
    // that may grow stale as the server evolves. See here: https://github.com/Azure/azure-sdk-for-js/issues/13519#issuecomment-822420305
    internal static class NameValidator
    {
        private static readonly RegexOptions RegexOptions = RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant;

        private static readonly Regex MetricsTableRegex = new Regex("^\\$Metrics(HourPrimary|MinutePrimary|HourSecondary|MinuteSecondary)?(Transactions)(Blob|Queue|Table)$", RegexOptions);
        private static readonly Regex ShareContainerQueueRegex = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions);
        private static readonly Regex TableRegex = new Regex("^[A-Za-z][A-Za-z0-9]*$", RegexOptions);

        public static void ValidateBlobName(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. The {0} name may not be null, empty, or whitespace only.", "blob"));
            }

            if (blobName.Length < 1 || blobName.Length > 1024)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name length. The {0} name must be between {1} and {2} characters long.", "blob", 1, 1024));
            }

            int num = 0;
            for (int i = 0; i < blobName.Length; i++)
            {
                if (blobName[i] == '/')
                {
                    num++;
                }
            }

            if (num >= 254)
            {
                throw new ArgumentException("The count of URL path segments (strings between '/' characters) as part of the blob name cannot exceed 254.");
            }
        }

        public static void ValidateContainerName(string containerName)
        {
            if (!"$root".Equals(containerName, StringComparison.Ordinal) && !"$logs".Equals(containerName, StringComparison.Ordinal))
            {
                ValidateShareContainerQueueHelper(containerName, "container");
            }
        }

        public static void ValidateQueueName(string queueName)
        {
            ValidateShareContainerQueueHelper(queueName, "queue");
        }

        public static void ValidateTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. The {0} name may not be null, empty, or whitespace only.", "table"));
            }

            if (tableName.Length < 3 || tableName.Length > 63)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name length. The {0} name must be between {1} and {2} characters long.", "table", 3, 63));
            }

            if (!TableRegex.IsMatch(tableName) && !MetricsTableRegex.IsMatch(tableName) && !tableName.Equals("$MetricsCapacityBlob", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. Check MSDN for more information about valid {0} naming.", "table"));
            }
        }

        private static void ValidateShareContainerQueueHelper(string resourceName, string resourceType)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. The {0} name may not be null, empty, or whitespace only.", resourceType));
            }

            if (resourceName.Length < 3 || resourceName.Length > 63)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name length. The {0} name must be between {1} and {2} characters long.", resourceType, 3, 63));
            }

            if (!ShareContainerQueueRegex.IsMatch(resourceName))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid {0} name. Check MSDN for more information about valid {0} naming.", resourceType));
            }
        }
    }
}
