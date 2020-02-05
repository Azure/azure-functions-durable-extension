// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

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

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BindToDurableClientAsString(bool localRcpEnabled)
        {
            Uri testNotificationUrl = new Uri("https://durable.edu/runtime/webhooks/durabletask?code=abcdefg");

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.BindToDurableClientAsString),
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
        [InlineData(null, true)]
        [InlineData("node", true)]
        [InlineData("java", true)]
        [InlineData("powershell", true)]
        [InlineData("python", true)]
        [InlineData("dotnet", false)]
        public async Task TestLocalRcpEndpointRuntimeVersion(string runtimeVersion, bool enabledExpected)
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

                // Check to see whether the local RPC endpoint has been opened
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] endpoints = ipGlobalProperties.GetActiveTcpListeners();

                const string LocalRcpAddress = "127.0.0.1:17071";
                if (enabledExpected)
                {
                    Assert.Contains(LocalRcpAddress, endpoints.Select(ep => ep.ToString()));
                }
                else
                {
                    Assert.DoesNotContain(LocalRcpAddress, endpoints.Select(ep => ep.ToString()));
                }

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

                using (var client = new WebClient())
                {
                    string jsonString = client.DownloadString("http://localhost:17071/durabletask/instances");

                    // The result is expected to be an empty array
                    JArray array = JArray.Parse(jsonString);
                }

                await host.StopAsync();
            }
        }
    }
}
