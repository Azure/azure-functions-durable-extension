// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.SqlServer;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    /// <summary>
    /// Test fixture that initializes SQL Server schema before running SQL tests.
    /// Ensures the database schema is created once before all SQL tests run.
    /// </summary>
    public class SqlServerTestFixture : IAsyncLifetime
    {
        private static readonly object LockObject = new object();
        private static bool schemaInitialized = false;

        public Task InitializeAsync()
        {
            // Only initialize schema once, even if multiple test classes use this fixture
            if (schemaInitialized)
            {
                return Task.CompletedTask;
            }

            lock (LockObject)
            {
                if (schemaInitialized)
                {
                    return Task.CompletedTask;
                }

                try
                {
                    var connectionString = TestHelpers.GetSqlConnectionString();
                    var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub", schemaName: null);
                    var service = new SqlOrchestrationService(settings);

                    // Initialize the schema synchronously in the lock
                    service.CreateIfNotExistsAsync().GetAwaiter().GetResult();

                    schemaInitialized = true;
                }
                catch (Exception ex)
                {
                    // If schema initialization fails, log but don't fail all tests
                    // Tests will fail individually if they can't connect
                    Console.WriteLine($"Warning: Failed to initialize SQL schema: {ex.Message}");
                }
            }

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            // No cleanup needed
            return Task.CompletedTask;
        }
    }
}
