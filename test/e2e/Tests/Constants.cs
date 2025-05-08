// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

internal class Constants
{
    public static readonly IConfiguration Configuration = TestUtility.GetTestConfiguration();

    internal static readonly string FunctionsHostUrl = Configuration["FunctionAppUrl"] ?? "http://localhost:7071";

    internal const string FunctionAppCollectionName = "DurableTestsCollection";
    internal const string FunctionAppCollectionSequentialName = "DurableTestsCollectionSequential";
}
