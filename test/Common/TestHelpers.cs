// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DurableFunctions.Tests.Common;
using DurableTask.AzureStorage;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
#if !FUNCTIONS_V1
using Microsoft.Extensions.Hosting;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TestHelpers : IDisposable
    {
#if FUNCTIONSV1
        public const string DefaultTestCategory = "FunctionsV1";
#else
        public const string DefaultTestCategory = "Functions";
#endif
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;
        private readonly bool useTestLogger;
        private readonly LogEventTraceListener eventSourceListener;

        public const string BVTTestCategory = DefaultTestCategory + "_BVT";
        public const string FlakeyTestCategory = DefaultTestCategory + "_Flakey";

        // Friendly strings for provider types so easier to read in enumerated test output
        public const string AzureStorageProviderType = "azure_storage";
        public const string EmulatorProviderType = "emulator";
        public const string RedisProviderType = "redis";

        public const string LogCategory = "Host.Triggers.DurableTask";
        public const string EmptyStorageProviderType = "empty";

        protected Dictionary<DurableTaskOptions, TestNameResolver> nameResolvers;

        public TestHelpers(ITestOutputHelper outputHelper)
        {
            this.output = outputHelper;
            this.loggerProvider = new TestLoggerProvider(outputHelper);
            this.eventSourceListener = new LogEventTraceListener();
            this.useTestLogger = this.IsLogFriendlyPlatform();
            this.StartLogCapture();
            this.nameResolvers = new Dictionary<DurableTaskOptions, TestNameResolver>();
        }

#if !FUNCTIONS_V1
        public virtual void RegisterDurabilityFactory(IWebJobsBuilder builder, IOptions<DurableTaskOptions> options, Type durableProviderFactoryType = null)
        {
            builder.AddTestDurableTask(options, durableProviderFactoryType);
        }

        public virtual Task PreHostStartupOp(IOptions<DurableTaskOptions> options)
        {
            return Task.CompletedTask;
        }

        public virtual Task PostHostShutdownOp(IOptions<DurableTaskOptions> options)
        {
            return Task.CompletedTask;
        }

        public virtual void AddSettings(DurableTaskOptions options, TestNameResolver testNameResolver)
        {
            this.nameResolvers[options] = testNameResolver;
        }
#endif

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

        // Testing on Linux currently throws exception in LogEventTraceListener.
        // May also need to limit on OSX.
        private bool IsLogFriendlyPlatform()
        {
            return !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }

        public ITestHost CreateJobHost(
#if !FUNCTIONS_V1
            IOptions<DurableTaskOptions> options,
            string storageProvider,
            Type durabilityProviderFactoryType,
            ILoggerProvider loggerProvider,
            TestNameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper,
            IMessageSerializerSettingsFactory serializerSettingsFactory,
            Action<ITelemetry> onSend,
            bool addDurableClientFactory,
            ITypeLocator typeLocator)
        {
            this.AddSettings(options.Value, nameResolver);
            return PlatformSpecificHelpers.CreateJobHost(
                options,
                (builder) => this.RegisterDurabilityFactory(builder, options, durabilityProviderFactoryType),
                loggerProvider,
                nameResolver,
                durableHttpMessageHandler,
                lifeCycleNotificationHelper,
                serializerSettingsFactory,
                onSend,
                addDurableClientFactory,
                typeLocator,
                () => this.PreHostStartupOp(options),
                () => this.PostHostShutdownOp(options));
        }
#else
            IOptions<DurableTaskOptions> options,
            string storageProvider,
            ILoggerProvider loggerProvider,
            INameResolver nameResolver,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper,
            IMessageSerializerSettingsFactory serializerSettingsFactory,
            IApplicationLifetimeWrapper shutdownNotificationService = null,
#pragma warning disable CS0612 // Type or member is obsolete
            Action<ITelemetry> onSend = null,
            IPlatformInformation platformInformationService = null)
        {
            return PlatformSpecificHelpers.CreateJobHost(
                options,
                storageProvider,
                loggerProvider,
                nameResolver,
                durableHttpMessageHandler,
                lifeCycleNotificationHelper,
                serializerSettingsFactory,
                shutdownNotificationService,
                onSend,
                platformInformationService);
        }
