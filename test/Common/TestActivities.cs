// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.DurableTaskEndToEndTests;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestActivities
    {
        public const char BigValueChar = '*';

        public static ComplexType ComplexTypeActivity([ActivityTrigger] IDurableActivityContext ctx)
        {
            var input = ctx.GetInput<ComplexType>();
            return input;
        }

        public static string ActivityWithNoInput([ActivityTrigger] IDurableActivityContext ctx)
        {
            return $"Hello!";
        }

        public static string Hello([ActivityTrigger] IDurableActivityContext ctx)
        {
            string input = ctx.GetInput<string>();
            return $"Hello, {input}!";
        }

        public static object Echo([ActivityTrigger] IDurableActivityContext ctx)
        {
            object obj = ctx.GetInput<object>();
            return obj;
        }

        public static long Multiply([ActivityTrigger] IDurableActivityContext ctx)
        {
            (long a, long b) = ctx.GetInput<(long, long)>();
            return a * b;
        }

        public static long Add([ActivityTrigger] IDurableActivityContext ctx)
        {
            (long a, long b) = ctx.GetInput<(long, long)>();
            return a + b;
        }

        public static string[] GetFileList([ActivityTrigger] IDurableActivityContext ctx)
        {
            string directory = ctx.GetInput<string>();
            string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            return files;
        }

        public static long GetFileSize([ActivityTrigger] IDurableActivityContext ctx)
        {
            string fileName = ctx.GetInput<string>();
            var info = new FileInfo(fileName);
            return info.Length;
        }

        public static void ThrowActivity([ActivityTrigger] IDurableActivityContext ctx)
        {
            string message = ctx.GetInput<string>();
            throw new InvalidOperationException(message);
        }

        public static async Task TimeDelayActivity([ActivityTrigger] IDurableActivityContext ctx)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        public static Task<string> LoadStringFromTextBlob(
            [ActivityTrigger] string blobName)
        {
            return TestHelpers.LoadStringFromTextBlobAsync(blobName);
        }

        public static Task WriteStringToTextBlob(
           [ActivityTrigger](string blobName, string content) input)
        {
            return TestHelpers.WriteStringToTextBlob(input.blobName, input.content);
        }

        public static void DeleteTextFile([ActivityTrigger] IDurableActivityContext ctx)
        {
            var filename = ctx.GetInput<string>();
            var info = new FileInfo(filename);
            info.Delete(); // would prefer async but it does not seem to exist
        }

        public static string BigReturnValue([ActivityTrigger] int stringLength)
        {
            return new string(BigValueChar, stringLength);
        }

        public static bool BindToJObject([ActivityTrigger] JObject jObj)
        {
            return string.Equals((string)jObj["Foo"], "Bar", StringComparison.Ordinal);
        }

        public static string BindToPOCO([ActivityTrigger] PlainOldClrObject poco)
        {
            return poco.Foo;
        }

        // Mark as no automatic trigger to allow usage of additional parameters not associated with bindings (i.e. outputWrapper).
        [NoAutomaticTrigger]
        public static void BindToPOCOWithOutParameter([ActivityTrigger] PlainOldClrObject poco, string[] outputWrapper)
        {
            outputWrapper[0] = poco.Foo;
        }

        public static double BindToDouble([ActivityTrigger] double value)
        {
            return value;
        }

        public static async Task BindToBlobViaParameterName(
            [ActivityTrigger] string name,
            [Blob("test/{name}", FileAccess.Read)] Stream input,
            [Blob("test/{name}-archive", FileAccess.Write)] Stream output)
        {
            await input.CopyToAsync(output);
        }

        public static async Task BindToBlobViaJsonPayload(
            [ActivityTrigger] JObject ignored,
            [Blob("test/{data.InputPrefix}-{data.Suffix}", FileAccess.Read)] Stream input,
            [Blob("test/{data.OutputPrefix}-{data.Suffix}", FileAccess.Write)] Stream output)
        {
            await input.CopyToAsync(output);
        }

        public static HttpManagementPayload GetAndReturnHttpManagementPayload(
            [ActivityTrigger] IDurableActivityContext ctx,
            [DurableClient] IDurableOrchestrationClient client)
        {
            HttpManagementPayload httpManagementPayload = client.CreateHttpManagementPayload(ctx.InstanceId);
            return httpManagementPayload;
        }

        public static DurableOrchestrationStatus UpdateDurableOrchestrationStatus([ActivityTrigger] IDurableActivityContext ctx)
        {
            DurableOrchestrationStatus durableOrchestrationStatus = ctx.GetInput<DurableOrchestrationStatus>();
            durableOrchestrationStatus.RuntimeStatus = OrchestrationRuntimeStatus.Completed;
            durableOrchestrationStatus.CreatedTime = DateTime.UtcNow;
            durableOrchestrationStatus.LastUpdatedTime = DateTime.UtcNow.AddSeconds(5);
            return durableOrchestrationStatus;
        }

        public static Guid NewGuid([ActivityTrigger] IDurableActivityContext ctx)
        {
            return Guid.NewGuid();
        }

#pragma warning disable 618
        public static Task<string> LegacyActivity([ActivityTrigger] DurableActivityContextBase ctx)
        {
            return Task.FromResult("ok");
        }
#pragma warning restore 618

        public class PlainOldClrObject
        {
            public string Foo { get; set; }
        }
    }
}
