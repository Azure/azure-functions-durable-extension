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
using DurableTask.AzureStorage;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Diagnostics.Tracing;
#if !FUNCTIONS_V1
using Microsoft.Extensions.Hosting;
using WebJobs.Extensions.DurableTask.Tests.V2;
#endif
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskEndToEndTests : IDisposable
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;
        private readonly bool useTestLogger = IsLogFriendlyPlatform();
        private readonly LogEventTraceListener eventSourceListener;

        private static readonly string InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

        public DurableTaskEndToEndTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
            this.eventSourceListener = new LogEventTraceListener();
            this.StartLogCapture();
        }

        public void Dispose()
        {
            this.eventSourceListener.Dispose();
        }

        // Testing on Linux currently throws exception in LogEventTraceListener.
        // May also need to limit on OSX.
        private static bool IsLogFriendlyPlatform()
        {
            return !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }

        private void StartLogCapture()
        {
            if (this.useTestLogger)
            {
                // Use GUID for eventsource, as TraceEventProviders.GetProviderGuidByName() is causing
                // the CI to abort runs.
                var traceConfig = new Dictionary<string, TraceEventLevel>
                {
                    { "4c4ad4a2-f396-5e18-01b6-618c12a10433", TraceEventLevel.Informational }, // DurableTask.AzureStorage
                    { "7DA4779A-152E-44A2-A6F2-F80D991A5BEE", TraceEventLevel.Warning }, // DurableTask.Core
                };

                this.eventSourceListener.OnTraceLog += this.OnEventSourceListenerTraceLog;

                string sessionName = "DTFxTrace" + Guid.NewGuid().ToString("N");
                this.eventSourceListener.CaptureLogs(sessionName, traceConfig);
            }
        }

        private void OnEventSourceListenerTraceLog(object sender, LogEventTraceListener.TraceLogEventArgs e)
        {
            this.output.WriteLine($"      ETW: {e.ProviderName} [{e.Level}] : {e.Message}");
        }

        /// <summary>
        /// End-to-end test which validates a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, TestHelpers.AzureStorageProviderType)]
        [InlineData(false, TestHelpers.AzureStorageProviderType)]
#if !FUNCTIONS_V1
        [InlineData(true, TestHelpers.EmulatorProviderType)]
        [InlineData(false, TestHelpers.EmulatorProviderType)]
#endif
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

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

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

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                // Next, start an orchestration from the client host and verify that it completes on the orchestration host.
                client = await clientHost.StartOrchestratorAsync(
                    nameof(TestOrchestrations.SayHelloInline),
                    "World",
                    this.output,
                    useTaskHubFromAppSettings: true);
                status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

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
                var status = await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }
        }

#if !FUNCTIONS_V1
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

            var storageAccountProvider = new CustomAccountStorageProvider(appSettings);

            using (IHost clientHost = TestHelpers.GetJobHostExternalEnvironment(storageAccountProvider))
            {
                await clientHost.StartAsync();
                IDurableClientFactory durableClientFactory = clientHost.Services.GetService(typeof(IDurableClientFactory)) as DurableClientFactory;
                IDurableClient durableClient = durableClientFactory.CreateClient(durableClientOptions);
                Assert.Equal(taskHubName, durableClient.TaskHubName);
                await clientHost.StopAsync();
            }
        }
#endif

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

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("", status.Output.ToString());

                await host.StopAsync();
            }
        }

#if !FUNCTIONS_V1
        /// <summary>
        /// By simulating the appropiate environment variables for Linux Consumption,
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

                // Simulate enviroment variables indicating linux consumption
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

        /// <summary>
        /// By simulating the appropiate environment variables for Linux Consumption,
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

                // Simulate enviroment variables indicating linux consumption
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
        /// By simulating the appropiate enviorment variables for Linux Consumption,
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

                // Simulate enviroment variables indicating linux consumption
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
                    var status = await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Ensure the console included prefixed logs
                Assert.Contains(prefix, consoleOutput);

                // Validate that the JSON has some minimal expected fields
                string[] lines = consoleOutput.Split('\n');
                var jsonStr = "";
                foreach (string line in lines)
                {
                    if (line.StartsWith(prefix))
                    {
                        jsonStr = line.Replace(prefix, "");
                        JObject json = JObject.Parse(jsonStr);

                        TestHelpers.IsValidJSONLog(json);
                    }
                }
            }
        }

        /// <summary>
        /// By simulating the appropiate enviorment variables for Linux Dedicated,
        /// this test checks that we are writing our JSON logs to a file. It does not
        /// verify the contents of the JSON logs themselves (expensive) but instead checks that,
        /// at least, the log file we are writing to now exists in the file system.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WritesToFile()
        {
            // Set a different logging path, since the CI is Windows-based instead of linux.
            LinuxAppServiceLogger.LoggingPath = Path.Combine(Directory.GetCurrentDirectory(), "logfile_WritesToFile.log");
            File.Delete(LinuxAppServiceLogger.LoggingPath); // To ensure the test generates the path
            string orchestratorName = nameof(TestOrchestrations.SayHelloInline);

            // Simulate linux dedicated via enviroment variables
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
                var status = await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }

            await TestHelpers.WaitUntilTrue(
                predicate: () => File.Exists(LinuxAppServiceLogger.LoggingPath),
                conditionDescription: "Log file exists",
                timeout: TimeSpan.FromSeconds(20));
        }

        /// <summary>
        /// By simulating the appropiate enviorment variables for Linux Consumption,
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

                // Simulate enviroment variables indicating linux consumption
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
                    var status = await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Ensure the console included prefixed logs
                Assert.Contains(prefix, consoleOutput);

                // Validate that the JSON has some minimal expected fields
                string[] lines = consoleOutput.Split('\n');
                var jsonStr = "";
                foreach (string line in lines)
                {
                    if (line.StartsWith(prefix))
                    {
                        jsonStr = line.Replace(prefix, "");
                        JObject json = JObject.Parse(jsonStr);

                        TestHelpers.IsValidJSONLog(json);

                        // Ensuring no DurableTask-Core Verbose logs are found
                        if ((int)json["Level"] == (int)EventLevel.Verbose)
                        {
                            Assert.False(json["ProviderName"].Equals("DurableTask-Core"));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// By simulating the appropiate enviorment variables for Linux Consumption,
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

                // Simulate enviroment variables indicating linux consumption
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
                    var status = await client.WaitForCompletionAsync(this.output);
                    await host.StopAsync();
                }

                string consoleOutput = sw.ToString();

                // Ensure the console included prefixed logs
                Assert.Contains(prefix, consoleOutput);

                // Validate that the JSON has some minimal expected fields
                string[] lines = consoleOutput.Split('\n');
                var jsonStr = "";
                var foundVerboseLog = false;
                foreach (string line in lines)
                {
                    if (line.StartsWith(prefix))
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
                }

                Assert.True(foundVerboseLog);
            }
        }

        /// <summary>
        /// By simulating the appropiate enviorment variables for Linux Dedicated,
        /// this test checks our logs have their newlines escaped, which otherwise
        /// could cause problems in our logging pipeline.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RemovesNewlinesFromExceptions()
        {
            // Set a different logging path, since the CI is Windows-based instead of linux.
            LinuxAppServiceLogger.LoggingPath = Path.Combine(Directory.GetCurrentDirectory(), "logfile_RemovesNewlinesFromExceptions.log");
            File.Delete(LinuxAppServiceLogger.LoggingPath); // To ensure the test generates the path
            string orchestratorName = nameof(TestOrchestrations.ThrowOrchestrator);

            // Simulate linux dedicated via enviroment variables
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
                var status = await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }

            await TestHelpers.WaitUntilTrue(
                predicate: () =>
                {
                    /* Exceptions have newlines embeded in them. Therefore, if there are as many lines
                     * as there are JSON (each of which has 1 EventTimestamp field), then we know that
                     * Exceptions must have had their newlines removed.
                     */
                    List<string> lines = TestHelpers.WriteSafeReadAllLines(LinuxAppServiceLogger.LoggingPath);
                    int countTimeStampCols = Regex.Matches(string.Join("", lines), "\"EventTimestamp\":").Count;
                    return lines.Count == countTimeStampCols;
                },
                conditionDescription: "Log file exists and newlines are removed from exceptions",
                timeout: TimeSpan.FromSeconds(65)); // enabling at least 2 file-buffer flushes (happen every 30 seconds)
        }

        /// <summary>
        /// By simulating the appropiate enviorment variables for Linux Dedicated,
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
            LinuxAppServiceLogger.LoggingPath = Path.Combine(Directory.GetCurrentDirectory(), "logfile.log");
            File.Delete(LinuxAppServiceLogger.LoggingPath); // To ensure the test generates the path
            string orchestratorName = nameof(TestOrchestrations.ThrowOrchestrator);

            // Simulate linux dedicated via enviroment variables
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
                var status = await client.WaitForCompletionAsync(this.output);
                await host.StopAsync();
            }

            await TestHelpers.WaitUntilTrue(
                predicate: () => File.Exists(LinuxAppServiceLogger.LoggingPath),
                conditionDescription: "Log file exists",
                timeout: TimeSpan.FromSeconds(30));

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
                            var val = !string.IsNullOrEmpty(eventType) && eventType.All(char.IsDigit);
                            return !string.IsNullOrEmpty(eventType) && eventType.All(char.IsDigit);
                        }))
                    {
                        return false;
                    }

                    return true;
                },
                conditionDescription: "Log file contains all required fields and expected events",
                timeout: TimeSpan.FromSeconds(35));
        }
