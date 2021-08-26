// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

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
        private readonly Dictionary<string, string> cachedEnviromentVariables;

        public DefaultPlatformInformation(INameResolver nameResolver)
        {
            this.nameResolver = nameResolver;
            this.cachedEnviromentVariables = new Dictionary<string, string>();
        }

        private string ReadEnviromentVariable(string variableName)
        {
            string value;
            if (!this.cachedEnviromentVariables.TryGetValue(variableName, out value))
            {
                value = this.nameResolver.Resolve(variableName);
                this.cachedEnviromentVariables.Add(variableName, value);
            }

            return value;
        }

        private bool IsInLinuxConsumption()
        {
            string containerName = this.GetContainerName();
            bool inLinuxConsumption = this.IsInAppService() && !string.IsNullOrEmpty(containerName);
            return inLinuxConsumption;
        }

        private bool IsInAppService()
        {
            string azureWebsiteInstanceId = this.ReadEnviromentVariable("WEBSITE_INSTANCE_ID");
            return !string.IsNullOrEmpty(azureWebsiteInstanceId);
        }

        private bool IsInLinuxAppService()
        {
            string functionsLogsMountPath = this.ReadEnviromentVariable("FUNCTIONS_LOGS_MOUNT_PATH");
            bool inLinuxDedicated = this.IsInAppService() && !string.IsNullOrEmpty(functionsLogsMountPath);
            return inLinuxDedicated;
        }

        public OperatingSystem GetOperatingSystem()
        {
            if (this.IsInLinuxConsumption() || this.IsInLinuxAppService())
            {
                return OperatingSystem.Linux;
            }

            return OperatingSystem.Windows;
        }

        private bool IsInWindowsConsumption()
        {
            string value = this.ReadEnviromentVariable("WEBSITE_SKU");
            return string.Equals(value, "Dynamic", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsInConsumptionPlan()
        {
            return this.IsInLinuxConsumption() || this.IsInWindowsConsumption();
        }

        public WorkerRuntimeType GetWorkerRuntimeType()
        {
            string workerRuntime = this.ReadEnviromentVariable("FUNCTIONS_WORKER_RUNTIME");
            WorkerRuntimeType workerRuntimeType;
            if (Enum.TryParse(workerRuntime, ignoreCase: true, out workerRuntimeType))
            {
                return workerRuntimeType;
            }

            throw new Exception("Could not determine worker runtime type.");
        }

        public string GetLinuxTenant()
        {
            return this.ReadEnviromentVariable("WEBSITE_STAMP_DEPLOYMENT_ID");
        }

        public string GetLinuxStampName()
        {
            return this.ReadEnviromentVariable("WEBSITE_HOME_STAMPNAME");
        }

        public string GetContainerName()
        {
            return this.ReadEnviromentVariable("CONTAINER_NAME");
        }
    }
}
