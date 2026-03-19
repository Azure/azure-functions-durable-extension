// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class FunctionNameWithVersionTests
    {
        [Theory]
        [InlineData("MyOrchestrator", null)] // Without version
        [InlineData("MyOrchestrator", "v2.5.1")] // With version
        [InlineData("Function", "1.0.0")] // Semantic version with major.minor.patch
        [InlineData("Function", "")] // Empty string version
        [InlineData("Complex_Name-123", "v3.2.1-beta+build")] // Complex names and versions
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Combine_And_Parse_PreserveValues(string originalName, string originalVersion)
        {
            string combined = FunctionNameWithVersion.Combine(originalName, originalVersion);
            (string parsedName, string parsedVersion) = FunctionNameWithVersion.Parse(combined);

            Assert.Equal(originalName, parsedName);
            if (originalVersion == null)
            {
                Assert.Null(parsedVersion);
            }
            else
            {
                Assert.Equal(originalVersion, parsedVersion);
            }
        }
    }
}
