// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Scale;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    public static class TestHelpers
    {
        public static string GetStorageConnectionString()
        {
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (string.IsNullOrEmpty(storageConnectionString))
            {
                storageConnectionString = "UseDevelopmentStorage=true";
            }

            return storageConnectionString;
        }

        public static string GetSqlConnectionString()
        {
            string sqlConnectionString = Environment.GetEnvironmentVariable("SQLDB_Connection");

            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                return sqlConnectionString;
            }

            throw new InvalidOperationException(
                "SQL connection string not found in environment variables.");
        }

        public static string GetAzureManagedConnectionString()
        {
            string connectionString = Environment.GetEnvironmentVariable("DURABLE_TASK_SCHEDULER_CONNECTION_STRING");

            if (!string.IsNullOrEmpty(connectionString))
            {
                return connectionString;
            }

            throw new InvalidOperationException(
                "Azure Managed connection string not found in environment variables.");
        }

        public static string GetNetheriteEventHubsConnectionString()
        {
            string connectionString = Environment.GetEnvironmentVariable("EventHubsConnection");

            if (!string.IsNullOrEmpty(connectionString))
            {
                return connectionString;
            }

            throw new InvalidOperationException(
                "Event Hubs connection string not found in environment variables. " +
                "Set the 'EventHubsConnection' environment variable (e.g. to the Event Hubs emulator connection string).");
        }

        /// <summary>
        /// Creates a TriggerMetadata object for testing with the specified storage provider type.
        /// </summary>
        /// <param name="hubName">The task hub name.</param>
        /// <param name="maxOrchestrator">Maximum concurrent orchestrator functions.</param>
        /// <param name="maxActivity">Maximum concurrent activity functions.</param>
        /// <param name="connectionName">The connection name for the storage provider.</param>
        /// <param name="storageType">The storage provider type (e.g., "mssql", "azureManaged", "AzureStorage").</param>
        /// <param name="triggerType">The trigger type (e.g., "orchestrationTrigger", "activityTrigger"). Defaults to "orchestrationTrigger".</param>
        /// <returns>A TriggerMetadata instance configured for testing.</returns>
        public static TriggerMetadata CreateTriggerMetadata(
            string hubName,
            int maxOrchestrator,
            int maxActivity,
            string connectionName,
            string storageType,
            string triggerType = "orchestrationTrigger")
        {
            var metadata = new JObject
            {
                { "functionName", "TestFunction" },
                { "type", triggerType },
                { "taskHubName", hubName },
                { "maxConcurrentOrchestratorFunctions", maxOrchestrator },
                { "maxConcurrentActivityFunctions", maxActivity },
                {
                    "storageProvider", new JObject
                    {
                        { "type", storageType },
                        { "connectionName", connectionName },
                    }
                },
            };

            return new TriggerMetadata(metadata);
        }
    }
}
