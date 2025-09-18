// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class BindingTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public BindingTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        /// <summary>
        /// Tests DurableClient attribute binds a client instance with the IDurableOrchestrationClient interface.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task IDurableOrchestrationClientBinding()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.IDurableOrchestrationClientBinding),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                IDurableOrchestrationClient client = await host.GetOrchestrationClientBindingTest(this.output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Tests DurableClient attribute binds a client instance with the IDurableEntityClient interface.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task IDurableEntityClientBinding()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.IDurableEntityClientBinding),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                IDurableEntityClient client = await host.GetEntityClientBindingTest(this.output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Tests OrchestrationClient attribute binds a client instance with the IDurableOrchestrationClient interface.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task IDurableOrchestrationClientBindingBackComp()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.IDurableOrchestrationClientBinding),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                var startFunction = typeof(ClientFunctions)
                    .GetMethod(nameof(ClientFunctions.GetOrchestrationClientBindingBackCompTest));

                var clientRef = new IDurableOrchestrationClient[1];
                var args = new Dictionary<string, object>
                {
                    { "clientRef", clientRef },
                };

                await host.CallAsync(startFunction, args);
                IDurableOrchestrationClient client = clientRef[0];

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Tests OrchestrationClient attribute binds a client instance with the IDurableEntityClient interface.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task IDurableEntityClientBindingBackComp()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.IDurableEntityClientBindingBackComp),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                var startFunction = typeof(ClientFunctions)
                    .GetMethod(nameof(ClientFunctions.GetEntityClientBindingBackCompTest));
                var clientRef = new IDurableEntityClient[1];
                var args = new Dictionary<string, object>
                {
                    { "clientRef", clientRef },
                };
                await host.CallAsync(startFunction, args);
                IDurableEntityClient client = clientRef[0];

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        private async Task ActivityTriggerAsJObject(string storageProviderType)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.ActivityTriggerAsJObject), false, storageProviderType: storageProviderType))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToJObject);
                startArgs.Input = new { Foo = "Bar" };

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                // The function checks to see if there is a property called "Foo" which is set to a value
                // called "Bar" and returns true if this is the case. Otherwise returns false.
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.True((bool)status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActivityTriggerAsPOCO(string storageProviderType)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.ActivityTriggerAsPOCO), false, storageProviderType: storageProviderType))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToPOCO);

                var input = new { Foo = "Bar" };
                startArgs.Input = input;

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                // The function echos back the 'Foo' input property value
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(input.Foo, status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActivityTriggerAsNumber(string storageProviderType)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.ActivityTriggerAsNumber), false, storageProviderType: storageProviderType))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToDouble);
                startArgs.Input = 3.14;

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                // The function echos back the input value
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal((double)startArgs.Input, status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BindToBlobViaParameterName(string storageProviderType)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.BindToBlobViaParameterName), false, storageProviderType: storageProviderType))
            {
                await host.StartAsync();

                string connectionString = TestHelpers.GetStorageConnectionString();
                var blobServiceClient = new BlobServiceClient(connectionString);
                this.output.WriteLine($"Using storage account: {blobServiceClient.AccountName}");

                // Blob and container names need to be kept in sync with the activity code.
                const string OriginalBlobName = "MyBlob";
                const string UpdatedBlobName = OriginalBlobName + "-archive";
                const string ContainerName = "test";

                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
                if (await containerClient.CreateIfNotExistsAsync() != null)
                {
                    this.output.WriteLine($"Created container '{containerClient.Name}'.");
                }

                string randomData = Guid.NewGuid().ToString("N");

                using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes(randomData)))
                {
                    BlockBlobClient blob = containerClient.GetBlockBlobClient(OriginalBlobName);
                    await blob.UploadAsync(buffer);
                    this.output.WriteLine($"Uploaded text '{randomData}' to {blob.Name}.");
                }

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToBlobViaParameterName);
                startArgs.Input = OriginalBlobName;

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                BlockBlobClient newBlob = containerClient.GetBlockBlobClient(UpdatedBlobName);
                BlobDownloadResult result = await newBlob.DownloadContentAsync();
                string copiedData = result.Content.ToString();
                this.output.WriteLine($"Downloaded text '{copiedData}' from {newBlob.Name}.");

                Assert.Equal(randomData, copiedData);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BindToBlobViaPOCO(string storageProviderType)
        {
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.BindToBlobViaPOCO), false, storageProviderType: storageProviderType))
            {
                await host.StartAsync();

                string connectionString = TestHelpers.GetStorageConnectionString();
                var blobServiceClient = new BlobServiceClient(connectionString);
                this.output.WriteLine($"Using storage account: {blobServiceClient.AccountName}");

                // Blob and container names need to be kept in sync with the activity code.
                var data = new
                {
                    InputPrefix = "Input",
                    OutputPrefix = "Output",
                    Suffix = 42,
                };

                const string ContainerName = "test";
                string inputBlobName = $"{data.InputPrefix}-{data.Suffix}";
                string outputBlobName = $"{data.OutputPrefix}-{data.Suffix}";

                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
                if (await containerClient.CreateIfNotExistsAsync() != null)
                {
                    this.output.WriteLine($"Created container '{containerClient.Name}'.");
                }

                string randomData = Guid.NewGuid().ToString("N");
                using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes(randomData)))
                {
                    this.output.WriteLine($"Creating blob named {outputBlobName}...");
                    BlockBlobClient blob = containerClient.GetBlockBlobClient(inputBlobName);
                    await blob.UploadAsync(buffer);
                    this.output.WriteLine($"Uploaded text '{randomData}' to {blob.Name}.");
                }

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToBlobViaJsonPayload);
                startArgs.Input = data;

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                this.output.WriteLine($"Searching for blob named {outputBlobName}...");
                BlockBlobClient newBlob = containerClient.GetBlockBlobClient(outputBlobName);
                BlobDownloadResult result = await newBlob.DownloadContentAsync();
                string copiedData = result.Content.ToString();
                this.output.WriteLine($"Downloaded text '{copiedData}' from {newBlob.Name}.");

                Assert.Equal(randomData, copiedData);

                await host.StopAsync();
            }
        }
    }
}