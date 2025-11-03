// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    /// <summary>
    /// Helper methods for SQL Server tests.
    /// Provides utilities for checking SQL connectivity and creating test services.
    /// </summary>
    internal static class SqlServerTestHelpers
    {
        /// <summary>
        /// Checks if SQL Server is available by attempting a connection.
        /// </summary>
        /// <returns>True if SQL Server is available, false otherwise.</returns>
        public static bool IsSqlServerAvailable()
        {
            try
            {
                var connectionString = TestHelpers.GetSqlConnectionString();
                using (var connection = new SqlConnection(connectionString))
                {
                    // Try to open connection with short timeout
                    var task = connection.OpenAsync();
                    if (task.Wait(TimeSpan.FromSeconds(5)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Connection failed - SQL Server not available
            }

            return false;
        }

        /// <summary>
        /// Creates a SqlOrchestrationService for testing.
        /// Returns null if SQL Server is not available.
        /// </summary>
        public static SqlOrchestrationService CreateSqlOrchestrationService(string hubName = "testHub")
        {
            try
            {
                var connectionString = TestHelpers.GetSqlConnectionString();
                var settings = new SqlOrchestrationServiceSettings(connectionString, hubName, schemaName: null);
                return new SqlOrchestrationService(settings);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a skip reason if SQL Server is not available.
        /// </summary>
        public static string GetSkipReason()
        {
            return "SQL Server is not available. Set SQLDB_Connection, SQLDB_Connection_Azure, or SQLDB_Connection_Local environment variable, or ensure Docker SQL Server is running on localhost:1433.";
        }
    }
}