#endif

        public string GetTaskHubSuffix()
        {
#if FUNCTIONS_V1
            return "V1";
#else
            return string.Empty;
#endif
        }

#if !FUNCTIONS_V1
        public IHost CreateJobHostExternalEnvironment(IConnectionStringResolver connectionStringResolver)
        {
            return PlatformSpecificHelpers.CreateJobHostExternalEnvironment(connectionStringResolver);
        }
#endif

        public ITestHost GetJobHost(
            string testName,
            bool enableExtendedSessions,
            string eventGridKeySettingName = null,
            INameResolver nameResolver = null,
            string eventGridTopicEndpoint = null,
            int? eventGridRetryCount = null,
            TimeSpan? eventGridRetryInterval = null,
            int[] eventGridRetryHttpStatus = null,
            bool traceReplayEvents = true,
            bool allowVerboseLinuxTelemetry = false,
            Uri notificationUrl = null,
            HttpMessageHandler eventGridNotificationHandler = null,
            TimeSpan? maxQueuePollingInterval = null,
            string[] eventGridPublishEventTypes = null,
            string storageProviderType = AzureStorageProviderType,
            Type durabilityProviderFactoryType = null,
            bool autoFetchLargeMessages = true,
            int httpAsyncSleepTime = 500,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler = null,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper = null,
            IMessageSerializerSettingsFactory serializerSettings = null,
            bool? localRpcEndpointEnabled = false,
            DurableTaskOptions options = null,
            Action<ITelemetry> onSend = null,
            bool rollbackEntityOperationsOnExceptions = true,
            int entityMessageReorderWindowInMinutes = 30,
            string exactTaskHubName = null,
            bool addDurableClientFactory = false,
            Type[] types = null)
        {
            switch (storageProviderType)
            {
                case AzureStorageProviderType:
#if !FUNCTIONS_V1
                case RedisProviderType:
                case EmulatorProviderType:
#endif
                    break;
                default:
                    throw new InvalidOperationException($"Storage provider {storageProviderType} is not supported for testing infrastructure.");
            }

            if (options == null)
            {
                options = new DurableTaskOptions();
            }

            // Some tests require knowing the task hub that the provider uses. Because of that, they will instantiate the exact
            // task hub name and require the usage of that task hub. Otherwise, generate a partially random task hub from the
            // test name and properties of the test.
            options.HubName = exactTaskHubName ?? this.GetTaskHubNameFromTestName(testName, enableExtendedSessions);

            options.Tracing.TraceInputsAndOutputs = true;
            options.Tracing.TraceReplayEvents = traceReplayEvents;
            options.Tracing.AllowVerboseLinuxTelemetry = allowVerboseLinuxTelemetry;

            options.Notifications = new NotificationOptions()
            {
                EventGrid = new EventGridNotificationOptions()
                {
                    KeySettingName = eventGridKeySettingName,
                    TopicEndpoint = eventGridTopicEndpoint,
                    PublishEventTypes = eventGridPublishEventTypes,
                },
            };
            options.HttpSettings = new HttpOptions()
            {
                DefaultAsyncRequestSleepTimeMilliseconds = httpAsyncSleepTime,
            };
            options.WebhookUriProviderOverride = () => notificationUrl;
            options.ExtendedSessionsEnabled = enableExtendedSessions;
            options.MaxConcurrentOrchestratorFunctions = 200;
            options.MaxConcurrentActivityFunctions = 200;
            options.NotificationHandler = eventGridNotificationHandler;
            options.LocalRpcEndpointEnabled = localRpcEndpointEnabled;
            options.RollbackEntityOperationsOnExceptions = rollbackEntityOperationsOnExceptions;
            options.EntityMessageReorderWindowInMinutes = entityMessageReorderWindowInMinutes;

            // Azure Storage specfic tests
            if (string.Equals(storageProviderType, AzureStorageProviderType))
            {
                options.StorageProvider["ConnectionStringName"] = "AzureWebJobsStorage";
                options.StorageProvider["fetchLargeMessagesAutomatically"] = autoFetchLargeMessages;
                if (maxQueuePollingInterval != null)
                {
                    options.StorageProvider["maxQueuePollingInterval"] = maxQueuePollingInterval.Value;
                }
            }

            if (eventGridRetryCount.HasValue)
            {
                options.Notifications.EventGrid.PublishRetryCount = eventGridRetryCount.Value;
            }

            if (eventGridRetryInterval.HasValue)
            {
                options.Notifications.EventGrid.PublishRetryInterval = eventGridRetryInterval.Value;
            }

            if (eventGridRetryHttpStatus != null)
            {
                options.Notifications.EventGrid.PublishRetryHttpStatus = eventGridRetryHttpStatus;
            }

            if (maxQueuePollingInterval != null)
            {
                options.StorageProvider["maxQueuePollingInterval"] = maxQueuePollingInterval.Value;
            }

            return this.GetJobHostWithOptions(
                durableTaskOptions: options,
                storageProviderType: storageProviderType,
                nameResolver: nameResolver,
                durableHttpMessageHandler: durableHttpMessageHandler,
                lifeCycleNotificationHelper: lifeCycleNotificationHelper,
                serializerSettings: serializerSettings,
#if !FUNCTIONS_V1
                onSend: onSend,
                addDurableClientFactory: addDurableClientFactory,
                types: types,
#endif
                durabilityProviderFactoryType: durabilityProviderFactoryType);
        }

        public ITestHost GetJobHostWithOptions(
            DurableTaskOptions durableTaskOptions,
            string storageProviderType = AzureStorageProviderType,
            INameResolver nameResolver = null,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandler = null,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper = null,
            IMessageSerializerSettingsFactory serializerSettings = null,
            Action<ITelemetry> onSend = null,
            Type durabilityProviderFactoryType = null,
            bool addDurableClientFactory = false,
            Type[] types = null)
        {
            if (serializerSettings == null)
            {
                serializerSettings = new MessageSerializerSettingsFactory();
            }

            var optionsWrapper = new OptionsWrapper<DurableTaskOptions>(durableTaskOptions);
            var testNameResolver = new TestNameResolver(nameResolver);
            if (durableHttpMessageHandler == null)
            {
                durableHttpMessageHandler = new DurableHttpMessageHandlerFactory();
            }

            ITypeLocator typeLocator = types == null ? GetTypeLocator() : new ExplicitTypeLocator(types);

            return this.CreateJobHost(
                options: optionsWrapper,
                storageProvider: storageProviderType,
#if !FUNCTIONS_V1
                durabilityProviderFactoryType: durabilityProviderFactoryType,
                addDurableClientFactory: addDurableClientFactory,
                typeLocator: typeLocator,
#endif
                onSend: onSend,
                loggerProvider: this.loggerProvider,
                nameResolver: testNameResolver,
                durableHttpMessageHandler: durableHttpMessageHandler,
                lifeCycleNotificationHelper: lifeCycleNotificationHelper,
                serializerSettingsFactory: serializerSettings);
        }

