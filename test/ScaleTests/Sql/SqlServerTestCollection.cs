// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Xunit collection definition that groups all SQL Server scale tests together.
    /// Tests in the [Collection("SqlServerTests")] collection run sequentially
    /// to avoid concurrent access to the shared SQL Server database.
    /// </summary>
    [CollectionDefinition("SqlServerTests")]
    public class SqlServerTestCollection
    {
    }
}
