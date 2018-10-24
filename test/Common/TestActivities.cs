// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestActivities
    {
        public const char BigValueChar = '*';

        public static string Hello([ActivityTrigger] DurableActivityContext ctx)
        {
            string input = ctx.GetInput<string>();
            return $"Hello, {input}!";
        }

        public static object Echo([ActivityTrigger] DurableActivityContext ctx)
        {
            object obj = ctx.GetInput<object>();
            return obj;
        }

        public static long Multiply([ActivityTrigger] DurableActivityContext ctx)
        {
            (long a, long b) = ctx.GetInput<(long, long)>();
            return a * b;
        }

        public static string[] GetFileList([ActivityTrigger] DurableActivityContext ctx)
        {
            string directory = ctx.GetInput<string>();
            string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            return files;
        }

        public static long GetFileSize([ActivityTrigger] DurableActivityContext ctx)
        {
            string fileName = ctx.GetInput<string>();
            var info = new FileInfo(fileName);
            return info.Length;
        }

        public static void ThrowActivity([ActivityTrigger] DurableActivityContext ctx)
        {
            string message = ctx.GetInput<string>();
            throw new Exception(message);
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
            [ActivityTrigger] DurableActivityContext ctx,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            HttpManagementPayload httpManagementPayload = client.CreateHttpManagementPayload(ctx.InstanceId);
            return httpManagementPayload;
        }

        public static DurableOrchestrationStatus UpdateDurableOrchestrationStatus([ActivityTrigger] DurableActivityContext ctx)
        {
            DurableOrchestrationStatus durableOrchestrationStatus = ctx.GetInput<DurableOrchestrationStatus>();
            durableOrchestrationStatus.RuntimeStatus = OrchestrationRuntimeStatus.Completed;
            durableOrchestrationStatus.CreatedTime = DateTime.UtcNow;
            durableOrchestrationStatus.LastUpdatedTime = DateTime.UtcNow.AddSeconds(5);
            return durableOrchestrationStatus;
        }

        public static Guid NewGuid([ActivityTrigger] DurableActivityContext ctx)
        {
            return Guid.NewGuid();
        }

        public class PlainOldClrObject
        {
            public string Foo { get; set; }
        }
    }
}
