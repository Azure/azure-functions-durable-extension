// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
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
            // Priority 1: Use DTMB_SQL_CONNECTION_STRING environment variable if set
            // This is the standard environment variable name used for SQL connection
            string? sqlConnectionString = Environment.GetEnvironmentVariable("DTMB_SQL_CONNECTION_STRING");

            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                return sqlConnectionString;
            }

            // Priority 2: Use SQLDB_Connection environment variable if set
            // This is the standard environment variable name used by the extension and CI pipeline
            sqlConnectionString = Environment.GetEnvironmentVariable("SQLDB_Connection");

            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                return sqlConnectionString;
            }

            // If no environment variable is set, throw an exception to ensure tests verify that
            // the package correctly reads connection strings from configuration/environment variables.
            // This prevents tests from silently using a hardcoded default that doesn't match the actual environment.
            throw new InvalidOperationException(
                "SQL connection string not found in environment variables.");
        }
    }
}
