// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;

namespace DFTestBot
{
    class TestParameters
    {
        /// <summary>
        /// Gets or sets the subscription ID to deploy the test app into.
        /// </summary>
        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Gets or sets the resource group to deploy the test app into.
        /// </summary>
        [JsonProperty("resourceGroup")]
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Gets or sets the storage account for the app.
        /// </summary>
        [JsonProperty("storageAccount")]
        public string StorageAccount { get; set; }

        /// <summary>
        /// Gets or sets the name of the app to deploy.
        /// </summary>
        [JsonProperty("appName")]
        public string AppName { get; set; }

        /// <summary>
        /// Gets or sets the name of the app plan to deploy.
        /// </summary>
        [JsonProperty("appPlanName")]
        public string AppPlanName { get; set; }

        /// <summary>
        /// Gets or sets the name of the test to run.
        /// </summary>
        [JsonProperty("testName")]
        public string TestName { get; set; }

        /// <summary>
        /// Gets or sets parameters for running the test.
        /// </summary>
        [JsonProperty("testParameters")]
        public string Parameters { get; set; }

        /// <summary>
        /// Gets or sets the name of the AppLens detector to use for analyzing results.
        /// </summary>
        [JsonProperty("detectorName")]
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets or sets the GitHub pull request comment API URL e.g. https://api.github.com/repos/Azure/azure-functions-durable-extension/issues/1464/comments.
        /// </summary>
        [JsonProperty("gitHubCommentApiUrl")]
        public Uri GitHubCommentApiUrl { get; set; }

        /// <summary>
        /// Gets or sets the GitHub pull request comment API URL e.g. https://api.github.com/repos/bachuv/azure-functions-durable-extension/issues/comments/724391328.
        /// </summary>
        [JsonProperty("gitHubCommentIdApiUrl")]
        public Uri GitHubCommentIdApiUrl { get; set; }

        /// <summary>
        /// Gets or sets the GitHub comment action e.g "created" or "edited"
        /// </summary>
        [JsonProperty("gitHubCommentAction")]
        public string GitHubCommentAction { get; set; }

        /// <summary>
        /// Gets or sets the GitHub branch to build e.g. "cgillum/perf-testing"
        /// </summary>
        [JsonProperty("branchName")]
        public string GitHubBranch { get; set; }

        /// <summary>
        /// Gets or sets the app plan type (e.g. Elastic Premium, Consumption).
        /// </summary>
        [JsonProperty("appPlanType")]
        public string AppPlanType { get; set; }

        /// <summary>
        /// Gets or sets the sku (e.g. EP1, EP2, EP3).
        /// </summary>
        [JsonProperty("sku")]
        public string Sku { get; set; }

        /// <summary>
        /// Gets or sets the minimum instance count to scale in
        /// </summary>
        [JsonProperty("minInstanceCount")]
        public string MinInstanceCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum instance count to scale out
        /// </summary>
        [JsonProperty("maxInstanceCount")]
        public string MaxInstanceCount { get; set; }

        /// <summary>
        /// Gets or sets the OS Type for the function to run on (e.g. Windows, Linux)
        /// </summary>
        [JsonProperty("OSType")]
        public string OSType { get; set; }

        /// <summary>
        /// Gets or sets the functions version
        /// </summary>
        [JsonProperty("functionsVersion")]
        public string FunctionsVersion { get; set; }
    }
}
