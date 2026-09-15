// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Collection("Non-Parallel Collection")]
    public class LinuxLoggingTests
    {
        private const string LogStreamName = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
        private const string Details = "First line\r\nSecond line with \"quotes\" and a \\backslash";
        private readonly ITestOutputHelper output;

        public LinuxLoggingTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null, null, null, null, null)]
        [InlineData("host", null, null, null, null)]
        [InlineData(null, "namespace", null, null, null)]
        [InlineData("", "namespace", null, null, null)]
        [InlineData("host", "", null, null, null)]
        [InlineData("host", "namespace", null, null, "")]
        [InlineData("host", "namespace", "", null, "")]
        [InlineData("host", "namespace", null, "container", "")]
        [InlineData(null, null, null, "container", LogStreamName + " ")]
        [InlineData("host", null, null, "container", LogStreamName + " ")]
        [InlineData(null, null, "managed", null, LogStreamName + " ")]
        [InlineData("host", "namespace", "managed", null, LogStreamName + " ")]
        public async Task ConsoleLoggingMatchesHostingEnvironment(
            string kubernetesServiceHost,
            string podNamespace,
            string managedEnvironment,
            string containerName,
            string expectedPrefix)
        {
            var settings = new Dictionary<string, string>
            {
                { "KUBERNETES_SERVICE_HOST", kubernetesServiceHost ?? string.Empty },
                { "POD_NAMESPACE", podNamespace ?? string.Empty },
                { "MANAGED_ENVIRONMENT", managedEnvironment ?? string.Empty },
                { "CONTAINER_NAME", containerName ?? string.Empty }
                { "FUNCTIONS_WORKER_RUNTIME", "dotnet" },
            };
            string instanceId = Guid.NewGuid().ToString();
            TextWriter originalConsole = Console.Out;
            using var console = new StringWriter();
            try
            {
                Console.SetOut(console);
                await this.EmitLogAsync(settings, instanceId);
            }
            finally
            {
                Console.SetOut(originalConsole);
            }

            string[] lines = console.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            if (expectedPrefix == null)
            {
                Assert.Empty(lines);
                return;
            }

            Assert.NotEmpty(lines);
            var logs = lines.Select(line =>
            {
                Assert.StartsWith(expectedPrefix, line);
                return JObject.Parse(line.Substring(expectedPrefix.Length));
            }).ToArray();
            JObject log = Assert.Single(logs, json => (string)json["InstanceId"] == instanceId);
            AssertLogPayload(log);
            Assert.Equal(expectedPrefix.Length == 0 ? LogStreamName : null, (string)log["EventType"]);
            AssertProviderPayload(logs, instanceId, kubernetes: expectedPrefix.Length == 0);
            Assert.Equal(containerName == null ? null : "App-" + containerName, (string)log["RoleInstance"]);
            Assert.Null(log["Tenant"]);
            Assert.Null(log["EventStampName"]);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task KubernetesLoggingTakesPrecedenceOverDedicatedLogging(bool kubernetes)
        {
            var settings = new Dictionary<string, string>
            {
                { "WEBSITE_INSTANCE_ID", "instance" },
                { "FUNCTIONS_LOGS_MOUNT_PATH", "logs" },
                { "FUNCTIONS_WORKER_RUNTIME", "dotnet" },
                { "WEBSITE_STAMP_DEPLOYMENT_ID", "tenant" },
                { "WEBSITE_HOME_STAMPNAME", "stamp01a" },
                { "KUBERNETES_SERVICE_HOST", kubernetes ? "host" : string.Empty },
                { "POD_NAMESPACE", kubernetes ? "namespace" : string.Empty },
                { "MANAGED_ENVIRONMENT", string.Empty },
            string instanceId = Guid.NewGuid().ToString();
            string logDirectory = Path.Combine(Path.GetTempPath(), "DurableLoggingTests", instanceId);
            string logPath = Path.Combine(logDirectory, "events.log");
            string originalLoggingPath = LinuxAppServiceLogger.LoggingPath;
            TextWriter originalConsole = Console.Out;
            using var console = new StringWriter();
            try
            {
                LinuxAppServiceLogger.LoggingPath = logPath;
                Console.SetOut(console);
                await this.EmitLogAsync(settings, instanceId);

                string[] lines;
                if (kubernetes)
                {
                    Assert.False(File.Exists(logPath));
                    lines = console.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    Assert.Equal(string.Empty, console.ToString());
                    await TestHelpers.WaitUntilTrue(
                        () => File.Exists(logPath) && TestHelpers.WriteSafeReadAllLines(logPath).Any(line => line.Contains(instanceId)),
                        conditionDescription: "Dedicated log is flushed",
                        timeout: TimeSpan.FromSeconds(40),
                        output: this.output);
                    lines = TestHelpers.WriteSafeReadAllLines(logPath).ToArray();
                }

                JObject[] logs = lines.Select(JObject.Parse).ToArray();
                JObject log = Assert.Single(logs, json => (string)json["InstanceId"] == instanceId);
                AssertLogPayload(log);
                AssertProviderPayload(logs, instanceId, kubernetes);
                Assert.Equal(kubernetes ? LogStreamName : null, (string)log["EventType"]);
                Assert.Equal("tenant", (string)log["Tenant"]);
                Assert.Equal("stamp01a", (string)log["EventStampName"]);
                Assert.Equal("stamp01", (string)log["EventPrimaryStampName"]);
            }
            finally
            {
                Console.SetOut(originalConsole);
                LinuxAppServiceLogger.LoggingPath = originalLoggingPath;
                if (Directory.Exists(logDirectory))
                {
                    Directory.Delete(logDirectory, recursive: true);
                }
            }
        }

        private static void AssertLogPayload(JObject log)
        {
            Assert.Equal("WebJobs-Extensions-DurableTask", (string)log["ProviderName"]);
            Assert.Equal("ExtensionInformationalEvent", (string)log["TaskName"]);
            Assert.Equal(213, (int)log["EventId"]);
            Assert.Equal((int)EventLevel.Informational, (int)log["Level"]);
            Assert.Equal(Details, (string)log["Details"]);
            Assert.Equal(JTokenType.Date, log["EventTimestamp"].Type);
            Assert.True((int)log["Pid"] > 0);
            Assert.True((int)log["Tid"] > 0);
            Assert.NotEqual(Guid.Empty, Guid.Parse((string)log["ExtensionGUID"]));
            Assert.Equal((string)log["InstanceId"], (string)log["ActivityId"]);
        }

        private static void AssertProviderPayload(JObject[] logs, string instanceId, bool kubernetes)
        {
            JObject providerLog = Assert.Single(logs, json => (string)json["MessageId"] == instanceId);
            Assert.Equal("DurableTask-CustomSource", (string)providerLog["ProviderName"]);
            Assert.Equal(kubernetes ? LogStreamName : "ExecutionStarted", (string)providerLog["EventType"]);
            Assert.Equal(kubernetes ? "ExecutionStarted" : null, (string)providerLog["TaskEventType"]);
            if (kubernetes)
            {
                Assert.All(logs, json => Assert.Equal(LogStreamName, (string)json["EventType"]));
            }
        }

        private async Task EmitLogAsync(Dictionary<string, string> settings, string instanceId)
        {
            using var host = TestHelpers.GetJobHost(
                new TestLoggerProvider(this.output),
                nameof(LinuxLoggingTests),
                enableExtendedSessions: false,
                nameResolver: new SimpleNameResolver(settings),
                durabilityProviderFactoryType: typeof(CustomEtwDurabilityProviderFactory),
                types: Type.EmptyTypes);
            await host.StartAsync();
            EventSource.SetCurrentThreadActivityId(Guid.Parse(instanceId), out Guid originalActivityId);
            try
            {
                EtwEventSource.Instance.ExtensionInformationalEvent("hub", "app", "slot", "function", instanceId, Details, "version");
                EtwSource.Current.Write("TaskMessage", new { EventType = "ExecutionStarted", MessageId = instanceId });
            }
            finally
            {
                EventSource.SetCurrentThreadActivityId(originalActivityId);
            }

            await host.StopAsync();
        }
    }
}
