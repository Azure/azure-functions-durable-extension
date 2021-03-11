// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface for accessing the AppService plan information,
    /// the OS, and user-facing PL.
    ///
    /// Note: The functionality is currently limited, but will grow
    /// along with the pursuit of more platform-specific defaults.
    /// </summary>
    [Obsolete]
    public interface IPlatformInformationService
    {
        /// <summary>
        /// Determines if the application is running on a Consumption plan,
        /// irrespective of OS.
        /// </summary>
        /// <returns>True if running in Consumption. Otherwise, False.</returns>
        bool InConsumption();

        /// <summary>
        /// Determines if the application is running in a Linux Consumption plan.
        /// </summary>
        /// <returns>True if running in Linux Consumption. Otherwise, False.</returns>
        bool InLinuxConsumption();

        /// <summary>
        /// Determines if the application is running in a Windows Consumption plan.
        /// </summary>
        /// <returns>True if running in Linux Consumption. Otherwise, False.</returns>
        bool InWindowsConsumption();

        /// <summary>
        /// Determines if the application is running in a Linux AppService plan.
        /// </summary>
        /// <returns>True if running in Linux AppService. Otherwise, False.</returns>
        bool InLinuxAppService();

        /// <summary>
        /// Returns the application tenant when running on linux.
        /// </summary>
        /// <returns>The application tenant.</returns>
        string GetLinuxTenant();

        /// <summary>
        /// Returns the application stamp name when running on linux.
        /// </summary>
        /// <returns>The application stamp name.</returns>
        string GetLinuxStampName();

        /// <summary>
        /// Returns the application container name when running on linux.
        /// </summary>
        /// <returns>The application container name.</returns>
        string GetContainerName();
    }
}
