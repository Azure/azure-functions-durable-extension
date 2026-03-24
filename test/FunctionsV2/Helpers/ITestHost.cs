// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Functions host abstraction that works for Functions 1.0 and Functions 2.0+.
    /// </summary>
    public interface ITestHost : IDisposable
    {
        Task StartAsync();

        Task StopAsync();

        Task CallAsync(string methodName, IDictionary<string, object> arguments);

        Task CallAsync(MethodInfo method, IDictionary<string, object> arguments);
    }
}