#if !FUNCTIONS_V1
        public IHost GetJobHostExternalEnvironment(IConnectionStringResolver connectionStringResolver = null)
        {
            if (connectionStringResolver == null)
            {
                connectionStringResolver = new TestConnectionStringResolver();
            }

            return this.GetJobHostWithOptionsForDurableClientFactoryExternal(connectionStringResolver);
        }

        public IHost GetJobHostWithOptionsForDurableClientFactoryExternal(IConnectionStringResolver connectionStringResolver)
        {
            return this.CreateJobHostExternalEnvironment(connectionStringResolver);
        }

        public ITestHost GetJobHostWithMultipleDurabilityProviders(
            DurableTaskOptions options = null,
            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = null)
        {
            if (options == null)
            {
                options = new DurableTaskOptions();
            }

            return this.GetJobHostWithOptionsWithMultipleDurabilityProviders(
                options,
                durabilityProviderFactories);
        }

        public ITestHost GetJobHostWithOptionsWithMultipleDurabilityProviders(
            DurableTaskOptions durableTaskOptions,
            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = null)
        {
            var optionsWrapper = new OptionsWrapper<DurableTaskOptions>(durableTaskOptions);

            return PlatformSpecificHelpers.CreateJobHostWithMultipleDurabilityProviders(
                optionsWrapper,
                durabilityProviderFactories);
        }
