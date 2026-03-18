// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.SqlServer;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Shared fixture for SQL Server tests. Creates the database and schema once
    /// before any test in the [Collection("SqlServerTests")] collection runs.
    /// </summary>
    public class SqlServerTestFixture : IDisposable
    {
        private readonly SqlOrchestrationService service;

        public SqlServerTestFixture()
        {
            string connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub")
            {
                CreateDatabaseIfNotExists = true,
            };

            this.service = new SqlOrchestrationService(settings);
            this.service.CreateIfNotExistsAsync().GetAwaiter().GetResult();
        }

        // SqlOrchestrationService does not implement IDisposable, so nothing to clean up.
        public void Dispose()
        {
        }
    }
}
