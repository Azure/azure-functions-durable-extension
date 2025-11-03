// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
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
            string sqlConnectionString = Environment.GetEnvironmentVariable("DTMB_SQL_CONNECTION_STRING");
            
            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                return sqlConnectionString;
            }

            // Priority 2: Use SQLDB_Connection environment variable if set
            // This is the standard environment variable name used by the extension
            sqlConnectionString = Environment.GetEnvironmentVariable("SQLDB_Connection");
            
            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                return sqlConnectionString;
            }

            // Priority 3: Use Azure SQL Database connection string (for local testing with Azure SQL)
            // Example: Server=tcp:mysqlservertny.database.windows.net,1433;Initial Catalog=testsqlscaling;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication="Active Directory Default";
            sqlConnectionString = Environment.GetEnvironmentVariable("SQLDB_Connection_Azure");
            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                return sqlConnectionString;
            }

            // Priority 4: Use Docker/local SQL Server connection string (for CI)
            // CI environments typically set up SQL Server in Docker
            // Example for Docker: Server=localhost,1433;Database=TestDurableDB;User Id=sa;Password=Strong!Passw0rd;TrustServerCertificate=True;Encrypt=False;
            sqlConnectionString = Environment.GetEnvironmentVariable("SQLDB_Connection_Local");
            if (!string.IsNullOrEmpty(sqlConnectionString))
            {
                return sqlConnectionString;
            }

            // Default: Try Docker SQL Server (common in CI)
            // This assumes SQL Server is running in Docker with default settings
            // For CI: Docker typically runs SQL Server on localhost:1433
            sqlConnectionString = "Server=localhost,1433;Database=TestDurableDB;User Id=sa;Password=Strong!Passw0rd;TrustServerCertificate=True;Encrypt=False;";
            
            return sqlConnectionString;
        }
    }
}


