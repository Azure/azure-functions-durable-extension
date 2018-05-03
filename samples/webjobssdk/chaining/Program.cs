// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;


namespace DFWebJobsSample
{
    public static class Program
    {
        static void Main(string[] args)
        {
            using (var loggerFactory = new LoggerFactory())
            {
                var config = new JobHostConfiguration();

                config.DashboardConnectionString = "";

                var instrumentationKey =
                    ConfigurationManager.AppSettings["APPINSIGHTS_INSTRUMENTATIONKEY"];
 
                config.LoggerFactory = loggerFactory
                    .AddApplicationInsights(instrumentationKey, null)
                    .AddConsole();

                config.UseTimers();
                config.UseDurableTask(new DurableTaskExtension
                {
                    HubName = "MyTaskHub",
                });
                var host = new JobHost(config);
                host.RunAndBlock();
            }
        }
    }
}
