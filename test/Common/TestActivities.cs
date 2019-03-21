// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestActivities
    {
        public const char BigValueChar = '*';

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
            throw new Exception(message);
        }

        public static Task<string> ReadStringFromTextFile([ActivityTrigger] IDurableActivityContext ctx)
        {
            string fileName = ctx.GetInput<string>();
            using (StreamReader streamReader = new StreamReader(fileName, Encoding.UTF8))
            {
                return streamReader.ReadToEndAsync();
            }
        }

        public static Task WriteStringToTextFile([ActivityTrigger] IDurableActivityContext ctx)
        {
            var input = ctx.GetInput<string[]>();
            var fileName = input[0];
            var content = input[1];
            using (var fileStream = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                using (var streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    return streamWriter.WriteAsync(content);
                }
            }
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
            [ActivityTrigger] IDurableActivityContext ctx,
            [OrchestrationClient] IDurableOrchestrationClient client)
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

        public class PlainOldClrObject
        {
            public string Foo { get; set; }
        }
    }
}
