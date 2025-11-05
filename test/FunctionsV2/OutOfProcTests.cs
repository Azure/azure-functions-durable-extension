// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualBasic;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.TaskOrchestrationShim;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OutOfProcTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public OutOfProcTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallHttpActionOrchestration()
        {
            DurableHttpRequest request = null;

            // Mock the CallHttpAsync API so we can capture the request and return a fixed response.
            var contextMock = new Mock<IDurableOrchestrationContext>();
            contextMock
                .Setup(ctx => ctx.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .Callback<DurableHttpRequest>(req => request = req)
                .Returns(Task.FromResult(new DurableHttpResponse(System.Net.HttpStatusCode.OK)));

            var shim = new OutOfProcOrchestrationShim(contextMock.Object);

            var executionJson = @"
{
    ""isDone"": false,
    ""actions"": [
        [{
            ""actionType"": ""CallHttp"",
            ""httpRequest"":
            {
                ""method"": ""POST"",
                ""uri"": ""https://example.com"",
                ""headers"": {
                    ""Content-Type"": ""application/json"",
                    ""Accept"": [""application/json"",""application/xml""],
                    ""x-ms-foo"": []
                },
                ""content"": ""5""
            }
        }]
    ]
}";

            // Feed the out-of-proc execution result JSON to the out-of-proc shim.
            var jsonObject = JObject.Parse(executionJson);
            OrchestrationInvocationResult result = new OrchestrationInvocationResult(jsonObject);
            bool moreWork = await shim.ScheduleDurableTaskEvents(result);

            // The request should not have completed because one additional replay is needed
            // to handle the result of CallHttpAsync. However, this test doesn't care about
            // completing the orchestration - we just need to validate the CallHttpAsync call.
            Assert.True(moreWork);
            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://example.com"), request.Uri);
            Assert.Equal("5", request.Content);
            Assert.Equal(3, request.Headers.Count);

            Assert.True(request.Headers.TryGetValue("Content-Type", out StringValues contentTypeValues));
            Assert.Single(contentTypeValues);
            Assert.Equal("application/json", contentTypeValues[0]);

            Assert.True(request.Headers.TryGetValue("Accept", out StringValues acceptValues));
            Assert.Equal(2, acceptValues.Count);
            Assert.Equal("application/json", acceptValues[0]);
            Assert.Equal("application/xml", acceptValues[1]);

            Assert.True(request.Headers.TryGetValue("x-ms-foo", out StringValues customHeaderValues));
            Assert.Equal(0, customHeaderValues.Count);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallHttpActionOrchestrationWithManagedIdentity()
        {
            DurableHttpRequest request = null;

            // Mock the CallHttpAsync API so we can capture the request and return a fixed response.
            var contextMock = new Mock<IDurableOrchestrationContext>();
            contextMock
                .Setup(ctx => ctx.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .Callback<DurableHttpRequest>(req => request = req)
                .Returns(Task.FromResult(new DurableHttpResponse(System.Net.HttpStatusCode.OK)));

            var shim = new OutOfProcOrchestrationShim(contextMock.Object);

            var executionJson = @"
{
    ""isDone"": false,
    ""actions"": [
        [{
            ""actionType"": ""CallHttp"",
            ""httpRequest"": {
                ""method"": ""GET"",
                ""uri"": ""https://example.com"",
                ""tokenSource"": {
                    ""kind"": ""AzureManagedIdentity"",
                    ""resource"": ""https://management.core.windows.net/.default""
                }
            }
        }]
    ]
}";

            // Feed the out-of-proc execution result JSON to the out-of-proc shim.
            var jsonObject = JObject.Parse(executionJson);
            OrchestrationInvocationResult result = new OrchestrationInvocationResult(jsonObject);
            bool moreWork = await shim.ScheduleDurableTaskEvents(result);

            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri("https://example.com"), request.Uri);
            Assert.Null(request.Content);

            Assert.NotNull(request.TokenSource);
            ManagedIdentityTokenSource tokenSource = Assert.IsType<ManagedIdentityTokenSource>(request.TokenSource);
            Assert.Equal("https://management.core.windows.net/.default", tokenSource.Resource);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallHttpActionOrchestrationWithManagedIdentityOptions()
        {
            DurableHttpRequest request = null;

            // Mock the CallHttpAsync API so we can capture the request and return a fixed response.
            var contextMock = new Mock<IDurableOrchestrationContext>();
            contextMock
                .Setup(ctx => ctx.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .Callback<DurableHttpRequest>(req => request = req)
                .Returns(Task.FromResult(new DurableHttpResponse(System.Net.HttpStatusCode.OK)));

            var shim = new OutOfProcOrchestrationShim(contextMock.Object);

            var executionJson = @"
{
    ""isDone"": false,
    ""actions"": [
        [{
            ""actionType"": ""CallHttp"",
            ""httpRequest"": {
                ""method"": ""GET"",
                ""uri"": ""https://example.com"",
                ""tokenSource"": {
                    ""kind"": ""AzureManagedIdentity"",
                    ""resource"": ""https://management.core.windows.net/.default"",
                    ""options"": {
                        ""authorityhost"": ""https://login.microsoftonline.com/"",
                        ""tenantid"": ""example_tenant_id""
                    }
                }
            }
        }]
    ]
}";

            // Feed the out-of-proc execution result JSON to the out-of-proc shim.
            var jsonObject = JObject.Parse(executionJson);
            OrchestrationInvocationResult result = new OrchestrationInvocationResult(jsonObject);
            bool moreWork = await shim.ScheduleDurableTaskEvents(result);

            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri("https://example.com"), request.Uri);
            Assert.Null(request.Content);

            Assert.NotNull(request.TokenSource);
            ManagedIdentityTokenSource tokenSource = Assert.IsType<ManagedIdentityTokenSource>(request.TokenSource);
            Assert.Equal("https://management.core.windows.net/.default", tokenSource.Resource);
            Assert.Equal("https://login.microsoftonline.com/", tokenSource.Options.AuthorityHost.ToString());
            Assert.Equal("example_tenant_id", tokenSource.Options.TenantId);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BindToDurableClientAsString(bool localRcpEnabled)
        {
            Uri testNotificationUrl = new Uri("https://durable.edu/runtime/webhooks/durabletask?code=abcdefg");

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.BindToDurableClientAsString) + localRcpEnabled.ToString(),
                enableExtendedSessions: false,
                localRpcEndpointEnabled: localRcpEnabled,
                notificationUrl: testNotificationUrl))
            {
                await host.StartAsync();

                // Fetch the JSON that was passed to the DurableClient binding
                string[] jsonRef = new string[1];
                await host.CallAsync(
                    nameof(ClientFunctions.GetDurableClientConfigJson),
                    new Dictionary<string, object>
                    {
                        { "jsonRef", jsonRef },
                    });

                string jsonString = jsonRef[0];
                Assert.NotNull(jsonString);
                JObject outerJson = JObject.Parse(jsonString);

                // Expected format:
                // {
                //   "taskHubName": "BindToDurableClientAsStringV2",
                //   "creationUrls": {
                //     "createNewInstancePostUri": "https://durable.edu/runtime/webhooks/durabletask/orchestrators/{functionName}[/{instanceId}]",
                //     "createAndWaitOnNewInstancePostUri": "https://durable.edu/runtime/webhooks/durabletask/orchestrators/{functionName}[/{instanceId}]?timeout={timeoutInSeconds}&pollingInterval={intervalInSeconds}"
                //   },
                //   "managementUrls": {
                //     "id": "INSTANCEID",
                //     "statusQueryGetUri": "https://durable.edu/runtime/webhooks/durabletask/instances/INSTANCEID?taskHub=BindToDurableClientAsStringV2&connection=Storage",
                //     "sendEventPostUri": "https://durable.edu/runtime/webhooks/durabletask/instances/INSTANCEID/raiseEvent/{eventName}?taskHub=BindToDurableClientAsStringV2&connection=Storage",
                //     "terminatePostUri": "https://durable.edu/runtime/webhooks/durabletask/instances/INSTANCEID/terminate?reason={text}&taskHub=BindToDurableClientAsStringV2&connection=Storage",
                //     "rewindPostUri": "https://durable.edu/runtime/webhooks/durabletask/instances/INSTANCEID/rewind?reason={text}&taskHub=BindToDurableClientAsStringV2&connection=Storage",
                //     "purgeHistoryDeleteUri": "https://durable.edu/runtime/webhooks/durabletask/instances/INSTANCEID?taskHub=BindToDurableClientAsStringV2&connection=Storage"
                //   },
                //   "baseUrl": "https://durable.edu/runtime/webhooks/durabletask/",
                //   "requiredQueryStringParameters": "code=abcdefg",
                //   "rpcBaseUrl": "http://127.0.0.1:17071/durabletask/" (or null)
                // }

                Assert.True(outerJson.TryGetValue("taskHubName", out JToken taskHubName));
                Assert.StartsWith(nameof(this.BindToDurableClientAsString), (string)taskHubName);

                // Local function that validates presence and format of the URL payloads.
                void CommonUriValidation(JToken json, string fieldName, string[] requiredSegments)
                {
                    Assert.Equal(JTokenType.Object, json.Type);
                    JObject jObj = (JObject)json;
                    Assert.True(jObj.TryGetValue(fieldName, out JToken fieldValue));
                    Assert.True(Uri.TryCreate((string)fieldValue, UriKind.Absolute, out Uri uri));
                    Assert.StartsWith(testNotificationUrl.GetLeftPart(UriPartial.Path), uri.GetLeftPart(UriPartial.Path));

                    if (fieldName != "baseUrl")
                    {
                        Assert.Contains(testNotificationUrl.Query.TrimStart('?'), uri.Query);
                    }

                    foreach (string segment in requiredSegments)
                    {
                        Assert.Contains(segment, uri.OriginalString);
                    }
                }

                string[] creationUrlParams = new[] { "{functionName}", "{instanceId}" };

                // Legacy payload validation
                Assert.True(outerJson.TryGetValue("creationUrls", out JToken creationUrls));
                CommonUriValidation(creationUrls, "createNewInstancePostUri", creationUrlParams);
                CommonUriValidation(creationUrls, "createAndWaitOnNewInstancePostUri", creationUrlParams);

                Assert.True(outerJson.TryGetValue("managementUrls", out JToken managementUrls));
                Assert.Equal(JTokenType.Object, managementUrls.Type);
                Assert.True(((JObject)managementUrls).TryGetValue("id", out JToken idValue));
                Assert.Equal(JTokenType.String, idValue.Type);

                string idPlaceholder = (string)idValue;
                string[] managementUrlParams = new[] { idPlaceholder };
                CommonUriValidation(managementUrls, "statusQueryGetUri", managementUrlParams);
                CommonUriValidation(managementUrls, "sendEventPostUri", managementUrlParams);
                CommonUriValidation(managementUrls, "terminatePostUri", managementUrlParams);
                CommonUriValidation(managementUrls, "rewindPostUri", managementUrlParams);
                CommonUriValidation(managementUrls, "purgeHistoryDeleteUri", managementUrlParams);

                CommonUriValidation(outerJson, "baseUrl", new string[0]);

                Assert.True(outerJson.TryGetValue("requiredQueryStringParameters", out JToken requiredQueryStringParameters));
                Assert.Equal(testNotificationUrl.Query.Trim('?'), (string)requiredQueryStringParameters);

                Assert.True(outerJson.TryGetValue("rpcBaseUrl", out JToken rpcBaseUrl));

                if (localRcpEnabled)
                {
                    Assert.True(Uri.TryCreate((string)rpcBaseUrl, UriKind.Absolute, out Uri rpcBaseUri));
                    Assert.True(rpcBaseUri.IsLoopback);
                    Assert.Equal("http", rpcBaseUri.Scheme);
                    Assert.Equal("/durabletask/", rpcBaseUri.AbsolutePath);
                }
                else
                {
                    Assert.Equal(JTokenType.Null, rpcBaseUrl.Type);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null, true, "http RPC")]
        [InlineData("node", true, "http RPC")]
        [InlineData("java", true, "gRPC")]
        [InlineData("powershell", true, "http RPC")]
        [InlineData("python", true, "http RPC")]
        [InlineData("dotnet", false, null)]
        [InlineData("dotnet-isolated", true, "gRPC")]
        [InlineData("custom", true, "gRPC")]
        public async Task TestLocalRcpEndpointRuntimeVersion(string runtimeVersion, bool enabledExpected, string expectedProtocol)
        {
            INameResolver nameResolver = new SimpleNameResolver(
                new Dictionary<string, string>
                {
                    { "FUNCTIONS_WORKER_RUNTIME", runtimeVersion },
                });

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TestLocalRcpEndpointRuntimeVersion),
                enableExtendedSessions: false,
                localRpcEndpointEnabled: null /* use FUNCTIONS_WORKER_RUNTIME to decide */,
                nameResolver: nameResolver))
            {
                await host.StartAsync();

                // Validate if we opened local RPC endpoint by looking at log statements.
                var logger = this.loggerProvider.CreatedLoggers.Single(l => l.Category == TestHelpers.LogCategory);
                var logMessages = logger.LogMessages.ToList();

                bool enabledRpcEndpoint = logMessages.Any(msg => msg.Level == Microsoft.Extensions.Logging.LogLevel.Information && msg.FormattedMessage.StartsWith($"Opened local {expectedProtocol} endpoint:"));

                Assert.Equal(enabledExpected, enabledRpcEndpoint);

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task InvokeLocalRpcEndpoint()
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.InvokeLocalRpcEndpoint),
                enableExtendedSessions: false,
                localRpcEndpointEnabled: true,
                notificationUrl: null))
            {
                await host.StartAsync();

#pragma warning disable SYSLIB0014 // Type or member is obsolete
                using (var client = new WebClient())
                {
                    string jsonString = client.DownloadString("http://localhost:17071/durabletask/instances");

                    // The result is expected to be an empty array
                    JArray array = JArray.Parse(jsonString);
                }
#pragma warning restore SYSLIB0014 // Type or member is obsolete

                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData("dotnet")]
        [InlineData("DotNet")]
        [InlineData("node")]
        [InlineData("Node")]
        [InlineData("python")]
        [InlineData("Python")]
        [InlineData("powershell")]
        [InlineData("PowerShell")]
        [InlineData("haskell")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void WorkerRuntimeTypeFollowsSpec(string workerRuntime)
        {
            INameResolver nameResolver = new SimpleNameResolver(
                new Dictionary<string, string>
                {
                    { "FUNCTIONS_WORKER_RUNTIME", workerRuntime },
                });

            ILoggerFactory loggerFactory = new LoggerFactory();
            var platformInformation = new DefaultPlatformInformation(nameResolver, loggerFactory);

            var runtimeType = platformInformation.GetWorkerRuntimeType();

            var supportedPLs = (IEnumerable<string>)Enum.GetNames(typeof(WorkerRuntimeType));
            if (!supportedPLs.Contains(workerRuntime, StringComparer.OrdinalIgnoreCase))
            {
                // Unknown runtimes should return Unknown
                Assert.Equal(WorkerRuntimeType.Unknown, runtimeType);
            }
            else
            {
                // Known runtimes should be parseable regardless of capitalization
                Assert.NotEqual(WorkerRuntimeType.Unknown, runtimeType);
                runtimeType.ToString().Equals(workerRuntime, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [InlineData(false, "2.0", null, "MySubOrchestrator\n2.0")] // Explicit version
        [InlineData(false, null, null, "MySubOrchestrator")] // Null version - no delimiter
        [InlineData(false, "", null, "MySubOrchestrator\n")] // Empty version - delimiter included
        [InlineData(false, "1.0.0", null, "MySubOrchestrator\n1.0.0")] // Semantic version
        [InlineData(false, "4.5.6-preview", null, "MySubOrchestrator\n4.5.6-preview")] // Pre-release version
        [InlineData(false, "2.0-beta.1", null, "MySubOrchestrator\n2.0-beta.1")] // Beta version
        [InlineData(false, "v1.2.3", null, "MySubOrchestrator\nv1.2.3")] // Version with prefix
        [InlineData(true, "3.5.1", null, "MySubOrchestrator\n3.5.1")] // Explicit version with retry
        [InlineData(true, null, null, "MySubOrchestrator")] // Null version with retry
        [InlineData(false, "5.0", "V2", "MySubOrchestrator\n5.0")] // Schema V2
        [InlineData(false, "5.0", "V3", "MySubOrchestrator\n5.0")] // Schema V3
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestrator_VersionHandling_OutOfProc(bool withRetry, string version, string schemaVersion, string expectedFunctionName)
        {
            string capturedFunctionName = null;
            RetryOptions capturedRetryOptions = null;

            // Mock the CallSubOrchestratorAsync or CallSubOrchestratorWithRetryAsync API
            var contextMock = new Mock<IDurableOrchestrationContext>();

            if (withRetry)
            {
                contextMock
                    .Setup(ctx => ctx.CallSubOrchestratorWithRetryAsync(
                        It.IsAny<string>(),
                        It.IsAny<RetryOptions>(),
                        It.IsAny<string>(),
                        It.IsAny<object>()))
                    .Callback<string, RetryOptions, string, object>((name, retry, instanceId, input) =>
                    {
                        capturedFunctionName = name;
                        capturedRetryOptions = retry;
                    })
                    .Returns(Task.CompletedTask);
            }
            else
            {
                contextMock
                    .Setup(ctx => ctx.CallSubOrchestratorAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<object>()))
                    .Callback<string, string, object>((name, instanceId, input) =>
                        capturedFunctionName = name)
                    .Returns(Task.CompletedTask);
            }

            var shim = new OutOfProcOrchestrationShim(contextMock.Object);

            var actionType = withRetry ? "CallSubOrchestratorWithRetry" : "CallSubOrchestrator";
            var versionField = version == null ? string.Empty : $@"""version"": ""{version}"",";
            var schemaVersionField = schemaVersion == null ? string.Empty : $@"""schemaVersion"": ""{schemaVersion}"",";
            var retryField = withRetry ? @"""retryOptions"": {
                ""firstRetryIntervalInMilliseconds"": 1000,
                ""maxNumberOfAttempts"": 3
            }," : string.Empty;

            var executionJson = $@"
{{
    ""isDone"": false,
    {schemaVersionField}
    ""actions"": [
        [{{
            ""actionType"": ""{actionType}"",
            ""functionName"": ""MySubOrchestrator"",
            {versionField}
            ""instanceId"": ""test-instance"",
            {retryField}
            ""input"": null
        }}]
    ]
}}";

            var jsonObject = JObject.Parse(executionJson);
            OrchestrationInvocationResult result = new OrchestrationInvocationResult(jsonObject);
            bool moreWork = await shim.ScheduleDurableTaskEvents(result);

            Assert.True(moreWork);
            Assert.NotNull(capturedFunctionName);
            Assert.Equal(expectedFunctionName, capturedFunctionName);

            if (withRetry)
            {
                Assert.NotNull(capturedRetryOptions);
                Assert.Equal(3, capturedRetryOptions.MaxNumberOfAttempts);
            }
        }
    }
}
