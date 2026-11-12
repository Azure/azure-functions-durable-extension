// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class ManagementKeyFixture : FunctionAppFixture
{
    private readonly string? temporaryDirectory;

    public ManagementKeyFixture(IMessageSink messageSink)
        : base(messageSink)
    {
        if (this.functionLanguageLocalizer.GetLanguageType() != LanguageType.DotnetIsolated)
        {
            throw new InvalidOperationException("Management key exclusion E2E tests support only .NET isolated.");
        }

        this.functionAppProcess.EnableAuth = true;
        this.functionAppProcess.AdditionalEnvironmentVariables = new Dictionary<string, string>
        {
            ["AzureFunctionsJobHost__extensions__durableTask__httpSettings__excludeManagementKeyFromUrls"] = "true",
        };

        if (!Constants.FunctionsHostUrl.Contains("localhost"))
        {
            this.SystemKey = Environment.GetEnvironmentVariable("E2E_TEST_DURABLE_SYSTEM_KEY")
                ?? throw new InvalidOperationException(
                    "External authenticated E2E hosts require E2E_TEST_DURABLE_SYSTEM_KEY.");
            if (string.IsNullOrWhiteSpace(this.SystemKey))
            {
                throw new InvalidOperationException("E2E_TEST_DURABLE_SYSTEM_KEY must not be empty.");
            }

            return;
        }

        this.SystemKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        this.temporaryDirectory = Path.Combine(Path.GetTempPath(), "DurableManagementKeyTests", Guid.NewGuid().ToString("N"));
        string secretsPath = Path.Combine(this.temporaryDirectory, "secrets", "functions", "secrets");
        Directory.CreateDirectory(secretsPath);
        File.WriteAllText(
            Path.Combine(secretsPath, "host.json"),
            JsonSerializer.Serialize(new
            {
                masterKey = new { name = "master", value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), encrypted = false },
                functionKeys = Array.Empty<object>(),
                systemKeys = new[] { new { name = "durabletask_extension", value = this.SystemKey, encrypted = false } },
            }));
        this.functionAppProcess.AdditionalEnvironmentVariables["AzureWebJobsSecretStorageType"] = "files";
        this.functionAppProcess.AdditionalEnvironmentVariables["FUNCTIONS_SECRETS_PATH"] = secretsPath;
        // Core Tools selects its secrets folder relative to the child process's temp directory.
        foreach (string variable in new[] { "TMP", "TEMP", "TMPDIR" })
        {
            this.functionAppProcess.AdditionalEnvironmentVariables[variable] = this.temporaryDirectory;
        }
    }

    internal string SystemKey { get; }

    public override async Task DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            if (this.temporaryDirectory != null)
            {
                Directory.Delete(this.temporaryDirectory, recursive: true);
            }
        }
    }
}
