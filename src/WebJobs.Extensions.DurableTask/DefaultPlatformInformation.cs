// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

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
        private readonly ConcurrentDictionary<string, string> cachedEnviromentVariables;
        private readonly EndToEndTraceHelper traceHelper;

        private WorkerRuntimeType? workerRuntimeType;

        public DefaultPlatformInformation(INameResolver nameResolver, ILoggerFactory loggerFactory)
        {
            this.nameResolver = nameResolver;

            ILogger logger = loggerFactory.CreateLogger(DurableTaskExtension.LoggerCategoryName);
            this.traceHelper = new EndToEndTraceHelper(logger, traceReplayEvents: false);

            this.cachedEnviromentVariables = new ConcurrentDictionary<string, string>();
        }

        private string? ReadEnviromentVariable(string variableName)
        {
            string? value;
            if (!this.cachedEnviromentVariables.TryGetValue(variableName, out value))
            {
                value = this.nameResolver.Resolve(variableName);
                this.cachedEnviromentVariables.TryAdd(variableName, value);
            }

            return value;
        }

        private bool IsInLinuxConsumption()
        {
            string? containerName = this.GetContainerName();
            bool inLinuxConsumption = !this.IsInAppService() && !string.IsNullOrEmpty(containerName);
            return inLinuxConsumption;
        }

        private bool IsInAppService()
        {
            string? azureWebsiteInstanceId = this.ReadEnviromentVariable("WEBSITE_INSTANCE_ID");
            return !string.IsNullOrEmpty(azureWebsiteInstanceId);
        }

        private bool IsInLinuxAppService()
        {
            string? functionsLogsMountPath = this.ReadEnviromentVariable("FUNCTIONS_LOGS_MOUNT_PATH");
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
            string? value = this.ReadEnviromentVariable("WEBSITE_SKU");
            return string.Equals(value, "Dynamic", StringComparison.OrdinalIgnoreCase);
        }

        public bool UsesExternalPowerShellSDK()
        {
            string? value = this.ReadEnviromentVariable("ExternalDurablePowerShellSDK");
            var parsingSucceeded = bool.TryParse(value, out var usesExternalPowerShellSDK);
            return parsingSucceeded ? usesExternalPowerShellSDK : false;
        }

        public bool IsInConsumptionPlan()
        {
            return this.IsInLinuxConsumption() || this.IsInWindowsConsumption();
        }

        public WorkerRuntimeType GetWorkerRuntimeType()
        {
            if (this.workerRuntimeType == null)
            {
                const string envVariableName = "FUNCTIONS_WORKER_RUNTIME";
                string? workerRuntime = this.ReadEnviromentVariable(envVariableName);
                if (workerRuntime != null && Enum.TryParse(workerRuntime.Replace("-", ""), ignoreCase: true, out WorkerRuntimeType type))
                {
                    this.workerRuntimeType = type;
                }
                else
                {
                    this.traceHelper.ExtensionWarningEvent(
                        hubName: "",
                        functionName: "",
                        instanceId: "",
                        $"Failed to parse {envVariableName} value: '{workerRuntime}'. This could lead to performance and correctness problems");
                    this.workerRuntimeType = WorkerRuntimeType.Unknown;
                }
            }

            return this.workerRuntimeType.Value;
        }

        public string? GetLinuxTenant()
        {
            return this.ReadEnviromentVariable("WEBSITE_STAMP_DEPLOYMENT_ID");
        }

        public string? GetLinuxStampName()
        {
            return this.ReadEnviromentVariable("WEBSITE_HOME_STAMPNAME");
        }

        public string? GetContainerName()
        {
            return this.ReadEnviromentVariable("CONTAINER_NAME");
        }
    }
}