#endif

        /// <summary>
        /// End-to-end test which runs a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_Activity(bool extendedSessions, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_Activity), extendedSessions, storageProvider);
        }

        /// <summary>
        /// End-to-end test which validates logs for replay events by a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_ValidateReplayEventLogs(bool traceReplayEvents, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_ValidateReplayEventLogs), false, storageProvider, traceReplayEvents: traceReplayEvents);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_Activity_History(bool extendedSessions, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_Activity_History), extendedSessions, storageProvider, showHistory: true);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information with input and result date is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_ShowHistoryInputOutput(bool extendedSessions, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_ShowHistoryInputOutput), extendedSessions, storageProvider, showHistory: true, showHistoryOutput: true);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that the generated GUID-s from the DurableOrchestrationContext are the same.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldActivityWithNewGUID(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityWithDeterministicGuid),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldActivityWithNewGUID),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status =
                    await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("True", status.Output.ToString());
            }
        }

        /// <summary>
        ///  End-to-end test which  validates that <see cref="DurableOrchestrationContext"/> NewGuid method creates unique GUIDs.
        ///  The tests creates 10,000 GUIDs and validates that all the values are unique.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task VerifyUniqueGuids(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.VerifyUniqueGuids),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.VerifyUniqueGuids),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                DurableOrchestrationStatus status =
                    await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Empty(status.Input.ToString());
                Assert.Equal("True", status.Output.ToString());
            }
        }

        /// <summary>
        ///  End-to-end test which  validates that <see cref="DurableOrchestrationContext"/> NewGuid method creates the same GUIDs on replay.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task VerifySameGuidsOnReplay(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.VerifySameGuidGeneratedOnReplay),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.VerifySameGuidsOnReplay),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                DurableOrchestrationStatus status =
                    await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Empty(status.Input.ToString());
                Assert.Equal("True", status.Output.ToString());
            }
        }

        private async Task HelloWorldOrchestration_Activity_Main_Logic(string taskHubName, bool extendedSessions, string storageProvider, bool showHistory = false, bool showHistoryOutput = false, bool traceReplayEvents = true)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                taskHubName,
                extendedSessions,
                traceReplayEvents: traceReplayEvents,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(this.output, showHistory, showHistoryOutput);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);
                if (!showHistory)
                {
                    Assert.Null(status.History);
                }
                else
                {
                    Assert.Equal(3, status.History.Count);
                    Assert.Equal("ExecutionStarted", status.History[0]["EventType"].ToString());
                    Assert.Equal("SayHelloWithActivity", status.History[0]["FunctionName"].ToString());
                    Assert.Equal("TaskCompleted", status.History[1]["EventType"].ToString());
                    Assert.Equal("Hello", status.History[1]["FunctionName"].ToString());
                    if (DateTime.TryParse(status.History[1]["Timestamp"].ToString(), out DateTime timestamp) &&
                        DateTime.TryParse(status.History[1]["ScheduledTime"].ToString(), out DateTime scheduledTime))
                    {
                        Assert.True(timestamp >= scheduledTime);
                    }

                    Assert.Equal("ExecutionCompleted", status.History[2]["EventType"].ToString());
                    Assert.Equal("Completed", status.History[2]["OrchestrationStatus"].ToString());

                    if (showHistoryOutput)
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.NotNull(status.History[1]["Result"]);
                        Assert.Equal("Hello, World!", status.History[1]["Result"].ToString());
                        Assert.NotNull(status.History[2]["Result"]);
                        Assert.Equal("Hello, World!", status.History[2]["Result"].ToString());
                    }
                    else
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.Null(status.History[1]["Result"]);
                        Assert.Null(status.History[2]["Result"]);
                    }

                    Assert.NotNull(status.History);
                }

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Activity",
                        client.InstanceId,
                        extendedSessions || !traceReplayEvents,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_Activity_CustomStatus(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityAndCustomStatus),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Activity_CustomStatus),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal(
                    new JObject
                    {
                        { "nextActions", new JArray("A", "B", "C") },
                        { "foo", 2 },
                    },
                    (JToken)status.CustomStatus);
                Assert.Equal("Hello, World!", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Activity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates fire-and-forget of a suborchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task FireAndForgetSuborchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.FireAndForgetSuborchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FireAndForgetHelloOrchestration), null, this.output);

                // Wait for it to complete
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                string subOrchestrationInstanceId = (string)status?.Output;

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

                do
                {
                    status = await client.InnerClient.GetStatusAsync(subOrchestrationInstanceId);
                    await Task.Delay(50);
                }
                while (DateTime.UtcNow <= deadline
                        && status?.RuntimeStatus != OrchestrationRuntimeStatus.Completed);

                Assert.Equal("Hello, Heloise!", (string)status?.Output);
                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates function chaining by implementing a naive factorial function orchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SequentialOrchestration(bool extendedSessions, string storageProvider)
        {
            string instanceId;
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.SequentialOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Factorial), 10, this.output);
                instanceId = client.InstanceId;

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(10, status?.Input);
                Assert.Equal(3628800, status?.Output);

                await host.StopAsync();
            }

            // Assert log entry count
            if (this.useTestLogger)
            {
                var logger = this.loggerProvider.CreatedLoggers.Single(l => l.Category == TestHelpers.LogCategory);
                var logMessages = logger.LogMessages.Where(
                    msg => msg.FormattedMessage.Contains(instanceId)).ToList();

                int expectedLogMessageCount = extendedSessions ? 43 : 153;
                Assert.Equal(expectedLogMessageCount, logMessages.Count);
            }
        }

        /// <summary>
        /// End-to-end test which validates parallel function execution by enumerating all files in the current directory
        /// in parallel and getting the sum total of all file sizes.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ParallelOrchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ParallelOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.DiskUsage), Environment.CurrentDirectory, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(90));

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(Environment.CurrentDirectory, status?.Input);
                Assert.True((long?)status?.Output > 0L);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the ContinueAsNew functionality by implementing a counter actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActorOrchestration(bool extendedSessions, string storageProvider)
        {
            string instanceId;
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActorOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);
                instanceId = client.InstanceId;

                // Wait for the instance to go into the Running state. This is necessary to ensure log validation consistency.
                await client.WaitForStartupAsync(this.output);

                TimeSpan waitTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 300 : 5);

                // Perform some operations
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 1);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 3);
                await client.RaiseEventAsync("operation", "decr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 3);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.GetStatusAsync();
                Assert.True(
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                // The end message will cause the actor to complete itself.
                await client.RaiseEventAsync("operation", "end", this.output);

                status = await client.WaitForCompletionAsync(this.output, timeout: waitTimeout);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(3, (int?)status?.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.Equal(3, (int)status.Input);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        nameof(this.ActorOrchestration),
                        client.InstanceId,
                        extendedSessions,
                        new[] { nameof(TestOrchestrations.Counter) });
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the ContinueAsNew functionality by implementing a counter actor pattern,
        /// and does so without any waiting between sending events.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActorOrchestration_NoWaiting(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActorOrchestration_NoWaiting),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "decr", this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "end", this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(3, (int?)status?.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.Equal(3, (int)status.Input);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BatchedActorOrchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.BatchedActorOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.BatchActor), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("newItem", "item1", this.output);
                await client.RaiseEventAsync("newItem", "item2", this.output);
                await client.RaiseEventAsync("newItem", "item3", this.output);
                await client.RaiseEventAsync("newItem", "item4", this.output);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.WaitForStartupAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Sending this last item will cause the actor to complete itself.
                await client.RaiseEventAsync("newItem", "item5", this.output);

                status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BatchedActorOrchestrationDeleteLastItemAlways(bool extendedSessions, string storageProvider)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.BatchedActorOrchestrationDeleteLastItemAlways),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.BatchActorRemoveLast), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item5
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item4
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item3
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item2

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.WaitForStartupAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Sending this last event will cause the actor to complete itself.
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item1

                status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the parallel wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ParallelBatchedActorOrchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ParallelBatchedActorOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ParallelBatchActor), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("newItem", "item1", this.output);
                await client.RaiseEventAsync("newItem", "item2", this.output);
                await client.RaiseEventAsync("newItem", "item3", this.output);

                // Make sure it's still running and didn't complete early (or fail).
                await client.WaitForStartupAsync(this.output);
                await Task.Delay(TimeSpan.FromSeconds(5));
                var status = await client.GetStatusAsync();
                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Sending this last item will cause the actor to complete itself.
                await client.RaiseEventAsync("newItem", "item4", this.output);
                status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ExternalEvents_MultipleNamesLooping(bool extendedSessions, string storageProvider)
        {
            const string testName = nameof(this.ExternalEvents_MultipleNamesLooping);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter2), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("incr", null, this.output);
                await client.RaiseEventAsync("incr", null, this.output);
                await client.RaiseEventAsync("done", null, this.output);

                // Make sure it actually completed
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(2, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the Terminate functionality.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task TerminateOrchestration(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Counter),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TerminateOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Using the counter orchestration because it will wait indefinitely for input.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], 0, this.output);

                // Need to wait for the instance to start before we can terminate it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(this.output);

                await client.TerminateAsync("sayōnara");

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Terminated, status?.RuntimeStatus);
                Assert.Equal("sayōnara", status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "TerminateOrchestration",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the Rewind functionality.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task RewindOrchestration(string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityForRewind),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.RewindOrchestration),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "Catherine", this.output);

                await client.WaitForStartupAsync(this.output);

                var statusFail = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, statusFail?.RuntimeStatus);

                TestOrchestrations.SayHelloWithActivityForRewindShouldFail = false;

                await client.RewindAsync("rewind!");

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Hello, Catherine!", status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "RewindOrchestration",
                        client.InstanceId,
                        false /* filterOutReplayLogs */,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the cancellation of durable timers.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task TimerCancellation(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TimerCancellation),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);
                await client.WaitForStartupAsync(this.output);
                await client.RaiseEventAsync("approval", eventData: true, output: this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Approved", status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "TimerCancellation",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of durable timer expiration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task TimerExpiration(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TimerExpiration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(2);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);
                await client.WaitForStartupAsync(this.output);

                // Don't send any notification - let the internal timeout expire

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Expired", status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "TimerExpiration",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the overloads of WaitForExternalEvent with timeout.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("throw", false, "TimeoutException")]
        [InlineData("throw", true, "ApprovalValue")]
        [InlineData("default", true, "ApprovalValue")]
        [InlineData("default", false, "default")]
        public async Task WaitForExternalEventWithTimeout(string defaultValue, bool sendEvent, string expectedResponse)
        {
            var orchestratorFunctionNames = new[] { nameof(TestOrchestrations.ApprovalWithTimeout) };
            var extendedSessions = false;
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.WaitForExternalEventWithTimeout),
                extendedSessions))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], (timeout, defaultValue), this.output);
                await client.WaitForStartupAsync(this.output);

                // Don't send any notification - let the internal timeout expire
                if (sendEvent)
                {
                    await client.RaiseEventAsync("Approval", "ApprovalValue", this.output);
                }

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(expectedResponse, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a CancellationToken-providing overload of WaitForExternalEvent.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForExternalEventWithCancellationToken()
        {
            var orchestratorFunctionNames = new[] { nameof(TestOrchestrations.ApprovalWithCancellationToken) };
            var extendedSessions = false;
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.WaitForExternalEventWithCancellationToken),
                extendedSessions))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);
                await client.WaitForStartupAsync(this.output);

                await client.RaiseEventAsync("approval", this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.InRange(status.LastUpdatedTime - status.CreatedTime, TimeSpan.Zero, timeout);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that orchestrations run concurrently of each other (up to 100 by default).
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task OrchestrationConcurrency(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationConcurrency),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                Func<Task> orchestrationStarter = async () =>
                {
                    var timeout = TimeSpan.FromSeconds(10);
                    var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Approval), timeout, this.output);
                    await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(60));

                    // Don't send any notification - let the internal timeout expire
                };

                int iterations = 100;
                var tasks = new Task[iterations];
                for (int i = 0; i < iterations; i++)
                {
                    tasks[i] = orchestrationStarter();
                }

                // The 100 orchestrations above (which each delay for 10 seconds) should all complete in less than 1000 seconds (~16 minutes).
                Task parallelOrchestrations = Task.WhenAll(tasks);
                Task timeoutTask = Task.Delay(TimeSpan.FromMinutes(3));

                Task winner = await Task.WhenAny(parallelOrchestrations, timeoutTask);
                Assert.Equal(parallelOrchestrations, winner);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the orchestrator's exception handling behavior.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HandledActivityException(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandledActivityException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Empty string input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.TryCatchLoop), 5, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(5, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the orchestrator's exception handling behavior.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task HandledSubOrchestratorException()
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandledActivityException),
                enableExtendedSessions: true))
            {
                await host.StartAsync();

                string message = $"Failure ID: {Guid.NewGuid()}";

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.SubOrchestrationThrow),
                    message,
                    this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.Contains(message, (string)status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from orchestrator code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledOrchestrationException(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ThrowOrchestrator),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledOrchestrationException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = status.Output.ToString();
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith($"Orchestrator function '{orchestratorFunctionNames[0]}' failed: Value cannot be null.", output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledOrchestrationException",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates calling an orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_Activity(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorGreeting),
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_Activity),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(orchestratorFunctionNames[0], status?.Name);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_Activity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ComplexType(string storageProvider)
        {
            await this.SubOrchestration_ComplexType_Main_Logic(nameof(this.SubOrchestration_ComplexType), storageProvider);
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ComplexType_History(string storageProvider)
        {
            await this.SubOrchestration_ComplexType_Main_Logic(nameof(this.SubOrchestration_ComplexType_History), storageProvider, showHistory: true);
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information with input and result data is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ComplexType_HistoryInputOutput(string storageProvider)
        {
            await this.SubOrchestration_ComplexType_Main_Logic(nameof(this.SubOrchestration_ComplexType_HistoryInputOutput), storageProvider, showHistory: true, showHistoryOutput: true);
        }

        /// <summary>
        /// End-to-end test which validates a sub-orchestrator function have assigned corrent value for <see cref="DurableOrchestrationContext.ParentInstanceId"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SubOrchestration_ParentInstanceId_Assigned(bool extendedSessions, string storageProvider)
        {
            const string TaskHub = nameof(this.SubOrchestration_ParentInstanceId_Assigned);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, TaskHub, extendedSessions, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.ProvideParentInstanceId),
                };

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);

                Assert.NotNull(status.Output);
                Assert.Equal(status.Output, client.InstanceId);

                await host.StopAsync();
            }
        }

        private async Task SubOrchestration_ComplexType_Main_Logic(string taskHub, string storageProvider, bool showHistory = false, bool showHistoryOutput = false)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, enableExtendedSessions: false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var complexTypeDataInput = new ComplexType
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

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.CallActivity),
                    Input = new StartOrchestrationArgs
                    {
                        FunctionName = nameof(TestActivities.Echo),
                        Input = complexTypeDataInput,
                    },
                };

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(
                    this.output,
                    showHistory,
                    showHistoryOutput);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);

                Assert.NotNull(status.Output);
                ComplexType complextTypeDataOutput = status.Output.ToObject<ComplexType>();
                CompareTwoComplexTypeObjects(complexTypeDataInput, complextTypeDataOutput);

                if (!showHistory)
                {
                    Assert.Null(status.History);
                }
                else
                {
                    Assert.Equal(3, status.History.Count);
                    Assert.Equal("ExecutionStarted", status.History[0]["EventType"].ToString());
                    Assert.Equal("CallOrchestrator", status.History[0]["FunctionName"].ToString());
                    Assert.Equal("SubOrchestrationInstanceCompleted", status.History[1]["EventType"].ToString());
                    Assert.Equal("CallActivity", status.History[1]["FunctionName"].ToString());
                    if (DateTime.TryParse(status.History[1]["Timestamp"].ToString(), out DateTime timestamp) &&
                        DateTime.TryParse(status.History[1]["FunctionName"].ToString(), out DateTime scheduledTime))
                    {
                        Assert.True(timestamp > scheduledTime);
                    }

                    Assert.Equal("ExecutionCompleted", status.History[2]["EventType"].ToString());
                    Assert.Equal("Completed", status.History[2]["OrchestrationStatus"].ToString());

                    if (showHistoryOutput)
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.Null(status.History[1]["Input"]);
                        Assert.NotNull(status.History[1]["Result"]);
                        var resultSubOrchestrationInstanceCompleted = JsonConvert.DeserializeObject<ComplexType>(status.History[1]["Result"].ToString());
                        CompareTwoComplexTypeObjects(complexTypeDataInput, resultSubOrchestrationInstanceCompleted);
                        Assert.NotNull(status.History[2]["Result"]);
                        var resultExecutionCompleted = JsonConvert.DeserializeObject<ComplexType>(status.History[2]["Result"].ToString());
                        CompareTwoComplexTypeObjects(complexTypeDataInput, resultExecutionCompleted);
                    }
                    else
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.Null(status.History[1]["Result"]);
                        Assert.Null(status.History[2]["Result"]);
                    }

                    Assert.NotNull(status.History);
                }

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a sub-orchestrator function have assigned corrent value for <see cref="DurableOrchestrationContext.ParentInstanceId"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SubOrchestration_Requires_Different_Id_Than_Parent(bool extendedSessions)
        {
            const string TaskHub = nameof(this.SubOrchestration_Requires_Different_Id_Than_Parent);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, TaskHub, extendedSessions))
            {
                await host.StartAsync();

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);
                string instanceId = Guid.NewGuid().ToString();

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.ProvideParentInstanceId),
                    InstanceId = instanceId,
                };

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output, instanceId: instanceId);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        private static void CompareTwoComplexTypeObjects(ComplexType firstObject, ComplexType secondObject)
        {
            Assert.NotNull(secondObject);
            Assert.Equal(firstObject.A, secondObject.A);
            Assert.Equal(firstObject.B[0], secondObject.B[0]);
            Assert.Equal(firstObject.B[1], secondObject.B[1]);
            Assert.NotNull(secondObject.D);
            Assert.Equal(firstObject.D.E, secondObject.D.E);
            Assert.Equal(firstObject.D.F, secondObject.D.F);
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from orchestrator functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledOrchestrationExceptionWithRetry(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorThrowWithRetry),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledOrchestrationExceptionWithRetry),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(50));

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                // Strip '\r' characters to make Windows and Unix output identical.
                string output = status.Output.ToString().Replace("\r", string.Empty);
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith($"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The orchestrator function 'ThrowOrchestrator' failed: \"Value cannot be null.", output);
                Assert.Contains("message", output);

                string subOrchestrationInstanceId = (string)status.CustomStatus;
                Assert.NotNull(subOrchestrationInstanceId);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledOrchestrationExceptionWithRetry",
                        subOrchestrationInstanceId,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an orchestrator function with null RetryOptions fails.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task OrchestrationWithRetry_NullRetryOptions(string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorWithRetry_NullRetryOptions),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationWithRetry_NullRetryOptions),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(50));

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = status.Output.ToString().Replace(Environment.NewLine, " ");
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: Value cannot be null.",
                    output);
                Assert.Contains("retryOptions", output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from activity code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledActivityException(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ThrowOrchestrator),
            };

            string activityFunctionName = nameof(TestActivities.ThrowActivity);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledActivityException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = (string)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The activity function '{activityFunctionName}' failed: \"{message}\"",
                    output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledActivityException",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from activity code
        /// within a sub-orchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledSubOrchestratorActivityException(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledSubOrchestratorActivityException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string exceptionMessage = "Kah-BOOOOM!!!";
                var args = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.ThrowOrchestrator),
                    Input = exceptionMessage,
                };

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.CallOrchestrator),
                    args,
                    this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = (string)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    string.Format(
                        "Orchestrator function '{0}' failed: The orchestrator function '{1}' failed: \"The activity function '{2}' failed: \"{3}\"",
                        nameof(TestOrchestrations.CallOrchestrator),
                        nameof(TestOrchestrations.ThrowOrchestrator),
                        nameof(TestActivities.ThrowActivity),
                        exceptionMessage),
                    output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task UnhandledActivityExceptionWithRetry(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ActivityThrowWithRetry),
            };

            string activityFunctionName = nameof(TestActivities.ThrowActivity);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledActivityExceptionWithRetry),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(40));

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = (string)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The activity function '{activityFunctionName}' failed: \"{message}\"",
                    output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledActivityExceptionWithRetry",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an activity function with null RetryOptions fails.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActivityWithRetry_NullRetryOptions(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActivityWithRetry_NullRetryOptions),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                string orchestratorFunctionName = nameof(TestOrchestrations.ActivityWithRetry_NullRetryOptions);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionName, message, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(40));

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = (string)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.Contains(orchestratorFunctionName, output);
                Assert.Contains("Value cannot be null.", output);
                Assert.Contains("retryOptions", output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that waiting for an external event and calling
        /// an activity multiple times in a row does not lead to dropped events.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task WaitForEventAndCallActivity_DroppedEventsTest(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.WaitForEventAndCallActivity_DroppedEventsTest),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string orchestratorFunctionName = nameof(TestOrchestrations.WaitForEventAndCallActivity);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionName, null, this.output);

                for (int i = 0; i < 5; i++)
                {
                    await client.RaiseEventAsync("add", i, this.output);
                }

                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(60));

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                int output = (int)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.Equal(26, output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task StartOrchestration_OnUnregisteredOrchestrator(bool extendedSessions, string storageProvider)
        {
            const string activityFunctionName = "UnregisteredOrchestrator";
            string errorMessage = $"The function '{activityFunctionName}' doesn't exist, is disabled, or is not an orchestrator function";

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.StartOrchestration_OnUnregisteredOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                Exception ex = await Assert.ThrowsAsync<FunctionInvocationException>(async () => await host.StartOrchestratorAsync("UnregisteredOrchestrator", "Unregistered", this.output));

                Assert.NotNull(ex.InnerException);
                Assert.Contains(errorMessage, ex.InnerException?.ToString());

                await host.StopAsync();
            }
        }

