// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestActivities
    {
        public static string Hello([ActivityTrigger] DurableActivityContext ctx)
        {
            string input = ctx.GetInput<string>();
            return $"Hello, {input}!";
        }

        public static long Multiply([ActivityTrigger] DurableActivityContext ctx)
        {
            long[] values = ctx.GetInput<long[]>();
            return values[0] * values[1];
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

        public static void Throw([ActivityTrigger] DurableActivityContext ctx)
        {
            string message = ctx.GetInput<string>();
            throw new Exception(message);
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

        public class PlainOldClrObject
        {
            public string Foo { get; set; }
        }
    }
}
