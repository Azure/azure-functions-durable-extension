// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
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

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        private async Task ActivityTriggerAsJObject(string storageProviderType)
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.ActivityTriggerAsJObject), false, storageProviderType))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToJObject);
                startArgs.Input = new { Foo = "Bar" };

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                // The function checks to see if there is a property called "Foo" which is set to a value
                // called "Bar" and returns true if this is the case. Otherwise returns false.
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.True((bool)status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActivityTriggerAsPOCO(string storageProviderType)
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.ActivityTriggerAsPOCO), false, storageProviderType))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToPOCO);

                var input = new { Foo = "Bar" };
                startArgs.Input = input;

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                // The function echos back the 'Foo' input property value
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(input.Foo, status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActivityTriggerAsNumber(string storageProviderType)
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.ActivityTriggerAsNumber), false, storageProviderType))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToDouble);
                startArgs.Input = 3.14;

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                // The function echos back the input value
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal((double)startArgs.Input, status?.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BindToBlobViaParameterName(string storageProviderType)
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.BindToBlobViaParameterName), false, storageProviderType))
            {
                await host.StartAsync();

                string connectionString = TestHelpers.GetStorageConnectionString();
                CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
                this.output.WriteLine($"Using storage account: {account.Credentials.AccountName}");

                // Blob and container names need to be kept in sync with the activity code.
                const string OriginalBlobName = "MyBlob";
                const string UpdatedBlobName = OriginalBlobName + "-archive";
                const string ContainerName = "test";

                CloudBlobClient blobClient = account.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
                if (await container.CreateIfNotExistsAsync())
                {
                    this.output.WriteLine($"Created container '{container.Name}'.");
                }

                string randomData = Guid.NewGuid().ToString("N");

                CloudBlockBlob blob = container.GetBlockBlobReference(OriginalBlobName);
                await blob.UploadTextAsync(randomData);
                this.output.WriteLine($"Uploaded text '{randomData}' to {blob.Name}.");

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToBlobViaParameterName);
                startArgs.Input = OriginalBlobName;

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                CloudBlockBlob newBlob = container.GetBlockBlobReference(UpdatedBlobName);
                string copiedData = await newBlob.DownloadTextAsync();
                this.output.WriteLine($"Downloaded text '{copiedData}' from {newBlob.Name}.");

                Assert.Equal(randomData, copiedData);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BindToBlobViaPOCO(string storageProviderType)
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.BindToBlobViaPOCO), false, storageProviderType))
            {
                await host.StartAsync();

                string connectionString = TestHelpers.GetStorageConnectionString();
                CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
                this.output.WriteLine($"Using storage account: {account.Credentials.AccountName}");

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

                CloudBlobClient blobClient = account.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
                if (await container.CreateIfNotExistsAsync())
                {
                    this.output.WriteLine($"Created container '{container.Name}'.");
                }

                string randomData = Guid.NewGuid().ToString("N");

                this.output.WriteLine($"Creating blob named {outputBlobName}...");
                CloudBlockBlob blob = container.GetBlockBlobReference(inputBlobName);
                await blob.UploadTextAsync(randomData);
                this.output.WriteLine($"Uploaded text '{randomData}' to {blob.Name}.");

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToBlobViaJsonPayload);
                startArgs.Input = data;

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                this.output.WriteLine($"Searching for blob named {outputBlobName}...");
                CloudBlockBlob newBlob = container.GetBlockBlobReference(outputBlobName);
                string copiedData = await newBlob.DownloadTextAsync();
                this.output.WriteLine($"Downloaded text '{copiedData}' from {newBlob.Name}.");

                Assert.Equal(randomData, copiedData);

                await host.StopAsync();
            }
        }
    }
}