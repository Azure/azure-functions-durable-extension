// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides utilities for inspecting the process enviroment and platform, currently mostly
    /// for checking if we are running on a Linux enviroment or not.
    /// </summary>
    /// Mostly duplicated functionality from:
    /// https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Environment/EnvironmentExtensions.cs
    /// We should consider abstracting away this functionality from both repos, to avoid code duplication.
    internal class SystemEnvironment
    {
        // Enviroment variable names. Consider moving these to a separate file if they get too big.
        // Duplicated from: https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Environment/EnvironmentSettingNames.cs
        // `FunctionsLogsMountPath` is set dynamically by the platform when it is safe to specialize the host instance (eg. file system is ready)
        public const string ContainerName = "CONTAINER_NAME";
        public const string AzureWebsiteInstanceId = "WEBSITE_INSTANCE_ID";
        public const string FunctionsLogsMountPath = "FUNCTIONS_LOGS_MOUNT_PATH";

        /// <summary>
        /// Private and lazily constructed singleton instance of SystemEnvironment.
        /// </summary>
        private static readonly Lazy<SystemEnvironment> PrivInstance =
            new Lazy<SystemEnvironment>(() => new SystemEnvironment());

        /// <summary>
        /// Private constructor, clients should obtain an instance via the `Instance` static attribute.
        /// </summary>
        private SystemEnvironment()
        {
        }

        /// <summary>
        /// The singleton instance of SystemEnvironment, no explicit constructor is exported.
        /// </summary>
        public static SystemEnvironment Instance => PrivInstance.Value;

        /// <summary>
        /// Gets a value indicating whether the application is running in a Linux Consumption (dynamic)
        /// App Service environment.
        /// </summary>
        /// <returns>true if running in a Linux Consumption App Service app; otherwise, false.</returns>
        public bool IsLinuxConsumtpion()
        {
            return !this.IsAppService() && !string.IsNullOrEmpty(this.GetEnvironmentVariable(ContainerName));
        }

        /// <summary>
        /// Gets a value indicating whether the application is running in a Linux App Service
        /// environment (Dedicated Linux).
        /// </summary>
        /// <returns>true if running in a Linux Azure App Service; otherwise, false.</returns>
        public bool IsLinuxDedicated()
        {
            return this.IsAppService() && !string.IsNullOrEmpty(this.GetEnvironmentVariable(FunctionsLogsMountPath));
        }

        /// <summary>
        /// Gets a value indicating whether the application is running in App Service
        /// (Windows Consumption, Windows Dedicated or Linux Dedicated).
        /// </summary>
        /// <returns>true if running in a Azure App Service; otherwise, false.</returns>
        private bool IsAppService()
        {
            return !string.IsNullOrEmpty(this.GetEnvironmentVariable(AzureWebsiteInstanceId));
        }

        /// <summary>
        /// Retrieves the value from an environment variable from the current process.
        /// </summary>
        public string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }
    }
}