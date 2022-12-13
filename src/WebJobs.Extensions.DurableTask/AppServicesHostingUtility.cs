// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    // Taken from Microsoft.Azure.WebJobs.Host https://github.com/Azure/azure-webjobs-sdk/commit/7442bed629d15b4837cd8d18c302381b34f3ddac
    internal static class AppServicesHostingUtility
    {
        private const int BytesPerGB = 1024 * 1024 * 1024;

        public static long GetMemoryLimitBytes(string sku = null, int? numCores = null)
        {
            sku ??= GetWebsiteSku();
            numCores ??= GetEffectiveCoresCount();

            if (!string.IsNullOrEmpty(sku))
            {
                float memoryGBPerCore = GetMemoryGBPerCore(sku);

                if (memoryGBPerCore > 0)
                {
                    double memoryLimitBytes = memoryGBPerCore * numCores.Value * BytesPerGB;

                    if (string.Equals(sku, "IsolatedV2", StringComparison.OrdinalIgnoreCase) && numCores == 8)
                    {
                        // special case for upper tier IsolatedV2 where GB per Core
                        // isn't cleanly linear
                        memoryLimitBytes = (float)23 * BytesPerGB;
                    }

                    return (long)memoryLimitBytes;
                }
            }

            // unable to determine memory limit
            return -1;
        }

        private static int GetEffectiveCoresCount()
        {
            // When not running on VMSS, the dynamic plan has some limits that mean that a given instance is using effectively a single core,
            // so we should not use Environment.Processor count in this case.
            var effectiveCores = (IsConsumptionSku() && !IsVMSS()) ? 1 : Environment.ProcessorCount;
            return effectiveCores;
        }

        private static string GetWebsiteSku()
        {
            return Environment.GetEnvironmentVariable("WEBSITE_SKU");
        }

        private static bool IsConsumptionSku()
        {
            string value = GetWebsiteSku();
            return string.Equals(value, "Dynamic", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVMSS()
        {
            string value = Environment.GetEnvironmentVariable("RoleInstanceId");
            return value != null && value.IndexOf("HostRole", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static float GetMemoryGBPerCore(string sku)
        {
            if (string.IsNullOrEmpty(sku))
            {
                return -1;
            }

            // These memory allowances are based on published limits:
            // Dynamic SKU: https://docs.microsoft.com/en-us/azure/azure-functions/functions-scale#service-limits
            // Premium SKU: https://docs.microsoft.com/en-us/azure/azure-functions/functions-premium-plan?tabs=portal#available-instance-skus
            // Dedicated SKUs: https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
            switch (sku.ToLower())
            {
                case "free":
                case "shared":
                    return 1;
                case "dynamic":
                    return 1.5F;
                case "basic":
                case "standard":
                    return 1.75F;
                case "premiumv2":
                case "isolated":
                case "elasticpremium":
                    return 3.5F;
                case "premiumv3":
                case "isolatedv2":
                    return 4;
                default:
                    return -1;
            }
        }
    }
}
