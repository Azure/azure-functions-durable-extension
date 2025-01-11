// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName )]
public class HttpEndToEndTests
{
    private readonly FunctionAppFixture _fixture;

    public HttpEndToEndTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
    }

    [Theory]
    [InlineData("HelloCities_HttpStart", "", HttpStatusCode.Accepted, "")]
    public async Task HttpTriggerTests(string functionName, string queryString, HttpStatusCode expectedStatusCode, string expectedMessage)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, queryString);
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatusCode, response.StatusCode);

        if (!string.IsNullOrEmpty(expectedMessage))
        {
            Assert.False(string.IsNullOrEmpty(actualMessage));
        }
    }
}
