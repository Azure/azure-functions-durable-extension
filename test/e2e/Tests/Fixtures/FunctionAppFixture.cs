// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class FunctionAppFixture : IAsyncLifetime
{
    internal readonly ILogger _logger;
    internal TestLoggerProvider TestLogs { get; private set; }

    internal FunctionAppProcess? _functionAppProcess;

    public FunctionAppFixture(IMessageSink messageSink)
    {
        ILoggerFactory loggerFactory = new LoggerFactory();
        this.TestLogs = new TestLoggerProvider(messageSink);
        loggerFactory.AddProvider(this.TestLogs);
        this._logger = loggerFactory.CreateLogger<FunctionAppProcess>();
    }

    public Task InitializeAsync()
    {
        string? e2eTestLanguageEnvVarValue = Environment.GetEnvironmentVariable("E2E_TEST_FUNCTIONS_LANGUAGE");
        _logger.LogInformation("E2E_TEST_FUNCTIONS_LANGUAGE set to " + e2eTestLanguageEnvVarValue);
        switch ((e2eTestLanguageEnvVarValue ?? "").ToLowerInvariant())
        {
            case "dotnet-isolated":
                _functionAppProcess = new IsolatedFunctionAppProcess(this._logger, this.TestLogs);
                break;
            case "powershell":
                _functionAppProcess = new PowerShellFunctionAppProcess(this._logger, this.TestLogs);
                break;
            default:
                _logger.LogWarning("Environment variable E2E_TEST_FUNCTIONS_LANGUAGE not set, tests configured for dotnet-isolated");
                _functionAppProcess = new IsolatedFunctionAppProcess(this._logger, this.TestLogs);
                break;
        }

        return _functionAppProcess.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        if (this._functionAppProcess != null)
        {
            return _functionAppProcess.DisposeAsync();
        }

        return Task.CompletedTask;
    }
}
