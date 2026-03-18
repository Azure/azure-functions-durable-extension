// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Xunit collection definition that groups all SQL Server scale tests together.
    /// Tests decorated with [Collection("SqlServerTests")] share a single <see cref="SqlServerTestFixture"/>
    /// instance, which creates the database/schema once before the first test and
    /// tears it down after the last test in the collection completes.
    /// </summary>
    [CollectionDefinition("SqlServerTests")]
    public class SqlServerTestCollection : ICollectionFixture<SqlServerTestFixture>
    {
    }
}
