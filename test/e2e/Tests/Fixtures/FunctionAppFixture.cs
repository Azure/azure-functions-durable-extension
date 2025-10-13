// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class FunctionAppFixture : IAsyncLifetime
{
    internal readonly ILogger logger;
    internal TestLoggerProvider TestLogs { get; private set; }

    internal FunctionAppProcess functionAppProcess;
    internal ITestLanguageLocalizer functionLanguageLocalizer;

    internal enum ConfiguredDurabilityProviderType
    {
        AzureStorage,
        MSSQL,
        AzureManaged
    }

    public FunctionAppFixture(IMessageSink messageSink)
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        this.TestLogs = new TestLoggerProvider(messageSink);
        loggerFactory.AddProvider(this.TestLogs);
        this.logger = loggerFactory.CreateLogger<FunctionAppProcess>();

        string? e2eTestLanguageEnvVarValue = Environment.GetEnvironmentVariable("E2E_TEST_FUNCTIONS_LANGUAGE");
        this.logger.LogInformation("E2E_TEST_FUNCTIONS_LANGUAGE set to " + e2eTestLanguageEnvVarValue);
        switch ((e2eTestLanguageEnvVarValue ?? "").ToLowerInvariant())
        {
            case "dotnet-isolated":
                this.functionLanguageLocalizer = new IsolatedTestLanguageLocalizer();
                break;
            case "powershell":
                this.functionLanguageLocalizer = new PowerShellTestLanguageLocalizer();
                break;
            case "python":
                this.functionLanguageLocalizer = new PythonTestLanguageLocalizer();
                break;
            case "node":
                this.functionLanguageLocalizer = new NodeTestLanguageLocalizer();
                break;
            case "java":
                this.functionLanguageLocalizer = new JavaTestLanguageLocalizer();
                break;
            default:
                this.logger.LogWarning("Environment variable E2E_TEST_FUNCTIONS_LANGUAGE not set, tests configured for dotnet-isolated");
                this.functionLanguageLocalizer = new IsolatedTestLanguageLocalizer();
                break;
        }

        this.functionAppProcess = new FunctionAppProcess(this.logger, this.TestLogs, this.functionLanguageLocalizer.GetLanguageType());
    }

    internal ConfiguredDurabilityProviderType GetDurabilityProvider()
    {
        string? e2eTestDurableBackendEnvVarValue = Environment.GetEnvironmentVariable("E2E_TEST_DURABLE_BACKEND");
        switch (e2eTestDurableBackendEnvVarValue)
        {
            case "mssql":
                return ConfiguredDurabilityProviderType.MSSQL;
            case "azuremanaged":
                return ConfiguredDurabilityProviderType.AzureManaged;
            case "azurestorage":
                return ConfiguredDurabilityProviderType.AzureStorage;
            default:
                this.logger.LogWarning("Environment variable E2E_TEST_DURABLE_BACKEND not set, test code will assume Azure Storage backend");
                return ConfiguredDurabilityProviderType.AzureStorage;
        }
    }

    public Task InitializeAsync()
    {
        return this.functionAppProcess.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        return this.functionAppProcess.DisposeAsync();
    }
}
