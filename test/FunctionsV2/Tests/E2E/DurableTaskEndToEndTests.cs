// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class DurableTaskEndToEndTests : DurableTaskEndToEndTestBase
    {
        public DurableTaskEndToEndTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// End-to-end test which validates a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, TestHelpers.AzureStorageProviderType)]
        [InlineData(false, TestHelpers.AzureStorageProviderType)]
        [InlineData(true, TestHelpers.EmulatorProviderType)]
        [InlineData(false, TestHelpers.EmulatorProviderType)]
        public async Task HelloWorldOrchestration_Inline(bool extendedSessions, string storageProviderType)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Inline),
                extendedSessions,
                storageProviderType: storageProviderType))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Inline",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates task hub name configured via the <see cref="DurableClientAttribute"/> when
        /// simple orchestrator function which that doesn't call any activity functions is executed.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorld_OrchestrationClientTaskHub(string storageProviderType)
        {
            string taskHubName = TestHelpers.GetTaskHubNameFromTestName(
                nameof(this.HelloWorld_OrchestrationClientTaskHub),
                enableExtendedSessions: false);

            Dictionary<string, string> appSettings = new Dictionary<string, string>
            {
                { "TestTaskHub", taskHubName },
            };

            using (var clientHost = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorld_OrchestrationClientTaskHub) + "_Unused",
                enableExtendedSessions: false,
                nameResolver: new SimpleNameResolver(appSettings),
                storageProviderType: storageProviderType))
            using (var orchestrationHost = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorld_OrchestrationClientTaskHub),
                enableExtendedSessions: false,
                storageProviderType: storageProviderType,
                exactTaskHubName: taskHubName))
            {
                await clientHost.StartAsync();
                await orchestrationHost.StartAsync();

                // First, start and complete an orchestration on the main orchestration host.
                var client = await orchestrationHost.StartOrchestratorAsync(
                    nameof(TestOrchestrations.SayHelloInline),
                    "World",
                    this.output,
                    useTaskHubFromAppSettings: false);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);

                // Next, start an orchestration from the client host and verify that it completes on the orchestration host.
                client = await clientHost.StartOrchestratorAsync(
                    nameof(TestOrchestrations.SayHelloInline),
                    "World",
                    this.output,
                    useTaskHubFromAppSettings: true);
                status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);

                await orchestrationHost.StopAsync();
                await clientHost.StopAsync();
            }
        }

        /// <summary>
        /// End to end test that ensures that DurableClientFactory is set up correctly
        /// (i.e. the correct services are injected through dependency injection
        /// and AzureStorageDurabilityProvider is created).
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableClient_AzureStorage_SuccessfulSetup()
        {
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);
            using (ITestHost host = TestHelpers.GetJobHost(
                loggerProvider: this.loggerProvider,
                testName: nameof(this.DurableClient_AzureStorage_SuccessfulSetup),
                enableExtendedSessions: false,
                storageProviderType: "azure_storage",
                addDurableClientFactory: true))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }
        }

        /// <summary>
        /// End to end test that ensures that customers can configure custom connection string names
        /// using DurableClientOptions when they create a DurableClient from an external app (e.g. ASP.NET Core app).
        /// The appSettings dictionary acts like appsettings.json and durableClientOptions are the
        /// settings passed in during a call to DurableClient (IDurableClientFactory.CreateClient(durableClientOptions)).
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableClient_AzureStorage__ReadsCustomStorageConnString()
        {
            string taskHubName = TestHelpers.GetTaskHubNameFromTestName(
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

            var clientProviderFactory = new CustomStorageServiceClientProviderFactory(appSettings);

            using (IHost clientHost = TestHelpers.GetJobHostExternalEnvironment(clientProviderFactory))
            {
                await clientHost.StartAsync();
                IDurableClientFactory durableClientFactory = clientHost.Services.GetRequiredService<IDurableClientFactory>();
                IDurableClient durableClient = durableClientFactory.CreateClient(durableClientOptions);
                Assert.Equal(taskHubName, durableClient.TaskHubName);
                await clientHost.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple orchestrator function does not have assigned value for <see cref="DurableOrchestrationContext.ParentInstanceId"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ParentInstanceId_Not_Assigned_In_Orchestrator(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ProvideParentInstanceId),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ParentInstanceId_Not_Assigned_In_Orchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("", status.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// By simulating the appropriate environment variables for Linux Consumption,
        /// this test checks that we are emitting logs from DurableTask.AzureStorage
        /// and reading the DurabilityProvider's EventSourceName property correctly.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task AzureStorageEmittingLogsWithEventSourceName()
        {
            var prefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // To capture console output in a StringWritter
            using (StringWriter sw = new StringWriter())
            {
                // Set console to write to StringWritter
                Console.SetOut(sw);

                // Simulate environment variables indicating linux consumption
                var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
                {
                    { "CONTAINER_NAME", "val1" },
                    { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                    { "WEBSITE_HOME_STAMPNAME", "val4" },
                    { "FUNCTIONS_WORKER_RUNTIME", "python" },
                });

                // Run trivial orchestrator
                using (var host = TestHelpers.GetJobHost(
                    this.loggerProvider,
                    nameResolver: nameResolver,
                    testName: "FiltersVerboseLogsByDefault",
                    enableExtendedSessions: false,
                    storageProviderType: "azure_storage"))
                {
                    await host.StartAsync();
                    var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                    await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Validate that the JSON has DurableTask-AzureStorage fields
                string[] lines = consoleOutput.Split('\n');
                var azureStorageLogLines = lines.Where(l => l.Contains("DurableTask-AzureStorage") && l.StartsWith(prefix));
                Assert.NotEmpty(azureStorageLogLines);
            }
        }

        /// <summary>
        /// By simulating the appropriate environment variables for Linux Consumption,
        /// this test checks that we are emitting logs from DurableTask-CustomSource
        /// and reading the DurabilityProvider's EventSourceName property correctly.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CustomProviderEventSourceLogsWithEventSourceName()
        {
            var prefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // To capture console output in a StringWritter
            using (StringWriter sw = new StringWriter())
            {
                // Set console to write to StringWritter
                Console.SetOut(sw);

                // Simulate environment variables indicating linux consumption
                var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
                {
                    { "CONTAINER_NAME", "val1" },
                    { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                    { "WEBSITE_HOME_STAMPNAME", "val4" },
                    { "FUNCTIONS_WORKER_RUNTIME", "node" },
                });

                // Run trivial orchestrator
                using (var host = TestHelpers.GetJobHost(
                    this.loggerProvider,
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
        /// By simulating the appropriate environment variables for Linux Consumption,
        /// this test checks that we are writing our JSON logs to the console. It does not
        /// verify the contents of the JSON logs themselves (expensive) but instead checks that,
        /// at least, we are writing messages beginning with the expected linux-dedicated prefix.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WritesToConsole()
        {
            var prefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // To capture console output in a StringWritter
            using (StringWriter sw = new StringWriter())
            {
                // Set console to write to StringWritter
                Console.SetOut(sw);

                // Simulate environment variables indicating linux consumption
                var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
                {
                    { "CONTAINER_NAME", "val1" },
                    { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                    { "WEBSITE_HOME_STAMPNAME", "val4" },
                    { "FUNCTIONS_WORKER_RUNTIME", "powershell" },
                });

                // Run trivial orchestrator
                using (var host = TestHelpers.GetJobHost(
                    this.loggerProvider,
                    nameResolver: nameResolver,
                    testName: "CanWriteToConsole",
                    enableExtendedSessions: false,
                    storageProviderType: "azure_storage"))
                {
                    await host.StartAsync();
                    var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                    await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Ensure the console included prefixed logs
                Assert.Contains(prefix, consoleOutput);

                // Validate that the JSON has some minimal expected fields
                string[] lines = consoleOutput.Split('\n');
                var jsonStr = "";
                foreach (string line in lines.Where(line => line.StartsWith(prefix)))
                {
                    jsonStr = line.Replace(prefix, "");
                    JObject json = JObject.Parse(jsonStr);

                    TestHelpers.IsValidJSONLog(json);
                }
            }
        }

        /// <summary>
        /// By simulating the appropriate environment variables for Linux Dedicated,
        /// this test checks that we are writing our JSON logs to a file. It does not
        /// verify the contents of the JSON logs themselves (expensive) but instead checks that,
        /// at least, the log file we are writing to now exists in the file system.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WritesToFile()
        {
            // Set a different logging path, since the CI is Windows-based instead of linux.
            LinuxAppServiceLogger.LoggingPath = Path.Join(Directory.GetCurrentDirectory(), "logfile_WritesToFile.log");
            File.Delete(LinuxAppServiceLogger.LoggingPath); // To ensure the test generates the path
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // Simulate linux dedicated via environment variables
            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "WEBSITE_INSTANCE_ID", "val1" },
                { "FUNCTIONS_LOGS_MOUNT_PATH", "val2" },
                { "FUNCTIONS_WORKER_RUNTIME", "python" },
            });

            // Run trivial orchestrator
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameResolver: nameResolver,
                testName: "CanWriteToFile",
                enableExtendedSessions: false,
                storageProviderType: "azure_storage"))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }

            await TestHelpers.WaitUntilTrue(
                predicate: () => File.Exists(LinuxAppServiceLogger.LoggingPath),
                conditionDescription: "Log file exists",
                timeout: TimeSpan.FromSeconds(20),
                output: this.output);
        }

        /// <summary>
        /// By simulating the appropriate environment variables for Linux Consumption,
        /// this test checks that we are filtering verbose logs from DurableTask.Core by default in Linux.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task FiltersVerboseLogsByDefault()
        {
            var prefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // To capture console output in a StringWritter
            using (StringWriter sw = new StringWriter())
            {
                // Set console to write to StringWritter
                Console.SetOut(sw);

                // Simulate environment variables indicating linux consumption
                var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
                {
                    { "CONTAINER_NAME", "val1" },
                    { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                    { "WEBSITE_HOME_STAMPNAME", "val4" },
                    { "FUNCTIONS_WORKER_RUNTIME", "python" },
                });

                // Run trivial orchestrator
                using (var host = TestHelpers.GetJobHost(
                    this.loggerProvider,
                    nameResolver: nameResolver,
                    testName: "FiltersVerboseLogsByDefault",
                    enableExtendedSessions: false,
                    storageProviderType: "azure_storage"))
                {
                    await host.StartAsync();
                    var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                    await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Ensure the console included prefixed logs
                Assert.Contains(prefix, consoleOutput);

                // Validate that the JSON has some minimal expected fields
                string[] lines = consoleOutput.Split('\n');
                var jsonStr = "";
                foreach (string line in lines.Where(line => line.StartsWith(prefix)))
                {
                    jsonStr = line.Replace(prefix, "");
                    JObject json = JObject.Parse(jsonStr);

                    TestHelpers.IsValidJSONLog(json);

                    // Ensuring no DurableTask-Core Verbose logs are found
                    if ((int)json["Level"] == (int)EventLevel.Verbose)
                    {
                        Assert.False(string.Equals((string)json["ProviderName"], "DurableTask-Core", StringComparison.Ordinal));
                    }
                }
            }
        }

        /// <summary>
        /// By simulating the appropriate environment variables for Linux Consumption,
        /// this test checks that we can enable verbose logs from DurableTask.Core in Linux.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CanEnableVerboseLogsInLinux()
        {
            var prefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // To capture console output in a StringWritter
            using (StringWriter sw = new StringWriter())
            {
                // Set console to write to StringWritter
                Console.SetOut(sw);

                // Simulate environment variables indicating linux consumption
                var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
                {
                    { "CONTAINER_NAME", "val1" },
                    { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                    { "WEBSITE_HOME_STAMPNAME", "val4" },
                    { "FUNCTIONS_WORKER_RUNTIME", "python" },
                });

                // Run trivial orchestrator
                using (var host = TestHelpers.GetJobHost(
                    this.loggerProvider,
                    nameResolver: nameResolver,
                    testName: "CanEnableVerboseLogsInLinux",
                    enableExtendedSessions: false,
                    allowVerboseLinuxTelemetry: true, // enabling verbose telemetry
                    storageProviderType: "azure_storage"))
                {
                    await host.StartAsync();
                    var client = await host.StartOrchestratorAsync(orchestratorName, input: "World", this.output);
                    await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Ensure the console included prefixed logs
                Assert.Contains(prefix, consoleOutput);

                // Validate that the JSON has some minimal expected fields
                string[] lines = consoleOutput.Split('\n');
                var jsonStr = "";
                var foundVerboseLog = false;
                foreach (string line in lines.Where(line => line.StartsWith(prefix)))
                {
                    jsonStr = line.Replace(prefix, "");
                    JObject json = JObject.Parse(jsonStr);

                    TestHelpers.IsValidJSONLog(json);

                    // Ensuring DurableTask-Core Verbose logs are found
                    if (((int)json["Level"] == (int)EventLevel.Verbose)
                        && string.Equals((string)json["ProviderName"], "DurableTask-Core"))
                    {
                        foundVerboseLog = true;
                    }
                }

                Assert.True(foundVerboseLog);
            }
        }

        /// <summary>
        /// By simulating the appropriate environment variables for Linux Dedicated,
        /// this test checks our logs have their newlines escaped, which otherwise
        /// could cause problems in our logging pipeline.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RemovesNewlinesFromExceptions()
        {
            // Set a different logging path, since the CI is Windows-based instead of linux.
            const string LogFileName = "logfile_RemovesNewlinesFromExceptions.log";
            string logFilePath = Path.IsPathRooted(LogFileName)
                ? LogFileName
                : Path.Combine(Directory.GetCurrentDirectory(), LogFileName);
            LinuxAppServiceLogger.LoggingPath = logFilePath;
            File.Delete(LinuxAppServiceLogger.LoggingPath); // To ensure the test generates the path
            string orchestratorName = nameof(TestOrchestrations.ThrowOrchestrator);

            // Simulate linux dedicated via environment variables
            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "WEBSITE_INSTANCE_ID", "val1" },
                { "FUNCTIONS_LOGS_MOUNT_PATH", "val2" },
                { "FUNCTIONS_WORKER_RUNTIME", "python" },
            });

            // Run trivial orchestrator
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameResolver: nameResolver,
                testName: "RemovesNewlinesFromExceptions",
                enableExtendedSessions: false,
                storageProviderType: "azure_storage"))
            {
                await host.StartAsync();

                // This orchestrator should error out on null inputs
                var client = await host.StartOrchestratorAsync(orchestratorName, input: null, this.output);
                await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }

            await TestHelpers.WaitUntilTrue(
                predicate: () =>
                {
                    /* Exceptions have newlines embedded in them. Therefore, if there are as many lines
                     * as there are JSON (each of which has 1 EventTimestamp field), then we know that
                     * Exceptions must have had their newlines removed.
                     */
                    List<string> lines = TestHelpers.WriteSafeReadAllLines(LinuxAppServiceLogger.LoggingPath);
                    int countTimeStampCols = Regex.Matches(string.Join("", lines), "\"EventTimestamp\":").Count;
                    return lines.Count == countTimeStampCols;
                },
                conditionDescription: "Log file exists and newlines are removed from exceptions",
                timeout: TimeSpan.FromSeconds(65),
                output: this.output); // enabling at least 2 file-buffer flushes (happen every 30 seconds)
        }

        /// <summary>
        /// By simulating the appropriate environment variables for Linux Dedicated,
        /// this test checks our JSON logs satisfy a minimal set of requirements:
        /// (1) Is JSON parseable
        /// (2) Contains minimal expected fields: EventId, TimeStamp,
        ///     Tenant, SourceMoniker, Pid, Tid, etc.
        /// (3) Ensure some Enums are printed correctly.
        /// (4) That we have logs from a variety of EventSource providers.
        /// (5) Ensure ActivityId and RelatedActivityId are eventually present.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OutputsValidJSONLogs()
        {
            // Set a different logging path, since the CI is Windows-based instead of linux.
            LinuxAppServiceLogger.LoggingPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "logfile.log";
            File.Delete(LinuxAppServiceLogger.LoggingPath); // To ensure the test generates the path
            string orchestratorName = nameof(TestOrchestrations.ThrowOrchestrator);

            // Simulate linux dedicated via environment variables
            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "WEBSITE_INSTANCE_ID", "val1" },
                { "FUNCTIONS_LOGS_MOUNT_PATH", "val2" },
                { "WEBSITE_STAMP_DEPLOYMENT_ID", "val3" },
                { "WEBSITE_HOME_STAMPNAME", "val4" },
                { "FUNCTIONS_WORKER_RUNTIME", "python" },
            });

            // Run trivial orchestrator
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameResolver: nameResolver,
                testName: "OutputsValidJSONLogs",
                enableExtendedSessions: false,
                storageProviderType: "azure_storage"))
            {
                await host.StartAsync();

                // This orchestrator should error out on null inputs
                var client = await host.StartOrchestratorAsync(orchestratorName, input: null, this.output);
                await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }

            await TestHelpers.WaitUntilTrue(
                predicate: () => File.Exists(LinuxAppServiceLogger.LoggingPath),
                conditionDescription: "Log file exists",
                timeout: TimeSpan.FromSeconds(30),
                output: this.output);

            // short wait to give logs time to flush
            await Task.Delay(TimeSpan.FromSeconds(5));

            await TestHelpers.WaitUntilTrue(
                predicate: () =>
                {
                    List<string> lines = TestHelpers.WriteSafeReadAllLines(LinuxAppServiceLogger.LoggingPath);
                    IEnumerable<JObject> jsons = lines.Select(line => JObject.Parse(line));

                    if (!jsons.All(json => TestHelpers.IsValidJSONLog(json)))
                    {
                        return false;
                    }

                    if (!jsons.Any(json => ((string)json.GetValue("ProviderName")) == "DurableTask-Core"))
                    {
                        return false;
                    }

                    if (!jsons.Any(json => ((string)json.GetValue("ProviderName")) == "DurableTask-AzureStorage"))
                    {
                        return false;
                    }

                    if (!jsons.Any(json => json.Properties().Select(p => p.Name).ToList().Contains("ActivityId")))
                    {
                        return false;
                    }

                    if (!jsons.Any(json => json.Properties().Select(p => p.Name).ToList().Contains("RelatedActivityId")))
                    {
                        return false;
                    }

                    if (jsons.Any(json =>
                        {
                            var eventType = (string)json.GetValue("EventType");
                            return !string.IsNullOrEmpty(eventType) && eventType.All(char.IsDigit);
                        }))
                    {
                        return false;
                    }

                    return true;
                },
                conditionDescription: "Log file contains all required fields and expected events",
                timeout: TimeSpan.FromSeconds(35),
                output: this.output);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task AzureStorage_FirstRetryIntervalLimitHit_ThrowsException()
        {
            string orchestrationFunctionName = nameof(TestOrchestrations.SimpleActivityRetrySuccceds);

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                "AzureStorageFirstRetryIntervalException", // Need custom name so don't exceed 50 chars
                false))
            {
                await host.StartAsync();

                var firstRetryInterval = TimeSpan.FromDays(7);
                var maxRetryInterval = TimeSpan.FromDays(1);

                var client = await host.StartOrchestratorAsync(orchestrationFunctionName, (firstRetryInterval, maxRetryInterval), this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = status.Output.ToString();
                Assert.Contains("FirstRetryInterval", output);
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task AzureStorage_MaxRetryIntervalLimitHit_ThrowsException()
        {
            string orchestrationFunctionName = nameof(TestOrchestrations.SimpleActivityRetrySuccceds);

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                "AzureStorageMaxRetryIntervalException", // Need custom name so don't exceed 50 chars
                false))
            {
                await host.StartAsync();

                var firstRetryInterval = TimeSpan.FromDays(1);
                var maxRetryInterval = TimeSpan.FromDays(7);

                var client = await host.StartOrchestratorAsync(orchestrationFunctionName, (firstRetryInterval, maxRetryInterval), this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = status.Output.ToString();
                Assert.Contains("MaxRetryInterval", output);
            }
        }

        /// <summary>
        /// End-to-end test which validates that QueueClientMessageEncoding.Base64 works correctly with a Hello World orchestration.
        /// And with base64-queueclient we can support orchestration with escaped characters.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task HelloWorld_QueueClientMessageEncoding_Base64()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var options = new DurableTaskOptions();

            // Configure Azure Storage provider with Base64 message encoding
            options.StorageProvider["ConnectionName"] = "AzureWebJobsStorage";
            options.StorageProvider["QueueClientMessageEncoding"] = "Base64";

            // Create input with escaped characters including 0xFFFE
            string inputWithEscapedChars = "World\uFFFE\u0001\u0002\u0003";

            using (var host = TestHelpers.GetJobHostWithOptions(
                this.loggerProvider,
                durableTaskOptions: options,
                storageProviderType: TestHelpers.AzureStorageProviderType))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], inputWithEscapedChars, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(inputWithEscapedChars, status.Input);
                Assert.Equal($"Hello, {inputWithEscapedChars}!", status.Output);

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task MaxOrchestrationAction_MaxReached_OrchestrationFails()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.AllOrchestratorActivityActions),
            };

            DurableTaskOptions options = new DurableTaskOptions();
            var maxActions = 7;
            options.MaxOrchestrationActions = maxActions;
            options.LocalRpcEndpointEnabled = false;

            using (var host = TestHelpers.GetJobHostWithOptions(
                this.loggerProvider,
                options))
            {
                await host.StartAsync();

                var counterEntityId = new EntityId("Counter", Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counterEntityId, this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal("AllAPICallsUsed", status.CustomStatus);
                Assert.Equal(
                    $"Orchestrator function 'AllOrchestratorActivityActions' failed: Maximum amount of orchestration actions ({maxActions}) has been reached. " +
                    $"This value can be configured in host.json file as MaxOrchestrationActions.",
                    status.Output.ToString());

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivity_Like_From_Azure_Portal()
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.CallActivity_Like_From_Azure_Portal),
                false))
            {
                string foo = "return_result";
                await host.StartAsync();
                string functionName = nameof(TestActivities.BindToPOCOWithOutParameter);
                var startFunction = typeof(TestActivities).GetMethod(functionName);
                string[] output = new string[1];
                var args = new Dictionary<string, object>
                {
                    { "poco", $"{{ \"Foo\": \"{foo}\" }}" },
                    { "outputWrapper", output },
                };

                await host.CallAsync(startFunction, args);
                this.output.WriteLine($"Started {functionName}");

                Assert.Equal(foo, output[0]);
                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MultipleHostsOnSameVM(bool enableLocalRpc)
        {
            // This test wants to be sure there are no race conditions while starting up multiple hosts in parallel,
            // so attempt various times to increase the likelihood of hitting a race condition if one exists.
            int numAttempts = 3;
            for (int attempt = 0; attempt < numAttempts; attempt++)
            {
                int numThreads = 10;
                var hosts = new List<ITestHost>(numThreads);

                try
                {
                    Parallel.For(0, numThreads, new ParallelOptions() { MaxDegreeOfParallelism = numThreads }, (i) =>
                        hosts.Add(TestHelpers.GetJobHost(
                                this.loggerProvider,
                                nameof(this.MultipleHostsOnSameVM) + i,
                                false,
                                localRpcEndpointEnabled: enableLocalRpc)));

                    await Task.WhenAll(hosts.Select(host => host.StartAsync()));
                }
                catch (AggregateException ex)
                {
                    Assert.Fail($"Could not start up two hosts on the same device in parallel. AggregateException: {ex}");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.Fail($"Could not start up two hosts on the same device in parallel. InvalidOperationException: {ex}");
                }
                finally
                {
                    await Task.WhenAll(hosts.Select(async host =>
                    {
                        await host.StopAsync();
                        host.Dispose();
                    }));
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates that bad input for task hub name throws instance of <see cref="ArgumentException"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData("Task-Hub-Name-Test")]
        [InlineData("1TaskHubNameTest")]
        [InlineData("/TaskHubNameTest")]
        [InlineData("-taskhubnametest")]
        [InlineData("taskhubnametesttaskhubnametesttaskhubnametesttaskhubnametesttaskhubnametesttaskhubnametest")]
        public async Task TaskHubName_Throws_ArgumentException(string taskHubName)
        {
            ArgumentException argumentException =
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    using (var host = TestHelpers.GetJobHost(
                        this.loggerProvider,
                        taskHubName,
                        false,
                        exactTaskHubName: taskHubName + PlatformSpecificHelpers.VersionSuffix))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

            Assert.NotNull(argumentException);
            Assert.Equal(
                argumentException.Message.Contains($"{taskHubName}V1")
                    ? $"Task hub name '{taskHubName}V1' should contain only alphanumeric characters, start with a letter, and have length between 3 and 45."
                    : $"Task hub name '{taskHubName}V2' should contain only alphanumeric characters, start with a letter, and have length between 3 and 45.",
                argumentException.Message);
        }

        /// <summary>
        /// Tests default and custom values for task hub name/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null, "TestSiteName", "Production")]
        [InlineData(null, "TestSiteName", null)]
        [InlineData("CustomName", "TestSiteName", "Production")]
        [InlineData("CustomName", "TestSiteName", null)]
        [InlineData("CustomName", "TestSiteName", "Test")]
        [InlineData("TestSiteName", "TestSiteName", "Test")]
        public void TaskHubName_HappyPath(string customHubName, string siteName, string slotName)
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", siteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", slotName);

                var options = new DurableTaskOptions();
                options.LocalRpcEndpointEnabled = false;

                var expectedHubName = siteName;

                if (customHubName != null)
                {
                    expectedHubName = customHubName;
                    options.HubName = customHubName;
                }

                using (TestHelpers.GetJobHostWithOptions(this.loggerProvider, options))
                {
                    Assert.Equal(expectedHubName, options.HubName);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }

        /// <summary>
        /// Tests default and custom values for task hub name/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("Task-Hub-Name-Test", "TaskHubNameTest")]
        [InlineData("1TaskHubNameTest", "t1TaskHubNameTest")]
        [InlineData("-taskhubnametest2", "taskhubnametest2")]
        [InlineData("-2taskhubnametest", "t2taskhubnametest")]
        [InlineData("--------", "DefaultTaskHub")]
        [InlineData("bb", "bbHub")]
        public async Task TaskHubName_DefaultHubName_UseSanitized(string siteName, string expectedHubName)
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", siteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "Production");

                var options = new DurableTaskOptions();
                options.LocalRpcEndpointEnabled = false;

                using (var host = TestHelpers.GetJobHostWithOptions(this.loggerProvider, options))
                {
                    await host.StartAsync();
                    Assert.Equal(expectedHubName, options.HubName);
                    await host.StopAsync();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }

        /// <summary>
        /// Tests that an attempt to use a default task hub name while in a test slot will throw an exception <see cref="InvalidOperationException"/>.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TaskHubName_DefaultNameNonProductionSlot_ThrowsException()
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "TestSiteName");
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "Test");
                DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
                durableTaskOptions.LocalRpcEndpointEnabled = false;

                InvalidOperationException exception =
                    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    using (var host = TestHelpers.GetJobHostWithOptions(
                        this.loggerProvider,
                        durableTaskOptions))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

                Assert.NotNull(exception);
                Assert.Contains("Task Hub name must be specified in host.json when using slots", exception.Message);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TaskHubName_AppSettingReference_ValidTaskHub_UsesResolvedTaskHub()
        {
            string taskHubSettingName = "TaskHubName";
            string taskHubName = "ValidTaskHub";
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            durableTaskOptions.HubName = $"%{taskHubSettingName}%";

            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { taskHubSettingName, taskHubName },
            });

            using (var host = TestHelpers.GetJobHostWithOptions(
                this.loggerProvider,
                durableTaskOptions,
                nameResolver: nameResolver))
            {
                await host.StartAsync();
                await host.StopAsync();
            }

            Assert.Equal(taskHubName, durableTaskOptions.HubName);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TaskHubName_AppSettingReference_InvalidTaskHub_ThrowsException()
        {
            string taskHubSettingName = "TaskHubName";
            string taskHubName = "Invalid-Task-Hub";
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            durableTaskOptions.HubName = $"%{taskHubSettingName}%";

            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { taskHubSettingName, taskHubName },
            });

            string expectedResolvedName = taskHubName;
            ArgumentException argumentException =
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    using (var host = TestHelpers.GetJobHostWithOptions(
                        this.loggerProvider,
                        durableTaskOptions,
                        nameResolver: nameResolver))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

            Assert.NotNull(argumentException);
            Assert.Equal(
                $"Task hub name '{expectedResolvedName}' should contain only alphanumeric characters, start with a letter, and have length between 3 and 45.",
                argumentException.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ExtendedSessions_OutOfProc_SetToFalse()
        {
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            durableTaskOptions.HubName = "ExtendedSessionsTestNode";
            durableTaskOptions.ExtendedSessionsEnabled = true;

            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "FUNCTIONS_WORKER_RUNTIME", "node" },
            });

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    using (var host = TestHelpers.GetJobHostWithOptions(
                        this.loggerProvider,
                        durableTaskOptions,
                        nameResolver: nameResolver))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

            Assert.NotNull(exception);
            Assert.StartsWith(
                "Durable Functions with extendedSessionsEnabled set to 'true' is only supported when using",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ExtendedSessions_CSharp_RemainsTrue()
        {
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            durableTaskOptions.HubName = "ExtendedSessionsTestCSharp";
            durableTaskOptions.ExtendedSessionsEnabled = true;

            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "FUNCTIONS_WORKER_RUNTIME", "dotnet" },
            });

            using (var host = TestHelpers.GetJobHostWithOptions(
                this.loggerProvider,
                durableTaskOptions,
                nameResolver: nameResolver))
            {
                await host.StartAsync();
                await host.StopAsync();
            }

            Assert.True(durableTaskOptions.ExtendedSessionsEnabled);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ExtendedSessions_UnknownLanguage_RemainsTrue()
        {
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            durableTaskOptions.HubName = "ExtendedSessionsUnknownLanguage";
            durableTaskOptions.ExtendedSessionsEnabled = true;

            var nameResolver = new SimpleNameResolver();

            using (var host = TestHelpers.GetJobHostWithOptions(
                this.loggerProvider,
                durableTaskOptions,
                nameResolver: nameResolver))
            {
                await host.StartAsync();
                await host.StopAsync();
            }

            Assert.True(durableTaskOptions.ExtendedSessionsEnabled);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CustomIMessageSerializerSettingsFactory()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ComplexTypeOrchestrator),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.CustomIMessageSerializerSettingsFactory),
                true,
                serializerSettings: new CustomEnumSettings()))
            {
                await host.StartAsync();

                var inputWithEnum = new ComplexType
                {
                    A = -42,
                    B = new List<DateTime> { DateTime.UtcNow, DateTime.UtcNow.AddYears(1) },
                    C = ComplexType.CustomEnum.Value2,
                    D = new ComplexType.ComplexInnerType
                    {
                        E = Guid.NewGuid().ToString(),
                        F = TimeSpan.FromHours(1.5),
                    },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], inputWithEnum, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Contains("Value2", status.Output.ToString());

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CustomSerializerSettings_TypeNameHandlingAll()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.CustomIMessageSerializerSettingsFactory),
                true,
                serializerSettings: new CustomTypeNameHandlingSettings()))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", (string)status.Input);
                Assert.Equal("Hello, World!", (string)status.Output);

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DefaultIMessageSerializerSettingsFactory()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ComplexTypeOrchestrator),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DefaultIMessageSerializerSettingsFactory),
                true))
            {
                await host.StartAsync();

                var inputWithEnum = new ComplexType
                {
                    A = -42,
                    B = new List<DateTime> { DateTime.UtcNow, DateTime.UtcNow.AddYears(1) },
                    C = ComplexType.CustomEnum.Value2,
                    D = new ComplexType.ComplexInnerType
                    {
                        E = Guid.NewGuid().ToString(),
                        F = TimeSpan.FromHours(1.5),
                    },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], inputWithEnum, this.output);
                await client.WaitForCompletionAsync(this.output);
                var status = await client.GetStatusAsync();

                Assert.NotNull(status);
                Assert.DoesNotContain("Value2", status.Output.ToString());

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TaskHubName_DefaultNameSiteTooLong_UsesSanitizedHubName()
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", new string('a', 100));
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", null);

                var options = new DurableTaskOptions();

                var expectedHubName = new string('a', 45);

                using (var host = TestHelpers.GetJobHostWithOptions(this.loggerProvider, options))
                {
                    await host.StartAsync();
                    Assert.Equal(expectedHubName, options.HubName);
                    await host.StopAsync();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ReplaySafeLogger_LogsOnlyOnce()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ReplaySafeLogger_OneLogMessage),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ReplaySafeLogger_LogsOnlyOnce),
                false))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                var logger = this.loggerProvider.CreatedLoggers.FirstOrDefault(l => l.Category.Equals("Function.ReplaySafeLogger_OneLogMessage.User"));
                var logMessages = logger.LogMessages.Where(
                    msg => msg.FormattedMessage.Contains("ReplaySafeLogger Test: About to say Hello")).ToList();
                Assert.Single(logMessages);

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ValidateExtensionLifecycleLogs()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ValidateExtensionLifecycleLogs),
                false))
            {
                // simply starting and stopping should generate all the logs we need to check for
                await host.StartAsync();
                await Task.Delay(1000);
                await host.StopAsync();
            }

            TestLogger testLogger = this.loggerProvider.CreatedLoggers.Single(
                logger => logger.Category == TestHelpers.LogCategory);

            // Ensure the basic startup/shutdown logs are present
            Assert.Single(testLogger.LogMessages, msg => msg.FormattedMessage.Contains("Starting task hub worker"));
            Assert.Single(testLogger.LogMessages, msg => msg.FormattedMessage.Contains("Task hub worker started"));
            Assert.Single(testLogger.LogMessages, msg => msg.FormattedMessage.Contains("Stopping task hub worker"));
            Assert.Single(testLogger.LogMessages, msg => msg.FormattedMessage.Contains("Task hub worker stopped"));

            // Ensure the configuration log is present and contains valid JSON.
            // Expected format: "Durable extension configuration loaded: {json}. HubName: ..."
            const string PrefixText = "Durable extension configuration loaded: ";
            LogMessage configMessage = Assert.Single(testLogger.LogMessages, msg => msg.FormattedMessage.Contains(PrefixText));
            string configMessageText = configMessage.FormattedMessage;
            int start = configMessageText.IndexOf(PrefixText) + PrefixText.Length;
            int end = configMessageText.IndexOf(". HubName: ", start);
            Assert.NotEqual(-1, end);
            string configJson = configMessageText.Substring(start, end - start);

            // This will throw if the JSON is not valid
            JObject.Parse(configJson);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestStoreInputsInOrchestrationHistory()
        {
            DurableTaskOptions options = new DurableTaskOptions();
            options.StoreInputsInOrchestrationHistory = true;

            using (var host = TestHelpers.GetJobHostWithOptions(
                   this.loggerProvider,
                   options))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivityWithorWithoutInput), null, this.output);
                await client.WaitForCompletionAsync(this.output);
                var status = await client.InnerClient.GetStatusAsync(client.InstanceId, showHistory: true, showInput: true);

                var input1 = status.History[1].Value<string>("Input");
                var input2 = status.History[2].Value<string>("Input");
                Assert.Equal("[\"Tokyo\"]", input1);
                Assert.Equal("[null]", input2);

                await host.StopAsync();
            }
        }

        // JsonSerializerSettings with StringEnumConverter
        private class CustomEnumSettings : IMessageSerializerSettingsFactory
        {
            public JsonSerializerSettings CreateJsonSerializerSettings()
            {
                var serializer = new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.None,
                };

                serializer.Converters.Add(new StringEnumConverter());

                return serializer;
            }
        }

        // JsonSerializerSettings with TypeNameHandling.All
        private class CustomTypeNameHandlingSettings : IMessageSerializerSettingsFactory
        {
            public JsonSerializerSettings CreateJsonSerializerSettings()
            {
                return new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.All,
                };
            }
        }
    }
}