#if !FUNCTIONS_V1
        /// <summary>
        /// End-to-end test which creates an external client that calls a non-existent orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ExternalClient_CallsNonexistentOrchestrator(string storageProvider)
        {
            string taskHubName = TestHelpers.GetTaskHubNameFromTestName(
                nameof(this.ExternalClient_CallsNonexistentOrchestrator),
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

            var storageAccountProvider = new CustomAccountStorageProvider(appSettings);

            using (IHost clientHost = TestHelpers.GetJobHostExternalEnvironment(storageAccountProvider))
            {
                using (var orchestrationHost = TestHelpers.GetJobHost(
                   this.loggerProvider,
                   nameof(this.ExternalClient_CallsNonexistentOrchestrator),
                   enableExtendedSessions: false,
                   storageProviderType: storageProvider,
                   exactTaskHubName: taskHubName))
                {
                    await clientHost.StartAsync();
                    await orchestrationHost.StartAsync();

                    IDurableClientFactory durableClientFactory = clientHost.Services.GetService(typeof(IDurableClientFactory)) as DurableClientFactory;
                    IDurableClient durableClient = durableClientFactory.CreateClient(durableClientOptions);

                    string instanceId = await durableClient.StartNewAsync("NonexistentOrchestrator");
                    await Task.Delay(10000);
                    DurableOrchestrationStatus status = await durableClient.GetStatusAsync(instanceId);
                    Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                    await orchestrationHost.StopAsync();
                    await clientHost.StopAsync();
                }
            }
        }

        /// <summary>
        /// End-to-end test which tests renaming/disabling/deleting activity functions. An orchestrator function schedules activity functions
        /// in the first host. The second host is created without any activity functions and an external client gets the status of the orchestrator
        /// instance. The orchestrator instance should fail in this case.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task NonexistentActivity_OrchestratorFunctionFails(bool extendedSessions, string storageProvider)
        {
            var modifiedTypeArray = new Type[]
            {
                typeof(TestOrchestrations),
                typeof(ClientFunctions),
            };

            string instanceId = "";
            string taskHub = "";
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.NonexistentActivity_OrchestratorFunctionFails),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivityWithDelay), null, this.output);
                instanceId = client.InstanceId;
                taskHub = client.TaskHubName;

                await client.WaitForStartupAsync(this.output, Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(20));
                await Task.Delay(TimeSpan.FromSeconds(1));

                await host.StopAsync();
            }

            Dictionary<string, string> taskHubAndStorageAppSetting = new Dictionary<string, string>
            {
                { "CustomStorageAccountName", TestHelpers.GetStorageConnectionString() },
                { "TestTaskHub", taskHub },
            };

            var storageAccountProvider = new CustomAccountStorageProvider(taskHubAndStorageAppSetting);

            // create a new host without activity functions and see if the function fails
            using (ITestHost newHost = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.NonexistentActivity_OrchestratorFunctionFails),
                extendedSessions,
                storageProviderType: storageProvider,
                exactTaskHubName: taskHub,
                types: modifiedTypeArray))
            {
                await newHost.StartAsync();
                using (IHost clientHost = TestHelpers.GetJobHostExternalEnvironment(storageAccountProvider))
                {
                    DurableClientOptions durableClientOptions = new DurableClientOptions
                    {
                        ConnectionName = "CustomStorageAccountName",
                        TaskHub = taskHub,
                    };

                    // create a new client (external)
                    await clientHost.StartAsync();
                    IDurableClientFactory durableClientFactory = clientHost.Services.GetService(typeof(IDurableClientFactory)) as DurableClientFactory;
                    IDurableClient durableClient = durableClientFactory.CreateClient(durableClientOptions);
                    await Task.Delay(15000);
                    DurableOrchestrationStatus newStatus = await durableClient.GetStatusAsync(instanceId);

                    Assert.Equal(OrchestrationRuntimeStatus.Failed, newStatus?.RuntimeStatus);
                    Assert.Contains("Non-Deterministic workflow detected", newStatus.Output.ToString());
                    await clientHost.StopAsync();
                }

                await newHost.StopAsync();
            }
        }
