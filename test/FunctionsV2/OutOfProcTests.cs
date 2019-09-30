// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OutOfProcTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallHttpActionOrchestration()
        {
            DurableHttpRequest request = null;

            // Mock the CallHttpAsync API so we can capture the request and return a fixed response.
            var contextMock = new Mock<IDurableOrchestrationContext>();
            contextMock
                .Setup(ctx => ctx.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .Callback<DurableHttpRequest>(req => request = req)
                .Returns(Task.FromResult(new DurableHttpResponse(System.Net.HttpStatusCode.OK)));

            var shim = new OutOfProcOrchestrationShim(contextMock.Object);

            var executionJson = @"
{
    ""isDone"": false,
    ""actions"": [
        [{
            ""actionType"": ""CallHttp"",
            ""httpRequest"":
            {
                ""method"": ""POST"",
                ""uri"": ""https://example.com"",
                ""headers"": {
                    ""Content-Type"": ""application/json"",
                    ""Accept"": [""application/json"",""application/xml""],
                    ""x-ms-foo"": []
                },
                ""content"": ""5""
            }
        }]
    ]
}";

            // Feed the out-of-proc execution result JSON to the out-of-proc shim.
            var jsonObject = JObject.Parse(executionJson);
            bool moreWork = await shim.ExecuteAsync(jsonObject);

            // The request should not have completed because one additional replay is needed
            // to handle the result of CallHttpAsync. However, this test doesn't care about
            // completing the orchestration - we just need to validate the CallHttpAsync call.
            Assert.True(moreWork);
            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("https://example.com"), request.Uri);
            Assert.Equal("5", request.Content);
            Assert.Equal(3, request.Headers.Count);

            Assert.True(request.Headers.TryGetValue("Content-Type", out StringValues contentTypeValues));
            Assert.Single(contentTypeValues);
            Assert.Equal("application/json", contentTypeValues[0]);

            Assert.True(request.Headers.TryGetValue("Accept", out StringValues acceptValues));
            Assert.Equal(2, acceptValues.Count);
            Assert.Equal("application/json", acceptValues[0]);
            Assert.Equal("application/xml", acceptValues[1]);

            Assert.True(request.Headers.TryGetValue("x-ms-foo", out StringValues customHeaderValues));
            Assert.Empty(customHeaderValues);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallHttpActionOrchestrationWithManagedIdentity()
        {
            DurableHttpRequest request = null;

            // Mock the CallHttpAsync API so we can capture the request and return a fixed response.
            var contextMock = new Mock<IDurableOrchestrationContext>();
            contextMock
                .Setup(ctx => ctx.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .Callback<DurableHttpRequest>(req => request = req)
                .Returns(Task.FromResult(new DurableHttpResponse(System.Net.HttpStatusCode.OK)));

            var shim = new OutOfProcOrchestrationShim(contextMock.Object);

            var executionJson = @"
{
    ""isDone"": false,
    ""actions"": [
        [{
            ""actionType"": ""CallHttp"",
            ""httpRequest"": {
                ""method"": ""GET"",
                ""uri"": ""https://example.com"",
                ""tokenSource"": {
                    ""kind"": ""AzureManagedIdentity"",
                    ""resource"": ""https://management.core.windows.net""
                }
            }
        }]
    ]
}";

            // Feed the out-of-proc execution result JSON to the out-of-proc shim.
            var jsonObject = JObject.Parse(executionJson);
            await shim.ExecuteAsync(jsonObject);

            Assert.NotNull(request);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri("https://example.com"), request.Uri);
            Assert.Null(request.Content);

            Assert.NotNull(request.TokenSource);
            ManagedIdentityTokenSource tokenSource = Assert.IsType<ManagedIdentityTokenSource>(request.TokenSource);
            Assert.Equal("https://management.core.windows.net", tokenSource.Resource);
        }
    }
}
