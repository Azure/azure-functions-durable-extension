// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableHttpClientFactory
    {
        private HttpClient durableHttpClient;

        public HttpClient GetClient(IDurableHttpMessageHandlerFactory handler)
        {
            if (this.durableHttpClient == null)
            {
                this.durableHttpClient = new HttpClient(handler.CreateHttpMessageHandler());
                this.durableHttpClient.Timeout = TimeSpan.FromSeconds(240);

                var assembly = typeof(DurableTaskExtension).Assembly;
                Version assemblyVersion = GetAssemblyVersion(assembly);

                this.durableHttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                    assembly.GetName().Name,
                    assemblyVersion.ToString()));
            }

            return this.durableHttpClient;
        }

        private static Version GetAssemblyVersion(Assembly assembly)
        {
            var assemblyInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var assemblyVersion = new Version(
                assemblyInfo.FileMajorPart,
                assemblyInfo.FileMinorPart,
                assemblyInfo.FileBuildPart);

            return assemblyVersion;
        }
    }
}
