// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Diagnostics.Tracing;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Base class for E2E test classes. Provides shared test infrastructure including
    /// logging, ETW trace capture, and common utilities.
    /// Splitting test methods across multiple derived classes enables xUnit to run
    /// them in parallel (different classes = different implicit collections).
    /// </summary>
    public abstract class DurableTaskEndToEndTestBase : IDisposable
    {
#pragma warning disable SA1401 // Fields should be private - protected for derived test classes
        protected readonly ITestOutputHelper output;
        private protected readonly TestLoggerProvider loggerProvider;
        protected readonly bool useTestLogger = IsLogFriendlyPlatform();
#pragma warning restore SA1401

        private readonly LogEventTraceListener eventSourceListener;

        protected DurableTaskEndToEndTestBase(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
            this.eventSourceListener = new LogEventTraceListener();
            this.StartLogCapture();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.eventSourceListener.Dispose();
            }
        }

        // ETW logging is Windows-only.
        protected static bool IsLogFriendlyPlatform()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        // Starts ETW log capture when supported.
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

                this.eventSourceListener.CaptureLogs(traceConfig);
            }
        }

        private void OnEventSourceListenerTraceLog(object sender, LogEventTraceListener.TraceLogEventArgs e)
        {
            this.output.WriteLine($"      ETW: {e.ProviderName} [{e.Level}] : {e.Message}");
        }

        protected static async Task ValidateBlobUrlAsync(string taskHubName, string instanceId, string value)
        {
            var blobServiceClient = new BlobServiceClient(TestHelpers.GetStorageConnectionString());
            Assert.StartsWith(blobServiceClient.Uri.OriginalString, value);
            Assert.Contains("/" + instanceId + "/", value);
            Assert.EndsWith(".json.gz", value);

            string containerName = $"{taskHubName.ToLowerInvariant()}-largemessages";
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            Assert.True(await containerClient.ExistsAsync(), $"Blob container {containerName} is expected to exist.");

            string blobName = value.Split('/').Last();
            BlobClient blob = containerClient.GetBlobClient(instanceId + "/" + blobName);
            Assert.True(await blob.ExistsAsync(), $"Blob named {blob.Uri} is expected to exist.");
        }
    }
}
