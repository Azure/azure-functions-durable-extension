// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides information about the enviroment (OS, app service plan, user-facing PL)
    /// using the DI-injected INameResolver.
    /// </summary>
#pragma warning disable CS0612 // Type or member is obsolete
    internal class DefaultPlatformInformation : IPlatformInformation
#pragma warning restore CS0612 // Type or member is obsolete
    {
        private readonly INameResolver nameResolver;

        public DefaultPlatformInformation(INameResolver nameResolver)
        {
            this.nameResolver = nameResolver;
        }

        private bool InWindowsConsumption()
        {
            string value = this.nameResolver.Resolve("WEBSITE_SKU");
            return string.Equals(value, "Dynamic", StringComparison.OrdinalIgnoreCase);
        }

        private bool InLinuxConsumption()
        {
            string containerName = this.GetContainerName();
            string azureWebsiteInstanceId = this.nameResolver.Resolve("WEBSITE_INSTANCE_ID");
            bool inAppService = !string.IsNullOrEmpty(azureWebsiteInstanceId);
            bool inLinuxConsumption = !inAppService && !string.IsNullOrEmpty(containerName);
            return inLinuxConsumption;
        }

        private bool InLinuxAppService()
        {
            string azureWebsiteInstanceId = this.nameResolver.Resolve("WEBSITE_INSTANCE_ID");
            string functionsLogsMountPath = this.nameResolver.Resolve("FUNCTIONS_LOGS_MOUNT_PATH");
            bool inAppService = !string.IsNullOrEmpty(azureWebsiteInstanceId);
            bool inLinuxDedicated = inAppService && !string.IsNullOrEmpty(functionsLogsMountPath);
            return inLinuxDedicated;
        }

        public OperatingSystem GetOperatingSystem()
        {
            if (this.InLinuxConsumption() || this.InLinuxAppService())
            {
                return OperatingSystem.Linux;
            }

            return OperatingSystem.Windows;
        }

        public bool IsInConsumptionPlan()
        {
            return this.InLinuxConsumption() || this.InWindowsConsumption();
        }

        public WorkerRuntimeType GetWorkerRuntimeType()
        {
            string workerRuntime = this.nameResolver.Resolve("FUNCTIONS_WORKER_RUNTIME");
            if (string.Compare(workerRuntime, "python", ignoreCase: true) == 0)
            {
                return WorkerRuntimeType.Python;
            }
            else if (string.Compare(workerRuntime, "node", ignoreCase: true) == 0)
            {
                return WorkerRuntimeType.JavaScript;
            }
            else if (string.Compare(workerRuntime, "powerShell", ignoreCase: true) == 0)
            {
                return WorkerRuntimeType.PowerShell;
            }
            else if (string.Compare(workerRuntime, "dotnet", ignoreCase: true) == 0)
            {
                return WorkerRuntimeType.Csharp;
            }

            throw new Exception("Could not determine worker runtime type.");
        }

        public string GetLinuxTenant()
        {
            return this.nameResolver.Resolve("WEBSITE_STAMP_DEPLOYMENT_ID");
        }

        public string GetLinuxStampName()
        {
            return this.nameResolver.Resolve("WEBSITE_HOME_STAMPNAME");
        }

        public string GetContainerName()
        {
            return this.nameResolver.Resolve("CONTAINER_NAME");
        }
    }
}
