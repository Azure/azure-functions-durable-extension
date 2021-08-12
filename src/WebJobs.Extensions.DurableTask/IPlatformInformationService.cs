// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Representation of the Consumption and the AppService (Dedicated or Premium) plans.
    /// </summary>
    public enum PlanType
    {
        /// <summary>
        /// Consumption App Service plan.
        /// </summary>
        Consumption,

        /// <summary>
        /// Non-consumption App Service plans: Dedicated, EP, etc.
        /// </summary>
        AppService,
    }

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
        /// C-Sharp.
        /// </summary>
        Csharp,

        /// <summary>
        /// Python.
        /// </summary>
        Python,

        /// <summary>
        /// JavaScript.
        /// </summary>
        JavaScript,

        /// <summary>
        /// PowerShell.
        /// </summary>
        PowerShell,
    }

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
        /// Determine the App Service Plan of this application.
        /// </summary>
        /// <returns>An AppServicePlan enum.</returns>
        PlanType GetPlanType();

        /// <summary>
        /// Determine the underlying operating system.
        /// </summary>
        /// <returns>An OperatingSystem enum.</returns>
        OperatingSystem GetOperatingSystem();

        /// <summary>
        /// Determine the underlying programming language.
        /// </summary>
        /// <returns>A ProgLanguage enum.</returns>
        WorkerRuntimeType GetWorkerRuntimeType();

        /// <summary>
        /// Determines is the language worker is OOProc.
        /// </summary>
        /// <returns>True if the language worker is for Python. Otherwise, False.</returns>
        bool IsOutOfProc();

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
