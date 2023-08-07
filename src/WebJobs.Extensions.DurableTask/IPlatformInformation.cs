// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Representation of the supported Operating Systems.
    /// </summary>
    public enum OperatingSystem
    {
        /// <summary>
        /// Linux OS.
        /// </summary>
        Linux,

        /// <summary>
        /// Windows OS
        /// </summary>
        Windows,
    }

    /// <summary>
    /// Representation of supported Programming Languages.
    /// </summary>
    public enum WorkerRuntimeType
    {
        /// <summary>
        /// .NET in-process.
        /// </summary>
        DotNet,

        /// <summary>
        /// .NET out-of-process.
        /// </summary>
        DotNetIsolated,

        /// <summary>
        /// Python.
        /// </summary>
        Python,

        /// <summary>
        /// Node: either JavaScript and TypeScript.
        /// </summary>
        Node,

        /// <summary>
        /// PowerShell.
        /// </summary>
        PowerShell,

        /// <summary>
        /// Java.
        /// </summary>
        Java,

        /// <summary>
        /// Custom handler (see https://learn.microsoft.com/en-us/azure/azure-functions/functions-custom-handlers).
        /// </summary>
        Custom,

        /// <summary>
        /// Unknown worker runtime.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// Interface for accessing the AppService plan information,
    /// the OS, and user-facing PL.
    ///
    /// Note: The functionality is currently limited, but will grow
    /// along with the pursuit of more platform-specific defaults.
    /// </summary>
    [Obsolete]
    public interface IPlatformInformation
    {
        /// <summary>
        /// Determine the underlying plan is Consumption or not.
        /// </summary>
        /// <returns> True if the plan is Consumption. Otherwise, False.</returns>
        bool IsInConsumptionPlan();

        /// <summary>
        /// Determine the underlying operating system.
        /// </summary>
        /// <returns>An OperatingSystem enum.</returns>
        OperatingSystem GetOperatingSystem();

        /// <summary>
        /// Determine the underlying worker runtime.
        /// </summary>
        /// <returns>A WorkerRuntimeType enum.</returns>
        WorkerRuntimeType GetWorkerRuntimeType();

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

        /// <summary>
        /// Determines whether the user has opted in to the external PowerShell SDK.
        /// </summary>
        /// <returns>True if the user has opted in to the external PowerShell SDK. False otherwise.</returns>
        bool UsesExternalPowerShellSDK();
    }
}