#endif

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_OnUnregisteredActivity(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallActivity),
            };

            const string activityFunctionName = "UnregisteredActivity";
            string errorMessage = $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The function '{activityFunctionName}' doesn't exist, is disabled, or is not an activity function";

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnUnregisteredActivity),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = activityFunctionName,
                    Input = new { Foo = "Bar" },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.StartsWith(errorMessage, (string)status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnUnregisteredActivity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which runs an orchestrator function that calls another orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_OnValidOrchestrator(bool extendedSessions, string storageProvider)
        {
            const string greetingName = "ValidOrchestrator";
            const string validOrchestratorName = "SayHelloWithActivity";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                validOrchestratorName,
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            var input = new { Foo = greetingName };
            var inputJson = JsonConvert.SerializeObject(input);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnValidOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string parentInstanceId = "PARENT_" + Guid.NewGuid().ToString("N");
                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = orchestratorFunctionNames[1],
                    InstanceId = parentInstanceId + ":0",
                    Input = inputJson,
                };

                // Function type call chain: 'CallActivity' (orchestrator) -> 'SayHelloWithActivity' (orchestrator) -> 'Hello' (activity)
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output, parentInstanceId);
                var status = await client.WaitForCompletionAsync(this.output);
                var statusInput = JsonConvert.DeserializeObject<Dictionary<string, object>>(status?.Input.ToString());

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(validOrchestratorName, statusInput["FunctionName"].ToString());
                Assert.Contains(greetingName, statusInput["Input"].ToString());
                Assert.Equal($"Hello, {inputJson}!", status.Output.ToString());

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnValidOrchestrator",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HandleUncallableOrchestrator(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandleUncallableOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(UnconstructibleClass.UncallableOrchestrator), null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
#if FUNCTIONS_V1
                Assert.Equal("Orchestrator function 'UncallableOrchestrator' failed: Exception has been thrown by the target of an invocation.", status.Output.ToString());
#else
                Assert.Equal("Orchestrator function 'UncallableOrchestrator' failed: Exception of type 'System.Exception' was thrown.", status.Output.ToString());