#endif

#pragma warning disable CS0612 // Type or member is obsolete
        public static IPlatformInformation GetMockPlatformInformationService(
            bool inConsumption = false,
            OperatingSystem operatingSystem = OperatingSystem.Windows,
            WorkerRuntimeType language = WorkerRuntimeType.DotNet,
            string getLinuxStampName = "",
            string getContainerName = "")
#pragma warning restore CS0612 // Type or member is obsolete
        {
#pragma warning disable CS0612 // Type or member is obsolete
            var mockPlatformProvider = new Mock<IPlatformInformation>();
#pragma warning restore CS0612 // Type or member is obsolete
            mockPlatformProvider.Setup(x => x.GetOperatingSystem()).Returns(operatingSystem);
            mockPlatformProvider.Setup(x => x.IsInConsumptionPlan()).Returns(inConsumption);
            mockPlatformProvider.Setup(x => x.GetLinuxStampName()).Returns(getLinuxStampName);
            mockPlatformProvider.Setup(x => x.GetContainerName()).Returns(getContainerName);
            mockPlatformProvider.Setup(x => x.GetWorkerRuntimeType()).Returns(language);
            return mockPlatformProvider.Object;
        }

        public static DurableTaskOptions GetDurableTaskOptionsForStorageProvider(string storageProvider)
        {
            switch (storageProvider)
            {
                case AzureStorageProviderType:
#if !FUNCTIONS_V1
                case RedisProviderType:
                case EmulatorProviderType:
#endif
                    return new DurableTaskOptions();
                default:
                    throw new InvalidOperationException($"Storage provider {storageProvider} is not supported for testing infrastructure.");
            }
        }

        /// <summary>
        /// Returns <c>true</c> if a JSON Durable log has all the minimum-required keys.
        /// Else, returns <c>false</c>.
        /// </summary>
        /// <param name="json">The JSON log to validate.</param>
        public static bool IsValidJSONLog(JObject json)
        {
            List<string> expectedKeys = new List<string>
            {
                "EventStampName",
                "EventPrimaryStampName",
                "ProviderName",
                "TaskName",
                "EventId",
                "EventTimestamp",
                "Tenant",
                "Pid",
                "Tid",
            };

            foreach (string expectedKey in expectedKeys)
            {
                if (!json.TryGetValue(expectedKey, out _))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Read a file's contents, line by line, even if another process is currently writing to it.
        /// </summary>
        /// <param name="path">The file's path.</param>
        /// <returns>An array of each line in the file.</returns>
        public static List<string> WriteSafeReadAllLines(string path)
        {
            /* A method like File.ReadAllLines cannot open a file that is open for writing by another process
             * This is due to the File.ReadAllLines  not opening the process with ReadWrite permissions.
             * As a result, we implement a variant ReadAllLines with the right permission mode.
             *
             * More info in: https://stackoverflow.com/questions/12744725/how-do-i-perform-file-readalllines-on-a-file-that-is-also-open-in-excel
             */
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream, Encoding.Default))
            {
                List<string> file = new List<string>();
                while (!streamReader.EndOfStream)
                {
                    file.Add(streamReader.ReadLine());
                }

                return file;
            }
        }

        /// <summary>
        /// Helper function to regularly poll for some condition until it is true. If timeout hits, throw timeoutexception.
        /// </summary>
        /// <param name="predicate">Predicate to wait until it returns true.</param>
        /// <param name="conditionDescription">Condition description in exception if condition not met.</param>
        /// <param name="timeout">Time to wait until predicate is true.</param>
        /// <param name="retryInterval">How frequently to test predicate. Defaults to 100 ms.</param>
        public static async Task WaitUntilTrue(Func<bool> predicate, string conditionDescription, TimeSpan timeout, TimeSpan? retryInterval = null)
        {
            if (retryInterval == null)
            {
                retryInterval = TimeSpan.FromMilliseconds(100);
            }

            Stopwatch sw = Stopwatch.StartNew();

            do
            {
                if (predicate())
                {
                    return;
                }

                await Task.Delay(retryInterval.Value);
            }
            while (sw.Elapsed < timeout);

            throw new TimeoutException($"Did not meet {conditionDescription} within {timeout}");
        }

        // Create a valid task hub from the test name, and add a random suffix to avoid conflicts
        public string GetTaskHubNameFromTestName(string testName, bool enableExtendedSessions)
        {
            string strippedTestName = testName.Replace("_", "");
            string truncatedTestName = strippedTestName.Substring(0, Math.Min(35, strippedTestName.Length));
            string testPropertiesSuffix = (enableExtendedSessions ? "EX" : "") + this.GetTaskHubSuffix();
            string randomSuffix = Guid.NewGuid().ToString().Substring(0, 4);
            return truncatedTestName + testPropertiesSuffix + randomSuffix;
        }

        public static ITypeLocator GetTypeLocator()
        {
            var types = new Type[]
            {
                typeof(TestOrchestrations),
                typeof(TestActivities),
                typeof(TestEntities),
                typeof(TestEntityClasses),
                typeof(ClientFunctions),
                typeof(UnconstructibleClass),
            };

            ITypeLocator typeLocator = new ExplicitTypeLocator(types);
            return typeLocator;
        }

        public static string GetStorageConnectionString()
        {
            return Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        }

        public Task DeleteTaskHubResources(string testName, bool enableExtendedSessions)
        {
            string hubName = this.GetTaskHubNameFromTestName(testName, enableExtendedSessions);
            var settings = new AzureStorageOrchestrationServiceSettings
            {
                TaskHubName = hubName,
                StorageConnectionString = GetStorageConnectionString(),
            };

            var service = new AzureStorageOrchestrationService(settings);
            return service.DeleteAsync();
        }

        public TestLogger GetLogger(string categoryName)
        {
            return this.loggerProvider.CreatedLoggers.FirstOrDefault(logger => string.Equals(categoryName, logger.Category, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<LogMessage> GetAllLogMessages()
        {
            return this.loggerProvider.GetAllLogMessages();
        }

        public void AssertLogMessageSequence(
            ITestOutputHelper testOutput,
            string testName,
            string instanceId,
            bool filterOutReplayLogs,
            string[] orchestratorFunctionNames,
            string activityFunctionName = null)
        {
            if (this.useTestLogger)
            {
                List<string> messageIds;
                string timeStamp;
                var logMessages = this.GetLogMessages(testName, instanceId, out messageIds, out timeStamp);

                var expectedLogMessages = ExpectedTestLogs.GetExpectedLogMessages(
                    testName,
                    messageIds,
                    orchestratorFunctionNames,
                    filterOutReplayLogs,
                    activityFunctionName,
                    timeStamp);
                var actualLogMessages = logMessages.Select(m => m.FormattedMessage).ToList();

                AssertLogMessages(expectedLogMessages, actualLogMessages, testOutput);
            }
        }

        public void UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(
            ITestOutputHelper testOutput,
            string testName,
            string subOrchestrationInstanceId,
            string[] orchestratorFunctionNames,
            string activityFunctionName = null)
        {
            if (this.useTestLogger)
            {
                List<string> messageIds;
                string timeStamp;
                var logMessages = this.GetLogMessages(testName, subOrchestrationInstanceId, out messageIds, out timeStamp);

                var actualLogMessages = logMessages.Select(m => m.FormattedMessage).ToList();
                var exceptionCount =
                    actualLogMessages.FindAll(m => m.Contains("failed with an error")).Count;

                // The sub-orchestration call is configured to make at most 3 attempts.
                Assert.Equal(3, exceptionCount);
            }
        }

        private List<LogMessage> GetLogMessages(
            string testName,
            string instanceId,
            out List<string> instanceIds,
            out string timeStamp)
        {
            var logger = this.loggerProvider.CreatedLoggers.Single(l => l.Category == LogCategory);
            var logMessages = logger.LogMessages.ToList();

            // Remove any logs which may have been generated by concurrently executing orchestrations.
            // Sub-orchestrations are expected to be prefixed with the parent orchestration instance ID.
            logMessages.RemoveAll(msg => !msg.FormattedMessage.Contains(instanceId));

            instanceIds = new List<string> { instanceId };

            timeStamp = string.Empty;

            if (testName.Equals("TimerCancellation", StringComparison.OrdinalIgnoreCase) ||
                testName.Equals("TimerExpiration", StringComparison.OrdinalIgnoreCase))
            {
                // It is assumed that the 4th log message is a timer message.
                timeStamp = GetTimerTimestamp(logMessages[3].FormattedMessage);
            }
            else if (testName.Equals("Orchestration_OnValidOrchestrator", StringComparison.OrdinalIgnoreCase) ||
                     testName.Equals("Orchestration_Activity", StringComparison.OrdinalIgnoreCase))
            {
                // It is assumed that the 5th log message is a sub-orchestration
                instanceIds.Add(GetInstanceId(logMessages[4].FormattedMessage));
            }

            Assert.True(
                logMessages.TrueForAll(m => m.Category.Equals(LogCategory, StringComparison.InvariantCultureIgnoreCase)));

            return logMessages;
        }

        private static void AssertLogMessages(IList<string> expected, IList<string> actual, ITestOutputHelper testOutput)
        {
            TraceExpectedLogMessages(testOutput, expected);

            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.StartsWith(expected[i], actual[i]);
            }
        }

        private static void TraceExpectedLogMessages(ITestOutputHelper testOutput, IList<string> expected)
        {
            string prefix = "    ";
            string allExpectedTraces = string.Join(Environment.NewLine + prefix, expected);
            testOutput.WriteLine("Expected trace output:");
            testOutput.WriteLine(prefix + allExpectedTraces);
        }

        private static string GetInstanceId(string message)
        {
            return message.Substring(0, message.IndexOf(':'));
        }

        private static string GetTimerTimestamp(string message)
        {
            const string CreateTimerPrefix = "CreateTimer:";
            int start = message.IndexOf(CreateTimerPrefix) + CreateTimerPrefix.Length;
            int end = message.IndexOf('Z', start) + 1;
            return message.Substring(start, end - start);
        }

        public static INameResolver GetTestNameResolver()
        {
            return new TestNameResolver(null);
        }

        public static async Task<string> LoadStringFromTextBlobAsync(string blobName)
        {
            string connectionString = GetStorageConnectionString();
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            var blobClient = account.CreateCloudBlobClient();
            var testcontainer = blobClient.GetContainerReference("test");
            var blob = testcontainer.GetBlockBlobReference(blobName);
            try
            {
                return await blob.DownloadTextAsync();
            }
            catch (StorageException e)
                when ((e as StorageException)?.RequestInformation?.HttpStatusCode == 404)
            {
                // if the blob does not exist, just return null.
                return null;
            }
        }

        public static async Task WriteStringToTextBlob(string blobName, string content)
        {
            string connectionString = GetStorageConnectionString();
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            var blobClient = account.CreateCloudBlobClient();
            var testcontainer = blobClient.GetContainerReference("test");
            var blob = testcontainer.GetBlockBlobReference(blobName);
            await blob.UploadTextAsync(content);
        }

        public void Dispose()
        {
            this.eventSourceListener.Dispose();
        }

        private class ExplicitTypeLocator : ITypeLocator
        {
            private readonly IReadOnlyList<Type> types;

            public ExplicitTypeLocator(params Type[] types)
            {
                this.types = types.ToList().AsReadOnly();
            }

            public ExplicitTypeLocator(List<Type> types)
            {
                this.types = types.AsReadOnly();
            }

            public IReadOnlyList<Type> GetTypes()
            {
                return this.types;
            }
        }

        public class TestNameResolver : INameResolver
        {
            private readonly Dictionary<string, string> DefaultAppSettings =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "TestTaskHub", string.Empty },
                };

            private readonly INameResolver innerResolver;

            public TestNameResolver(INameResolver innerResolver)
            {
                // null is okay
                this.innerResolver = innerResolver;
            }

            public void AddSetting(string settingName, string settingValue)
            {
                this.DefaultAppSettings.Add(settingName, settingValue);
            }

            public string Resolve(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                string value = this.innerResolver?.Resolve(name);
                if (value == null)
                {
                    this.DefaultAppSettings.TryGetValue(name, out value);
                }

                if (value == null)
                {
                    value = Environment.GetEnvironmentVariable(name);
                }

                return value;
            }
        }
    }
}
