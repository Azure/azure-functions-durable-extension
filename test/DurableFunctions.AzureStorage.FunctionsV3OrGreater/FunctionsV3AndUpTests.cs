// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class FunctionsV3AndUpTests : IDisposable
    {
        private readonly ITestOutputHelper output;

        private readonly TestHelpers testHelper;

        public FunctionsV3AndUpTests(ITestOutputHelper output)
        {
            this.output = output;
            this.testHelper = new TestHelpers(output);
        }

        public void Dispose()
        {
            this.testHelper.Dispose();
        }

        /// <summary>
        /// By simulating the appropiate environment variables for Linux Consumption,
        /// this test checks that we are emitting logs from DurableTask-CustomSource
        /// and reading the DurabilityProvider's EventSourceName property correctly.
        /// </summary>
        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task CustomProviderEventSourceLogsWithEventSourceName()
        {
            var prefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // To capture console output in a StringWritter
            using (StringWriter sw = new StringWriter())
            {
                // Set console to write to StringWritter
                Console.SetOut(sw);

                // Simulate enviroment variables indicating linux consumption
                var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
                {
                    { "CONTAINER_NAME", "val1" },
                    { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                    { "WEBSITE_HOME_STAMPNAME", "val4" },
                });

                // Run trivial orchestrator
                using (var host = this.testHelper.GetJobHost(
                    nameResolver: nameResolver,
                    testName: "FiltersVerboseLogsByDefault",
                    enableExtendedSessions: false,
                    durabilityProviderFactoryType: typeof(CustomEtwDurabilityProviderFactory)))
                {
                    await host.StartAsync();
                    var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                    var status = await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Validate that the JSON has DurableTask-AzureStorage fields
                string[] lines = consoleOutput.Split('\n');
                var customeEtwLogs = lines.Where(l => l.Contains("DurableTask-CustomSource") && l.StartsWith(prefix));
                Assert.NotEmpty(customeEtwLogs);
            }
        }

        /// <summary>
        /// End to end test that ensures that customers can configure custom connection string names
        /// using DurableClientOptions when they create a DurableClient from an external app (e.g. ASP.NET Core app).
        /// The appSettings dictionary acts like appsettings.json and durableClientOptions are the
        /// settings passed in during a call to DurableClient (IDurableClientFactory.CreateClient(durableClientOptions)).
        /// </summary>
        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task DurableClient_AzureStorage__ReadsCustomStorageConnString()
        {
            string taskHubName = this.testHelper.GetTaskHubNameFromTestName(
                nameof(this.DurableClient_AzureStorage__ReadsCustomStorageConnString),
                enableExtendedSessions: false);

            Dictionary<string, string> appSettings = new Dictionary<string, string>
            {
                { "CustomStorageAccountName", TestHelpers.GetStorageConnectionString() },
                { "TestTaskHub", taskHubName },
            };

            // ConnectionName is used to look up the storage connection string in appsettings
            DurableClientOptions durableClientOptions = new DurableClientOptions
            {
                ConnectionName = "CustomStorageAccountName",
                TaskHub = taskHubName,
            };

            var connectionStringResolver = new TestCustomConnectionsStringResolver(appSettings);

            using (IHost clientHost = this.testHelper.GetJobHostExternalEnvironment(
                connectionStringResolver: connectionStringResolver))
            {
                await clientHost.StartAsync();
                IDurableClientFactory durableClientFactory = clientHost.Services.GetService(typeof(IDurableClientFactory)) as DurableClientFactory;
                IDurableClient durableClient = durableClientFactory.CreateClient(durableClientOptions);
                Assert.Equal(taskHubName, durableClient.TaskHubName);
                await clientHost.StopAsync();
            }
        }

        /// <summary>
        /// By simulating the appropiate environment variables for Linux Consumption,
        /// this test checks that we are emitting logs from DurableTask.AzureStorage
        /// and reading the DurabilityProvider's EventSourceName property correctly.
        /// </summary>
        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task AzureStorageEmittingLogsWithEventSourceName()
        {
            var prefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // To capture console output in a StringWritter
            using (StringWriter sw = new StringWriter())
            {
                // Set console to write to StringWritter
                Console.SetOut(sw);

                // Simulate enviroment variables indicating linux consumption
                var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
                {
                    { "CONTAINER_NAME", "val1" },
                    { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                    { "WEBSITE_HOME_STAMPNAME", "val4" },
                });

                // Run trivial orchestrator
                using (var host = this.testHelper.GetJobHost(
                    nameResolver: nameResolver,
                    testName: "FiltersVerboseLogsByDefault",
                    enableExtendedSessions: false,
                    storageProviderType: "azure_storage"))
                {
                    await host.StartAsync();
                    var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                    var status = await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Validate that the JSON has DurableTask-AzureStorage fields
                string[] lines = consoleOutput.Split('\n');
                var azureStorageLogLines = lines.Where(l => l.Contains("DurableTask-AzureStorage") && l.StartsWith(prefix));
                Assert.NotEmpty(azureStorageLogLines);
            }
        }
    }
}
