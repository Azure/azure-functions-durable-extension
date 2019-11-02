// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Attribute used with the Durable Functions Analyzer to label a method as Deterministic. This allows the method to be called in an Orchestration function without causing a compiler warning.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DeterministicAttribute : Attribute
    {
    }
}
