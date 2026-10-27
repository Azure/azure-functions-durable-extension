// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(ExternalizedPayloadCollection.Name)]
[Trait("PowerShell", "Skip")] // Worker-owned payload registration is specific to .NET isolated.
[Trait("Python", "Skip")] // Worker-owned payload registration is specific to .NET isolated.
[Trait("Node", "Skip")] // Worker-owned payload registration is specific to .NET isolated.
[Trait("Java", "Skip")] // Worker-owned payload registration is specific to .NET isolated.
public class ExternalizedPayloadTests
{
    private readonly ExternalizedPayloadFixture fixture;

    public ExternalizedPayloadTests(ExternalizedPayloadFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(output);
    }

    [Theory]
    [InlineData("sync", 1_100_000)]
    [InlineData("async", 1_100_000)]
    [InlineData("string", 1_100_000)]
    [InlineData("array", 1_100_000)]
    [InlineData("binary", 1_100_000)]
    [InlineData("binary", 16)]
    [InlineData("binary", 0)]
    [InlineData("registered", 1_100_000)]
    [InlineData("registered-binary", 1_100_000)]
    [InlineData("registered-orchestration", 1_100_000)]
    [InlineData("sub", 1_100_000)]
    [InlineData("continue", 1_100_000)]
    [InlineData("event", 1_100_000)]
    [InlineData("http", 8192)]
    public async Task OptIn_RoundTripsThroughHostBinding_WithoutHostPayloadStorage(string mode, int size)
    {
        using var client = new HttpClient { BaseAddress = this.fixture.HostAddress };
        using HttpResponseMessage start = await client.PostAsJsonAsync($"api/ExternalizedPayload_Start/{mode}", size);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(start);
        string statusUri = await DurableHelpers.ParseStatusQueryGetUriAsync(start);
        string expected = new('x', size);

        if (mode == "event")
        {
            using HttpResponseMessage raised = await client.PostAsJsonAsync(
                $"api/ExternalizedPayload_Event/{instanceId}", expected);
            raised.EnsureSuccessStatusCode();
        }

        await DurableHelpers.WaitForOrchestrationStateAsync(statusUri, "Completed", 60);
        JsonElement rawState = await client.GetFromJsonAsync<JsonElement>(statusUri);
        string rawOutput = rawState.GetProperty("output").GetString()!;
        if (size >= 4096)
        {
            Assert.StartsWith("blob:", rawOutput);
            Assert.True(rawOutput.Length < 1024);
        }
        else
        {
            Assert.Equal(expected, rawOutput);
        }

        using HttpResponseMessage read = await client.GetAsync($"api/ExternalizedPayload_Read/{instanceId}");
        string body = await read.Content.ReadAsStringAsync();
        Assert.True(read.IsSuccessStatusCode, body);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expected, document.RootElement.GetProperty("input").GetString());
        Assert.Equal(expected, document.RootElement.GetProperty("output").GetString());
        Assert.Equal(expected, document.RootElement.GetProperty("customStatus").GetString());

        JsonElement activityOutputs = document.RootElement.GetProperty("activityOutputs");
        if (mode is not ("sub" or "event"))
        {
            Assert.NotEmpty(activityOutputs.EnumerateArray());
        }

        foreach (JsonElement output in activityOutputs.EnumerateArray())
        {
            using JsonDocument result = JsonDocument.Parse(output.GetString()!);
            JsonElement argument = result.RootElement;
            if (mode is "binary" or "registered-binary")
            {
                Assert.Equal(Encoding.UTF8.GetBytes(expected), argument.GetBytesFromBase64());
            }
            else if (mode == "array")
            {
                Assert.Equal(expected, Assert.Single(argument.EnumerateArray()).GetString());
            }
            else if (mode == "http")
            {
                Assert.Equal(expected, argument.GetProperty("content").GetString());
            }
            else if (mode == "string")
            {
                Assert.Equal(expected, argument.GetString());
            }
            else
            {
                Assert.Equal(expected, Assert.Single(argument.EnumerateObject()).Value.GetString());
            }
        }
    }
}