#endif

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HandleUncallableFunctions(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandleUncallableFunctions),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(UnconstructibleClass.UncallableEntity), Guid.NewGuid().ToString());
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.HandleUncallableFunctions), entityId, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Orchestration_OnUnregisteredOrchestrator(bool extendedSessions, string storageProvider)
        {
            const string unregisteredOrchestrator = "UnregisteredOrchestrator";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                unregisteredOrchestrator,
            };

            string errorMessage = $"The function '{unregisteredOrchestrator}' doesn't exist, is disabled, or is not an orchestrator function";

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnUnregisteredOrchestrator),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = unregisteredOrchestrator,
                    Input = new { Foo = "Bar" },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains(errorMessage));

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnUnregisteredOrchestrator",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task AzureStorage_BigReturnValue_Orchestrator(bool extendedSessions, bool autoFetch)
        {
            string taskHub = nameof(this.AzureStorage_BigReturnValue_Orchestrator);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions, autoFetchLargeMessages: autoFetch))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.BigReturnValue);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;

                var client = await host.StartOrchestratorAsync(orchestrator, stringLength, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                if (!autoFetch)
                {
                    await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);
                }
                else
                {
                    Assert.Equal(stringLength, ((string)status.Output).Length);
                }

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task AzureStorage_BigReturnValue_Activity(bool extendedSessions, bool autoFetch)
        {
            string taskHub = nameof(this.AzureStorage_BigReturnValue_Activity);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions, autoFetchLargeMessages: autoFetch))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.CallActivity);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;
                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestActivities.BigReturnValue),
                    Input = stringLength,
                };

                var client = await host.StartOrchestratorAsync(orchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                if (!autoFetch)
                {
                    await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);
                }
                else
                {
                    Assert.Equal(stringLength, ((string)status.Output).Length);
                }

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task RaiseEventToSubOrchestration(bool extendedSessions, string storageProvider)
        {
            string taskHub = nameof(this.RaiseEventToSubOrchestration);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.Approval),
                    InstanceId = "SubOrchestration-" + Guid.NewGuid().ToString("N"),
                    Input = TimeSpan.FromMinutes(5),
                };

                var client = await host.StartOrchestratorAsync(orchestrator, input, this.output);
                var status = await client.WaitForStartupAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Wait long enough for the sub-orchestration to be started and waiting for input.
                await Task.Delay(TimeSpan.FromSeconds(5));
                await client.InnerClient.RaiseEventAsync(input.InstanceId, "approval", true);

                status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal("Approved", status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SetStatusOrchestration(bool extendedSessions, string storageProvider)
        {
            const string testName = nameof(this.SetStatusOrchestration);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SetStatus), null, this.output);
                await client.WaitForStartupAsync(this.output);

                DurableOrchestrationStatus orchestrationStatus = await client.GetStatusAsync();
                Assert.Equal(JTokenType.Null, orchestrationStatus.CustomStatus?.Type);

                // The orchestrator will wait for an external event, and use the payload to update its custom status.
                const string statusValue = "updated status";
                await client.RaiseEventAsync("UpdateStatus", statusValue, this.output);
                await client.WaitForCustomStatusAsync(TimeSpan.FromSeconds(10), this.output, statusValue);

                // Test clearing an existing custom status
                await client.RaiseEventAsync("UpdateStatus", null, this.output);
                await client.WaitForCustomStatusAsync(TimeSpan.FromSeconds(30), this.output, JValue.CreateNull());

                // Test setting the custom status to a complex object.
                var newCustomStatus = new { Foo = "Bar", Count = 2, };
                await client.RaiseEventAsync("UpdateStatus", newCustomStatus, this.output);
                orchestrationStatus = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(newCustomStatus.Foo, (string)orchestrationStatus.CustomStatus["Foo"]);
                Assert.Equal(newCustomStatus.Count, (int)orchestrationStatus.CustomStatus["Count"]);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, orchestrationStatus?.RuntimeStatus);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_InstanceNotFound(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.GetStatus_InstanceNotFound), false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Start a dummy orchestration just to help us get a client object
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloInline), null, this.output);
                await client.WaitForCompletionAsync(this.output);

                string bogusInstanceId = "BOGUS_" + Guid.NewGuid().ToString("N");
                this.output.WriteLine($"Fetching status for fake instance: {bogusInstanceId}");
                DurableOrchestrationStatus status = await client.InnerClient.GetStatusAsync(instanceId: bogusInstanceId);
                Assert.Null(status);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_ShowInputFalse(string storageProvider)
        {
            const string testName = nameof(this.GetStatus_ShowInputFalse);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, testName, false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 1, this.output);

                DurableOrchestrationStatus status = await client.GetStatusAsync(showHistory: false, showHistoryOutput: false, showInput: false);
                Assert.True(string.IsNullOrEmpty(status.Input.ToString()));
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_ShowInputDefault(string storageProvider)
        {
            const string testName = nameof(this.GetStatus_ShowInputDefault);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, testName, false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 1, this.output);

                DurableOrchestrationStatus status = await client.GetStatusAsync(showHistory: false, showHistoryOutput: false);
                Assert.Equal("1", status.Input.ToString());
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Deserialize_DurableOrchestrationStatus(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.Deserialize_DurableOrchestrationStatus), false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string instanceId = Guid.NewGuid().ToString();
                DurableOrchestrationStatus input = new DurableOrchestrationStatus();
                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.GetDurableOrchestrationStatus),
                    input,
                    this.output);
                DurableOrchestrationStatus desereliazedStatus = await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(desereliazedStatus);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, desereliazedStatus.RuntimeStatus);
                Assert.True(desereliazedStatus.LastUpdatedTime > desereliazedStatus.CreatedTime);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that Activity function can get an instance of HttpManagementPayload and return via the orchestrator.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Activity_Gets_HttpManagementPayload(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ReturnHttpManagementPayload),
                nameof(TestActivities.GetAndReturnHttpManagementPayload),
            };

            string testName = nameof(this.Activity_Gets_HttpManagementPayload);
            string taskHub = TestHelpers.GetTaskHubNameFromTestName(testName, extendedSessions);
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                exactTaskHubName: taskHub,
                notificationUrl: new Uri(TestConstants.NotificationUrl),
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                HttpManagementPayload httpManagementPayload = status.Output.ToObject<HttpManagementPayload>();
                ValidateHttpManagementPayload(httpManagementPayload, extendedSessions, taskHub);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates HttpManagementPayload retrieved from Orchestration client when executing a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task OrchestrationClient_Gets_HttpManagementPayload(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            string testName = nameof(this.OrchestrationClient_Gets_HttpManagementPayload);
            string taskHub = TestHelpers.GetTaskHubNameFromTestName(testName, extendedSessions);
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                notificationUrl: new Uri(TestConstants.NotificationUrl),
                storageProviderType: storageProvider,
                exactTaskHubName: taskHub))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                HttpManagementPayload httpManagementPayload = client.InnerClient.CreateHttpManagementPayload(status.InstanceId);
                ValidateHttpManagementPayload(httpManagementPayload, extendedSessions, taskHub);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

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

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActorOrchestration_WithTaskHubName(bool extendedSessions, string storageProvider)
        {
            var taskHubName1 = "ActorOrchestration1";
            var taskHubName2 = "ActorOrchestration2";
            using (ITestHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions, storageProviderType: storageProvider))
            using (ITestHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions, storageProviderType: storageProvider))
            {
                await host1.StartAsync();
                await host2.StartAsync();

                int initialValue = 0;
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "World", this.output);
                var instanceId = client1.InstanceId;
                taskHubName1 = client1.TaskHubName;

                // Perform some operations
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);

                TimeSpan waitTimeout = TimeSpan.FromSeconds(10);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 1);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 3);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "decr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr", this.output);
                await client1.WaitForCustomStatusAsync(waitTimeout, this.output, 3);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client1.GetStatusAsync();
                Assert.True(
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                // The end message will cause the actor to complete itself.
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "end", this.output);

                status = await client1.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(3, (int)status?.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.Equal(3, (int)status.Input);

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates legacy compatibility of orchestration and activity bindings.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task LegacyBaseClasses()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.LegacyBaseClasses),
                false))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.LegacyOrchestration), null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", (string)status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple entity scenario involving a signal and two calls.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SignalAndCallStringStore(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SignalAndCallStringStore),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SignalAndCallStringStore),
                extendedSessions))
            {
                await host.StartAsync();

                var guid = Guid.NewGuid(); // used as the key for the entity

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], guid, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", (string)status?.Output);

                // try to read the state of the entity directly from the client
                var response = await client.InnerClient.ReadEntityStateAsync<string>(new EntityId("StringStore2", guid.ToString()));
                Assert.True(response.EntityExists);
                Assert.Equal("333", response.EntityState);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple entity scenario involving creation and deletion.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_StringStoreWithCreateDelete(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.StringStoreWithCreateDelete),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_StringStoreWithCreateDelete),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "start", this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("start", status?.Input);
                Assert.Equal("ok", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates batching of entity signals.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_BatchedSignals(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_BatchedSignals),
                extendedSessions))
            {
                await host.StartAsync();

                int numIterations = 100;
                var entityId = new EntityId(nameof(TestEntities.BatchEntity), Guid.NewGuid().ToString());
                var client = await host.GetEntityClientAsync(entityId, this.output);

                // send a number of signals immediately after each other
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < numIterations; i++)
                {
                    tasks.Add(client.SignalEntity(this.output, i.ToString()));
                }

                await Task.WhenAll(tasks);

                var result = await client.WaitForEntityState<List<(int, int)>>(
                    this.output,
                    timeout: Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(20),
                    list => list.Count == numIterations ? null : $"waiting for {numIterations - list.Count} signals");

                // validate the batching positions and sizes
                int? cursize = null;
                int curpos = 0;
                int numBatches = 0;
                foreach (var (position, size) in result)
                {
                    if (cursize == null)
                    {
                        cursize = size;
                        curpos = 0;
                        numBatches++;
                    }

                    Assert.Equal(curpos, position);

                    if (++curpos == cursize)
                    {
                        cursize = null;
                    }
                }

                // there should always be some batching going on
                Assert.True(numBatches < numIterations);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates batching of entity signals.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_NonexistentEntity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.NonexistentEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_NonexistentEntity),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates exception handling in entity operations.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(false, false, false)]
        public async Task DurableEntity_CallFaultyEntity(bool extendedSessions, bool useClassBasedEntity, bool rollbackOnExceptions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallFaultyEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_CallFaultyEntity),
                extendedSessions,
                rollbackEntityOperationsOnExceptions: rollbackOnExceptions))
            {
                await host.StartAsync();
                var entityName = useClassBasedEntity ? "ClassBasedFaultyEntity" : "FunctionBasedFaultyEntity";
                var entityId = new EntityId(entityName, Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], (entityId, rollbackOnExceptions), this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates rollback of sent signals on exceptions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task DurableEntity_RollbackSignalsOnExceptions(bool extendedSessions, bool useClassBasedEntity)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_RollbackSignalsOnExceptions),
                extendedSessions,
                rollbackEntityOperationsOnExceptions: true))
            {
                await host.StartAsync();
                var entityName = useClassBasedEntity ? "ClassBasedFaultyEntity" : "FunctionBasedFaultyEntity";
                var entityKey = Guid.NewGuid().ToString();
                var entityId = new EntityId(entityName, entityKey);

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.RollbackSignalsOnExceptions), entityId, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", status?.Output);

                var receiverEntityId = new EntityId(nameof(TestEntities.SchedulerEntity), entityKey);
                TestEntityClient receiverClient = await host.GetEntityClientAsync(receiverEntityId, this.output);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var state = await receiverClient.WaitForEntityState<List<string>>(this.output, timeout, curstate => curstate.Count >= 7 ? null : "expect 11 messages");
                Assert.Equal(new string[] { "1:56", "2:100", "3:100", "4:10", "5:10", "6:10", "7:11" }, state);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple entity scenario which sends a signal
        /// to a relay which forwards it to counter, and polls until the signal is delivered.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SignalThenPoll(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.PollCounterEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SignalThenPoll),
                extendedSessions))
            {
                await host.StartAsync();

                var relayEntityId = new EntityId("Relay", "");
                var counterEntityId = new EntityId("Counter", Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counterEntityId, this.output);

                await client.InnerClient.SignalEntityAsync(relayEntityId, "", (counterEntityId, "increment"));

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates launching orchestrations from entities.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityFireAndForget(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityFireAndForget),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.LaunchOrchestrationFromEntity),
                    null,
                    this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                var instanceId = (string)status?.Output;
                Assert.NotNull(instanceId);
                var launchedStatus = await client.InnerClient.GetStatusAsync(instanceId, false, false, false);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, launchedStatus.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple entity scenario where an entity's state is
        /// larger than what fits into Azure table rows.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_LargeEntity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LargeEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_LargeEntity),
                extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId("StringStore2", Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityId, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", status?.Output);

                var response = await client.InnerClient.ReadEntityStateAsync<string>(entityId);
                Assert.True(response.EntityExists);
                Assert.Equal(100000, response.EntityState.Length);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario involving a blob-backed entity that stores text and,
        /// when deactivated, saves its state to storage. The test concurrently runs an orchestration that
        /// creates a load of "append" operations, and sends periodic "deactivate" operations to the entity.
        /// At the end, it validates that all of the appends are reflected in the final state.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityToAndFromBlob(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityToAndFromBlob),
                extendedSessions))
            {
                await host.StartAsync();

                await EnsureBlobContainerExists("test");

                var entityId = new EntityId("BlobBackedTextStore", Guid.NewGuid().ToString());

                // first, start the orchestration
                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.EntityToAndFromBlob),
                    entityId,
                    this.output);

                DurableOrchestrationStatus status;
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Debugger.IsAttached ? 3000 : 240);

                while (true)
                {
                    await Task.Delay(1000);

                    // while the orchestration is running, just for fun,
                    // send some deactivation signals which unload the entity from memory.
                    // this should not change the final outcome as the entities are storage-backed.
                    await client.InnerClient.SignalEntityAsync(entityId, "deactivate");

                    status = await client.GetStatusAsync();

                    if (DateTime.UtcNow >= deadline ||
                        ((status?.RuntimeStatus != OrchestrationRuntimeStatus.Pending)
                         && (status?.RuntimeStatus != OrchestrationRuntimeStatus.Running)))
                    {
                        break;
                    }
                }

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", (string)status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Send a bunch of signals from a client to a single entity, then test that they are all being delivered.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, false, 1)]
        [InlineData(true, false, 2)]
        [InlineData(true, false, 20)]
        [InlineData(true, false, 200)]
        [InlineData(false, false, 1)]
        [InlineData(false, false, 2)]
        [InlineData(false, false, 20)]
        [InlineData(false, false, 200)]
        [InlineData(true, true, 1)]
        [InlineData(true, true, 2)]
        [InlineData(true, true, 20)]
        [InlineData(true, true, 200)]
        [InlineData(false, true, 1)]
        [InlineData(false, true, 2)]
        [InlineData(false, true, 20)]
        [InlineData(false, true, 200)]
        public async Task DurableEntity_ManyScheduledSignals(bool extendedSessions, bool delay, int numSignals)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_ManyScheduledSignals),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntities.SchedulerEntity), Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);

                var now = DateTime.UtcNow;

                for (int i = 0; i < numSignals; i++)
                {
                    if (delay)
                    {
                        await client.SignalEntity(this.output, now + TimeSpan.FromSeconds(i * (10.0 / numSignals)), i.ToString(), null);
                    }
                    else
                    {
                        await client.SignalEntity(this.output, i.ToString(), null);
                    }
                }

                string DescribeWhatsMissing(List<string> curstate)
                {
                    var expected = new HashSet<string>();
                    for (int i = 0; i < numSignals; i++)
                    {
                        expected.Add(i.ToString());
                    }

                    foreach (var s in curstate)
                    {
                        expected.Remove(s);
                    }

                    if (expected.Count == 0)
                    {
                        return null;
                    }
                    else
                    {
                        return string.Join(",", expected);
                    }
                }

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var state = await client.WaitForEntityState<List<string>>(this.output, timeout, DescribeWhatsMissing);

                this.output.WriteLine(string.Join(", ", state));

                // The scheduled signals are not guaranteed to be delivered in order, so we sort before comparing
                var intlist = state.Select(s => int.Parse(s)).ToList();
                intlist.Sort();

                for (int i = 0; i < numSignals; i++)
                {
                    Assert.Equal(i, intlist[i]);
                }

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates calling an entity from successive incarnations of an orchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_ContinueAsNewBetweenCalls(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_ContinueAsNewBetweenCalls),
                extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntities.SchedulerEntity), Guid.NewGuid().ToString("N"));

                var orchestratorClient = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.ThreeSuccessiveCalls),
                    (entityId, 0),
                    this.output);

                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10);
                var state = await client.WaitForEntityState<List<string>>(this.output, timeout, curstate => curstate.Count == 3 ? null : "expect 3 calls");

                var status = await orchestratorClient.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                Assert.Equal("ok", (string)status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Send a scheduled signal, then an immediate signal, and test delivery order.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task DurableEntity_ScheduledSignal(bool extendedSessions, bool useUtc)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_ScheduledSignal),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntities.SchedulerEntity), Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);

                // Wait 8 seconds to account for time to grab ownership lease.
                await Task.Delay(8000);

                var now = useUtc ? DateTime.UtcNow : DateTime.Now;

                await client.SignalEntity(this.output, now + TimeSpan.FromSeconds(4), "delayed", null);
                await client.SignalEntity(this.output, "immediate", null);

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10);
                var state = await client.WaitForEntityState<List<string>>(this.output, timeout, curstate => curstate.Count == 2 ? null : "expect both messages");

                Assert.Equal("immediate, delayed", string.Join(", ", state));

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test an entity that signals itself with a delay.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SelfSchedulingEntity(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SelfSchedulingEntity),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var entityId = new EntityId(nameof(TestEntityClasses.SelfSchedulingEntity), Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);
                await client.SignalEntity(this.output, "Start", null);

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(15);
                var state = await client.WaitForEntityState<TestEntityClasses.SelfSchedulingEntity>(this.output, timeout, curstate => curstate.Value.Length == 4 ? null : "expect 4 letters");

                Assert.Equal("ABCD", state.Value);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario where three "LockedIncrement" orchestrations
        /// concurrently increment a counter saved in blob storage, using a read-modify-write pattern, while holding
        /// a lock on the same entity. This tests that the lock prevents the interleaving of these orchestrations.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_LockedIncrements(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LockedBlobIncrement),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_LockedIncrements),
                extendedSessions))
            {
                await host.StartAsync();

                await EnsureBlobContainerExists("test");

                var entityPlayingALock = new EntityId("Counter", Guid.NewGuid().ToString()); // does not matter what entity we use

                // start three concurrent increment operations
                // the lock should prevent incorrect interleavings

                var client1 = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityPlayingALock, this.output);
                var client2 = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityPlayingALock, this.output);
                var client3 = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityPlayingALock, this.output);

                var status1 = await client1.WaitForCompletionAsync(this.output);
                var status2 = await client2.WaitForCompletionAsync(this.output);
                var status3 = await client3.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status1?.RuntimeStatus);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status2?.RuntimeStatus);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status3?.RuntimeStatus);

                var result = new int[] { (int)status1?.Output, (int)status2?.Output, (int)status3?.Output };
                Array.Sort(result);

                for (int i = 0; i < result.Length; i++)
                {
                    Assert.True(result[i] == i + 1);
                }

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario where a "LockedTransfer" orchestration locks
        /// two "Counter" entities, and then in parallel increments/decrements them, respectively, using
        /// a read-modify-write pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_SingleLockedTransfer(bool extendedSessions)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_SingleLockedTransfer),
                extendedSessions))
            {
                await host.StartAsync();

                var counter1 = new EntityId("Counter", Guid.NewGuid().ToString());
                var counter2 = new EntityId("Counter", Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.LockedTransfer),
                    (counter1, counter2),
                    this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                // validate the state of the counters
                var response1 = await client.InnerClient.ReadEntityStateAsync<int>(counter1);
                var response2 = await client.InnerClient.ReadEntityStateAsync<int>(counter2);
                Assert.True(response1.EntityExists);
                Assert.True(response2.EntityExists);
                Assert.Equal(-1, response1.EntityState);
                Assert.Equal(1, response2.EntityState);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates an entity scenario where a a number of "LockedTransfer" orchestrations
        /// concurrently operate on a number of entities, in a classical dining-philosophers configuration.
        /// This showcases the deadlock prevention mechanism achieved by the sequential, ordered lock acquisition.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, 5)]
        [InlineData(false, 5)]
        public async Task DurableEntity_MultipleLockedTransfers(bool extendedSessions, int numberEntities)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LockedTransfer),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_MultipleLockedTransfers),
                extendedSessions))
            {
                await host.StartAsync();

                // create specified number of entities
                var counters = new EntityId[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    counters[i] = new EntityId("Counter", Guid.NewGuid().ToString());
                }

                // in parallel, start one transfer per counter, each decrementing a counter and incrementing
                // its successor (where the last one wraps around to the first)
                // This is a pattern that would deadlock if we didn't order the lock acquisition.
                var clients = new Task<TestDurableClient>[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    clients[i] = host.StartOrchestratorAsync(
                        orchestratorFunctionNames[0],
                        (counters[i], counters[(i + 1) % numberEntities]),
                        this.output);
                }

                await Task.WhenAll(clients);

                // in parallel, wait for all transfers to complete
                var stati = new Task<DurableOrchestrationStatus>[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    stati[i] = clients[i].Result.WaitForCompletionAsync(this.output);
                }

                await Task.WhenAll(stati);

                // check that they all completed
                for (int i = 0; i < numberEntities; i++)
                {
                    Assert.Equal(OrchestrationRuntimeStatus.Completed, stati[i].Result?.RuntimeStatus);
                }

                // in parallel, read all the entity states
                var entityStates = new Task<EntityStateResponse<int>>[numberEntities];
                for (int i = 0; i < numberEntities; i++)
                {
                    entityStates[i] = clients[i].Result.InnerClient.ReadEntityStateAsync<int>(counters[i]);
                }

                await Task.WhenAll(entityStates);

                // check that the counter states are all back to 0
                // (since each participated in 2 transfers, one incrementing and one decrementing)
                for (int i = 0; i < numberEntities; i++)
                {
                    Assert.True(entityStates[i].Result.EntityExists);
                    Assert.Equal(0, entityStates[i].Result.EntityState);
                }

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test which validates that actors can safely make async I/O calls.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableEntity_AsyncIO()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_AsyncIO),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                var entityId = new EntityId("HttpEntity", Guid.NewGuid().ToString("N"));
                TestEntityClient client = await host.GetEntityClientAsync(entityId, this.output);

                await client.SignalEntity(this.output, "get", "https://www.microsoft.com");
                await client.SignalEntity(this.output, "get", "https://bing.com");

                var state = await client.WaitForEntityState<IDictionary<string, string>>(this.output, TimeSpan.FromSeconds(10));
                Assert.NotNull(state);

                if (state.TryGetValue("error", out string error))
                {
                    throw new XunitException("Entity encountered an error: " + error);
                }

                Assert.True(state.ContainsKey("https://www.microsoft.com"));
                Assert.Equal("200", state["https://www.microsoft.com"]);

                Assert.True(state.ContainsKey("https://bing.com"));
                Assert.Equal("200", state["https://bing.com"]);

                Assert.Equal(2, state.Count);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test for EntityId case insensitivity.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityNameCaseInsensitivity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.LargeEntity),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityNameCaseInsensitivity),
                extendedSessions))
            {
                await host.StartAsync();

                var entityKey = Guid.NewGuid().ToString();
                var entityName = "StringStore2";

                var entityId = new EntityId(entityName.ToUpperInvariant(), entityKey);

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityId, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                IDurableEntityClient durableOrchestrationClient = client.InnerClient;

                var response = await durableOrchestrationClient.ReadEntityStateAsync<JToken>(new EntityId(entityName.ToLowerInvariant(), entityKey));

                Assert.True(response.EntityExists);

                await host.StopAsync();
            }
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
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// TODO: This test is flakey in Functions V1.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_BasicObjects(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.BasicObjects),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_BasicObjects),
                extendedSessions))
            {
                await host.StartAsync();

                var chatroom = new EntityId(nameof(TestEntityClasses.ChatRoom), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], chatroom, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("a,b,c", status?.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy),
                extendedSessions))
            {
                await host.StartAsync();

                var counter = new EntityId(nameof(TestEntityClasses.CounterWithProxy), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counter, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(true, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy_MultipleInterfaces(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy_MultipleInterfaces),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy_MultipleInterfaces),
                extendedSessions))
            {
                await host.StartAsync();

                var counter = new EntityId(nameof(TestEntityClasses.JobWithProxyMultiInterface), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counter, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(true, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy_UsesBindings(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy),
            };

            string storageConnectionString = TestHelpers.GetStorageConnectionString();
            CloudStorageAccount.TryParse(storageConnectionString, out CloudStorageAccount storageAccount);

            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(TestEntityClasses.BlobContainerPath);
            await cloudBlobContainer.CreateIfNotExistsAsync();

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy_UsesBindings),
                extendedSessions))
            {
                await host.StartAsync();

                var counter = new EntityId(nameof(TestEntityClasses.StorageBackedCounter), Guid.NewGuid().ToString());

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], counter, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(true, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates basic use of the object dispatch feature.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DurableEntity_EntityProxy_NameResolve(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.EntityProxy_NameResolve),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_EntityProxy_NameResolve),
                extendedSessions))
            {
                await host.StartAsync();

                var entityKey = Guid.NewGuid().ToString();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], entityKey, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(true, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test which validates that orchestrations can call a timer after doing a continue as new.
        /// This is meant to catch regressions of azure/durabletask/#285.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ContinueAsNew_Repro285()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ContinueAsNew_Repro285),
                enableExtendedSessions: true))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ContinueAsNew_Repro285), 0, this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test which validates that orchestrations can call a timer and then cancel it if receiving an event instead.
        /// This is meant to catch regressions of azure/durabletask/#285.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, 20)]
        [InlineData(false, 20)]
        public async Task ContinueAsNewMultipleTimersAndEvents(bool extendedSessions, int numSignals)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ContinueAsNewMultipleTimersAndEvents),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ContinueAsNewMultipleTimersAndEvents), numSignals, this.output);

                await Task.Delay(TimeSpan.FromSeconds(10));

                for (int i = numSignals; i > 0; i--)
                {
                    await client.RaiseEventAsync($"signal{i}", this.output);
                }

                var status = await client.WaitForCompletionAsync(this.output, false, false, TimeSpan.FromSeconds(80));

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("ok", status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ExternalEvents_WithTaskHubName_MultipleNamesLooping(bool extendedSessions, string storageProvider)
        {
            var taskHubName1 = "MultipleNamesLooping1";
            var taskHubName2 = "MultipleNamesLooping2";
            using (ITestHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions, storageProviderType: storageProvider))
            using (ITestHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions, storageProviderType: storageProvider))
            {
                await host1.StartAsync();
                await host2.StartAsync();
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.Counter2), null, this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "World", this.output);
                taskHubName1 = client1.TaskHubName;
                taskHubName2 = client2.TaskHubName;
                var instanceId = client1.InstanceId;

                // Perform some operations
                await client2.RaiseEventAsync(taskHubName1, instanceId, "incr", null, this.output);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "incr", null, this.output);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "done", null, this.output);

                // Make sure it actually completed
                var status = await client1.WaitForCompletionAsync(this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(2, (int)status.Output);

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Purge_Single_Instance_History(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Purge_Single_Instance_History),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string instanceId = Guid.NewGuid().ToString();
                string message = GenerateMediumRandomStringPayload().ToString();
                TestDurableClient client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.EchoWithActivity), message, this.output, instanceId);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(2));
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                DurableOrchestrationStatus orchestrationStatus = await client.GetStatusAsync(true);
                Assert.NotNull(orchestrationStatus);
                Assert.Equal(instanceId, orchestrationStatus.InstanceId);
                Assert.True(orchestrationStatus.History.Count > 0);

                int blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", instanceId);
                Assert.True(blobCount > 0);

                await client.InnerClient.PurgeInstanceHistoryAsync(instanceId);

                orchestrationStatus = await client.GetStatusAsync(true);
                Assert.Null(orchestrationStatus);

                blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", instanceId);
                Assert.Equal(0, blobCount);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Purge_All_History_By_TimePeriod(bool extendedSessions, string storageProvider)
        {
            string testName = nameof(this.Purge_All_History_By_TimePeriod);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                storageProviderType: storageProvider,
                autoFetchLargeMessages: false))
            {
                await host.StartAsync();

                DateTime startDateTime = DateTime.Now;

                string firstInstanceId = Guid.NewGuid().ToString();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 50, this.output, firstInstanceId);
                await client.WaitForCompletionAsync(this.output);

                var status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string secondInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 50, this.output, secondInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string thirdInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 50, this.output, thirdInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string fourthInstanceId = Guid.NewGuid().ToString();
                string message = GenerateMediumRandomStringPayload().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.EchoWithActivity), message, this.output, fourthInstanceId);
                await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(2));

                status = await client.InnerClient.GetStatusAsync(fourthInstanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.True(status.History.Count > 0);
                await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);

                int blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", fourthInstanceId);
                Assert.True(blobCount > 0);

                await client.InnerClient.PurgeInstanceHistoryAsync(
                    startDateTime,
                    DateTime.UtcNow,
                    new List<OrchestrationStatus>
                    {
                        OrchestrationStatus.Completed,
                        OrchestrationStatus.Terminated,
                        OrchestrationStatus.Failed,
                    });

                status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(fourthInstanceId, true);
                Assert.Null(status);

                blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", fourthInstanceId);
                Assert.Equal(0, blobCount);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Purge_Partially_History_By_TimePeriod(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Purge_Partially_History_By_TimePeriod),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                DateTime startDateTime = DateTime.Now;

                string firstInstanceId = Guid.NewGuid().ToString();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 50, this.output, firstInstanceId);
                await client.WaitForCompletionAsync(this.output);

                var status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                DateTime endDateTime = DateTime.Now;
                await Task.Delay(5000);

                string secondInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 50, this.output, secondInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string thirdInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 50, this.output, thirdInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                await client.InnerClient.PurgeInstanceHistoryAsync(
                    startDateTime,
                    endDateTime,
                    new List<OrchestrationStatus>
                    {
                        OrchestrationStatus.Completed,
                        OrchestrationStatus.Terminated,
                        OrchestrationStatus.Failed,
                    });

                status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(secondInstanceId, status.InstanceId);
                Assert.True(status.History.Count > 0);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(thirdInstanceId, status.InstanceId);
                Assert.True(status.History.Count > 0);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RestartOrchestator_IsSuccess(bool restartWithNewInstanceId)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Inline),
                false))
            {
                await host.StartAsync();

                var instanceId = Guid.NewGuid().ToString();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "RestartAsyncTest", this.output, instanceId: instanceId);
                await client.WaitForCompletionAsync(this.output);

                var newInstanceId = await client.InnerClient.RestartAsync(instanceId, restartWithNewInstanceId: restartWithNewInstanceId);
                var status = await client.WaitForCompletionAsync(this.output);

                if (restartWithNewInstanceId)
                {
                    Assert.NotEqual(instanceId, newInstanceId);
                }
                else
                {
                    Assert.Equal(instanceId, newInstanceId);
                }

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("RestartAsyncTest", status?.Input);

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RestartOrchestrator_ThrowsException()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Inline),
                false))
            {
                await host.StartAsync();

                var nonExistentId = Guid.NewGuid().ToString();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);

                ArgumentException exception =
                    await Assert.ThrowsAsync<ArgumentException>(async () =>
                    {
                        await client.InnerClient.RestartAsync(nonExistentId);
                    });

                Assert.Equal(
                    $"An orchestrastion with the instanceId {nonExistentId} was not found.",
                    exception.Message);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_WithCondition(bool extendedSessions, string storageProvider)
        {
            var taskHubName1 = "GetStatus1";
            var taskHubName2 = "GetStatus2";
            await TestHelpers.DeleteTaskHubResources(taskHubName1, extendedSessions);
            await TestHelpers.DeleteTaskHubResources(taskHubName2, extendedSessions);
            using (ITestHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions, storageProviderType: storageProvider))
            using (ITestHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions, storageProviderType: storageProvider))
            {
                await host1.StartAsync();
                await host2.StartAsync();
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "foo", this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "bar", this.output);
                var client3 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "baz", this.output);

                taskHubName1 = client1.TaskHubName;
                taskHubName2 = client2.TaskHubName;
                var instanceId = client1.InstanceId;

                var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
                var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

                var condition1 = new OrchestrationStatusQueryCondition
                {
                    RuntimeStatus = new List<OrchestrationRuntimeStatus>()
                        { OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Completed },
                    CreatedTimeFrom = yesterday,
                    CreatedTimeTo = tomorrow,
                    TaskHubNames = new List<string>() { taskHubName1 },
                };
                var condition2 = new OrchestrationStatusQueryCondition
                {
                    RuntimeStatus = new List<OrchestrationRuntimeStatus>()
                        { OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Completed },
                    CreatedTimeFrom = yesterday,
                    CreatedTimeTo = tomorrow,
                    TaskHubNames = new List<string>() { taskHubName2 },
                };

                // Make sure it actually completed
                await client1.WaitForCompletionAsync(this.output);
                await client2.WaitForCompletionAsync(this.output);
                await client3.WaitForCompletionAsync(this.output);

                // Perform some operations
                var result1 = await client1.GetStatusAsync(condition1, CancellationToken.None);
                var result2 = await client2.GetStatusAsync(condition2, CancellationToken.None);

                Assert.Single(result1.DurableOrchestrationState);
                Assert.Equal(2, result2.DurableOrchestrationState.Count());

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntitiesAsync_FetchState(bool fetchState, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery
            {
                EntityName = "StringStore",
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                FetchState = fetchState,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore", "bar"),
                new EntityId("StringStore", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntitiesAsync_FetchState), storageProvider, query, entityIds);

            Assert.Equal(3, result.Count);

            if (fetchState)
            {
                Assert.NotNull(result[0].State);
            }
            else
            {
                Assert.Null(result[0].State);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntitiesAsync_Paging(bool moreThanOne, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery
            {
                EntityName = "StringStore",
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                PageSize = moreThanOne ? 2 : 1,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore", "bar"),
                new EntityId("StringStore", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntitiesAsync_Paging), storageProvider, query, entityIds);

            Assert.Equal(3, result.Count);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntitiesAsync_NoResults(bool fetchState, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery
            {
                EntityName = "noResult",
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                FetchState = fetchState,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore2", "bar"),
                new EntityId("StringStore2", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntitiesAsync_NoResults), storageProvider, query, entityIds);

            Assert.Empty(result);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntities_Deleted(bool includeDeleted, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery()
            {
                IncludeDeleted = includeDeleted,
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore", "foo"),
                new EntityId("StringStore2", "bar"),
                new EntityId("StringStore2", "baz"),
                new EntityId("StringStore2", "foo"),
            };

            List<string> orchestrations = new List<string>()
            {
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntities_Deleted), storageProvider, query, entityIds, orchestrations);

            Assert.Equal(includeDeleted ? 4 : 2, result.Count);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_ListEntities_DeletedPaged(bool includeDeleted, string storageProvider)
        {
            var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

            var query = new EntityQuery()
            {
                IncludeDeleted = includeDeleted,
                LastOperationFrom = yesterday,
                LastOperationTo = tomorrow,
                PageSize = 2,
            };

            List<EntityId> entityIds = new List<EntityId>()
            {
                new EntityId("StringStore2", "bar"),
                new EntityId("StringStore2", "baz"),
                new EntityId("StringStore2", "foo"),
                new EntityId("StringStore2", "ffo"),
                new EntityId("StringStore2", "zzz"),
                new EntityId("StringStore2", "aaa"),
                new EntityId("StringStore2", "bbb"),
            };

            List<string> orchestrations = new List<string>()
            {
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_CallAndDeleteStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
                nameof(TestOrchestrations.EntityId_SignalAndCallStringStore),
            };

            var result = await this.DurableEntity_ListEntitiesAsync(nameof(this.DurableEntity_ListEntities_DeletedPaged), storageProvider, query, entityIds, orchestrations);

            Assert.Equal(includeDeleted ? 7 : 4, result.Count);
        }

        private async Task<IList<DurableEntityStatus>> DurableEntity_ListEntitiesAsync(string taskHub, string storageProvider, EntityQuery query, IList<EntityId> entitiyIds, IList<string> orchestrations = null)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                taskHub,
                true,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                TestDurableClient client = null;

                for (int i = 0; i < entitiyIds.Count; i++)
                {
                    EntityId id = entitiyIds[i];
                    string orchestrationName = orchestrations == null ? nameof(TestOrchestrations.EntityId_SignalAndCallStringStore) : orchestrations[i];
                    client = await host.StartOrchestratorAsync(orchestrationName, id, this.output);

                    await client.WaitForCompletionAsync(this.output);
                }

                if (storageProvider == TestHelpers.AzureStorageProviderType)
                {
                    // account for delay in updating instance tables
                    await Task.Delay(TimeSpan.FromSeconds(20));
                }

                List<DurableEntityStatus> results = new List<DurableEntityStatus>();

                do
                {
                    var result = await client.InnerClient.ListEntitiesAsync(query, CancellationToken.None);

                    // The result may return fewer records than the page size, but never more
                    Assert.True(result.Entities.Count() <= query.PageSize);

                    foreach (var element in result.Entities)
                    {
                        results.Add(element);
                    }

                    query.ContinuationToken = result.ContinuationToken;
                }
                while (query.ContinuationToken != null);

                await host.StopAsync();

                return results;
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_CleanEntityStorage(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_CleanEntityStorage),
                enableExtendedSessions: false, // we use a failing replay to create the orphaned lock
                entityMessageReorderWindowInMinutes: 0, // need to set this to zero so deleted entities can be removed immediately
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // construct unique names for this test
                string prefix = Guid.NewGuid().ToString("N").Substring(0, 6);
                var emptyEntityId = new EntityId("Counter", $"{prefix}-empty");
                var orphanedEntityId = new EntityId(nameof(TestEntityClasses.CounterWithProxy), $"{prefix}-orphaned");
                var orchestrationA = $"{prefix}-A";
                var orchestrationB = $"{prefix}-B";

                // create an empty entity
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CreateEmptyEntities), new EntityId[] { emptyEntityId }, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                if (storageProvider == TestHelpers.AzureStorageProviderType)
                {
                    // account for delay in updating instance tables
                    await Task.Delay(TimeSpan.FromSeconds(20));
                }

                // check that the empty entity record is still there in storage
                var query = new EntityQuery
                {
                    EntityName = emptyEntityId.EntityName,
                    IncludeDeleted = true,
                };
                var result = await client.InnerClient.ListEntitiesAsync(query, CancellationToken.None);
                Assert.Contains(result.Entities, s => s.EntityId.Equals(emptyEntityId));

                // run an orchestration A that leaves an orphaned lock
                TestDurableClient clientA = await host.StartOrchestratorAsync(nameof(TestOrchestrations.LockThenFailReplay), (orphanedEntityId, true), this.output, orchestrationA);
                status = await clientA.WaitForCompletionAsync(this.output);

                // run an orchestration B that queues behind A for the lock (and thus gets stuck)
                TestDurableClient clientB = await host.StartOrchestratorAsync(nameof(TestOrchestrations.LockThenFailReplay), (orphanedEntityId, false), this.output, orchestrationB);

                // remove empty entity and release orphaned lock
                var response = await client.InnerClient.CleanEntityStorageAsync(true, true, CancellationToken.None);
                Assert.Equal(1, response.NumberOfOrphanedLocksRemoved);
                Assert.Equal(1, response.NumberOfEmptyEntitiesRemoved);

                // wait for orchestration B to complete, now that the lock has been released
                status = await clientB.WaitForCompletionAsync(this.output);
                Assert.True(status.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

                // check that the empty entity record has been removed from storage
                result = await client.InnerClient.ListEntitiesAsync(query, CancellationToken.None);
                Assert.DoesNotContain(result.Entities, s => s.EntityId.Equals(emptyEntityId));

                // clean again to remove the orphaned entity which is now empty also
                response = await client.InnerClient.CleanEntityStorageAsync(true, true, CancellationToken.None);
                Assert.Equal(0, response.NumberOfOrphanedLocksRemoved);
                Assert.Equal(1, response.NumberOfEmptyEntitiesRemoved);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DurableEntity_CleanEntityStorage_Many(string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DurableEntity_CleanEntityStorage_Many),
                enableExtendedSessions: false, // we use a failing replay to create the orphaned lock
                entityMessageReorderWindowInMinutes: 0, // need to set this to zero so deleted entities can be removed immediately
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int numReps = 120; // is above the default page size for queries

                // construct unique names for this test
                string prefix = Guid.NewGuid().ToString("N").Substring(0, 6);
                EntityId[] entityIds = new EntityId[numReps];
                for (int i = 0; i < entityIds.Length; i++)
                {
                    entityIds[i] = new EntityId("Counter", $"{prefix}-{i:D3}");
                }

                // create the empty entities
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CreateEmptyEntities), entityIds, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                if (storageProvider == TestHelpers.AzureStorageProviderType)
                {
                    // account for delay in updating instance tables
                    await Task.Delay(TimeSpan.FromSeconds(20));
                }

                // remove all empty entities
                var response = await client.InnerClient.CleanEntityStorageAsync(true, true, CancellationToken.None);
                Assert.Equal(0, response.NumberOfOrphanedLocksRemoved);
                Assert.Equal(numReps, response.NumberOfEmptyEntitiesRemoved);

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

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Dedupe_Default_NotRunning_ThrowsException(bool extendedSessions, string storageProvider)
        {
           var instanceId = "OverridableStatesDefaultTest_" + Guid.NewGuid().ToString("N");

           using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Dedupe_Default_NotRunning_ThrowsException),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int initialValue = 0;

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output, instanceId: instanceId);

                // Wait for the instance to go into the Running state. This is necessary to ensure log validation consistency.
                await client.WaitForStartupAsync(this.output);

                TimeSpan waitTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 300 : 10);

                // Perform some operations
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 1);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.GetStatusAsync();
                Assert.True(
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                FunctionInvocationException exception =
                    await Assert.ThrowsAsync<FunctionInvocationException>(async () =>
                    {
                        await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output, instanceId: instanceId);
                    });

                Assert.Equal(
                    "An Orchestration instance with the status Running already exists.",
                    exception.InnerException.Message);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task DedupeStates_AnyState(bool extendedSessions, string storageProvider)
        {
            DurableTaskOptions options = new DurableTaskOptions();
            options.OverridableExistingInstanceStates = OverridableStates.AnyState;

            var instanceId = "OverridableStatesAnyStateTest_" + Guid.NewGuid().ToString("N");

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.DedupeStates_AnyState),
                extendedSessions,
                storageProviderType: storageProvider,
                options: options))
            {
                await host.StartAsync();

                int initialValue = 0;

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output, instanceId: instanceId);

                // Wait for the instance to go into the Running state. This is necessary to ensure log validation consistency.
                await client.WaitForStartupAsync(this.output);

                TimeSpan waitTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 300 : 10);

                // Perform some operations
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 1);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.GetStatusAsync();
                Assert.True(
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output, instanceId: instanceId);

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
            int numAttempts = 5;
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
                catch (Exception)
                {
                    Assert.True(false, "Could not start up two hosts on the same device in parallel");
                }
                finally
                {
                    foreach (var host in hosts)
                    {
                        await host.StopAsync();
                        host.Dispose();
                    }
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

                using (var host = TestHelpers.GetJobHostWithOptions(this.loggerProvider, options))
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
        [InlineData("-taskhubnametest", "taskhubnametest")]
        [InlineData("-1taskhubnametest", "t1taskhubnametest")]
        [InlineData("--------", "DefaultTaskHub")]
        [InlineData("bb", "bbHub")]
        public void TaskHubName_DefaultHubName_UseSanitized(string siteName, string expectedHubName)
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

            taskHubName += PlatformSpecificHelpers.VersionSuffix;
            ArgumentException argumentException =
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    using (var host = TestHelpers.GetJobHost(
                        this.loggerProvider,
                        nameof(this.TaskHubName_Throws_ArgumentException),
                        false,
                        exactTaskHubName: taskHubName))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

            Assert.NotNull(argumentException);
            Assert.Equal(
                $"Task hub name '{taskHubName}' should contain only alphanumeric characters, start with a letter, and have length between 3 and 45.",
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

            using (var host = TestHelpers.GetJobHostWithOptions(
                this.loggerProvider,
                durableTaskOptions,
                nameResolver: nameResolver))
            {
                await host.StartAsync();
                await host.StopAsync();
            }

            Assert.False(durableTaskOptions.ExtendedSessionsEnabled);
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

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", (string)status?.Input);
                Assert.Equal("Hello, World!", (string)status?.Output);

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
                var status = client.GetStatusAsync();

                Assert.NotNull(status);
                Assert.DoesNotContain("Value2", status.Result.Output.ToString());

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TaskHubName_DefaultNameSiteTooLong_UsesSanitizedHubName()
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
                    Assert.Equal(expectedHubName, options.HubName);
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

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
#if FUNCTIONS_V1
                var logger = this.loggerProvider.CreatedLoggers.FirstOrDefault(l => l.Category.Equals("Function"));
#else
                var logger = this.loggerProvider.CreatedLoggers.FirstOrDefault(l => l.Category.Equals("Function.ReplaySafeLogger_OneLogMessage.User"));
#endif
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
                await Task.Delay(3000);
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

        private static StringBuilder GenerateMediumRandomStringPayload()
        {
            // Generate a medium random string payload
            const int TargetPayloadSize = 128 * 1024; // 128 KB
            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 {}/<>.-";
            var sb = new StringBuilder();
            var random = new Random();
            while (Encoding.Unicode.GetByteCount(sb.ToString()) < TargetPayloadSize)
            {
                for (int i = 0; i < 1000; i++)
                {
                    sb.Append(Chars[random.Next(Chars.Length)]);
                }
            }

            return sb;
        }

        private static async Task<int> GetBlobCount(string containerName, string directoryName)
        {
            string storageConnectionString = TestHelpers.GetStorageConnectionString();
            CloudStorageAccount storageAccount;
            if (!CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                return 0;
            }

            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            await cloudBlobContainer.CreateIfNotExistsAsync();
            CloudBlobDirectory instanceDirectory = cloudBlobContainer.GetDirectoryReference(directoryName);
            int blobCount = 0;
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                BlobResultSegment results = await instanceDirectory.ListBlobsSegmentedAsync(blobContinuationToken);
                blobContinuationToken = results.ContinuationToken;
                blobCount += results.Results.Count();
            }
            while (blobContinuationToken != null);

            return blobCount;
        }

        private static async Task EnsureBlobContainerExists(string containerName)
        {
            var storageConnectionString = TestHelpers.GetStorageConnectionString();
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var cloudBlobClient = storageAccount.CreateCloudBlobClient();
            var cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            await cloudBlobContainer.CreateIfNotExistsAsync();
        }

        private static async Task ValidateBlobUrlAsync(string taskHubName, string instanceId, string value)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(TestHelpers.GetStorageConnectionString());
            Assert.StartsWith(account.BlobStorageUri.PrimaryUri.OriginalString, value);
            Assert.Contains("/" + instanceId + "/", value);
            Assert.EndsWith(".json.gz", value);

            string containerName = $"{taskHubName.ToLowerInvariant()}-largemessages";
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(containerName);
            Assert.True(await container.ExistsAsync(), $"Blob container {containerName} is expected to exist.");

            await client.GetBlobReferenceFromServerAsync(new Uri(value));
            CloudBlobDirectory instanceDirectory = container.GetDirectoryReference(instanceId);

            string blobName = value.Split('/').Last();
            CloudBlob blob = instanceDirectory.GetBlobReference(blobName);
            Assert.True(await blob.ExistsAsync(), $"Blob named {blob.Uri} is expected to exist.");
        }

        private static void ValidateHttpManagementPayload(HttpManagementPayload httpManagementPayload, bool extendedSessions, string taskHubName)
        {
            Assert.NotNull(httpManagementPayload);
            Assert.NotEmpty(httpManagementPayload.Id);
            string instanceId = httpManagementPayload.Id;
            string notificationUrl = TestConstants.NotificationUrlBase;

            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}?taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.StatusQueryGetUri);
            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}/raiseEvent/{{eventName}}?taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.SendEventPostUri);
            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}/terminate?reason={{text}}&taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.TerminatePostUri);
            Assert.Equal(
                $"{notificationUrl}/instances/{instanceId}/restart?taskHub={taskHubName}&connection=AzureWebJobsStorage&code=mykey",
                httpManagementPayload.RestartPostUri);
        }

        [DataContract]
        internal class ComplexType
        {
            [DataContract]
            public enum CustomEnum
            {
                [EnumMember]
                Value1,

                [EnumMember]
                Value2,
            }

            [DataMember]
            public int A { get; set; }

            [DataMember]
            public List<DateTime> B { get; set; }

            [DataMember]
            public CustomEnum C { get; set; }

            [DataMember]
            public ComplexInnerType D { get; set; }

            [DataContract]
            public class ComplexInnerType
            {
                [DataMember]
                public string E { get; set; }

                [DataMember]
                public TimeSpan F { get; set; }
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
