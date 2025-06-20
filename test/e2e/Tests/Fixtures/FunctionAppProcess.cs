// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;


namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public abstract class FunctionAppProcess
{
    private bool _disposed;
    private Process? _funcProcess;
    internal string? _appName;

    private JobObjectRegistry? _jobObjectRegistry;
    private ILogger _logger;
    private TestLoggerProvider TestLogs;

    public FunctionAppProcess(ILogger logger, TestLoggerProvider TestLogs)
    {
        this._logger = logger;
        this.TestLogs = TestLogs;
        this._appName = Environment.GetEnvironmentVariable("TEST_APP_NAME") ?? "BasicDotNetIsolated";
    }

    public async Task InitializeAsync()
    {
        // start host via CLI if testing locally
        if (Constants.FunctionsHostUrl.Contains("localhost"))
        {
            // kill existing func processes
            this._logger.LogInformation("Shutting down any running functions hosts..");
            FixtureHelpers.KillExistingProcessesMatchingName("func");

            // start functions process
            this._logger.LogInformation($"Starting functions host for {Constants.FunctionAppCollectionName}...");

            string e2eAppPath = this.GetAppPath();

            this._funcProcess = FixtureHelpers.GetFuncHostProcess(e2eAppPath);
            string workingDir = this._funcProcess.StartInfo.WorkingDirectory;
            this._logger.LogInformation($"  Working dir: '${workingDir}' Exists: '{Directory.Exists(workingDir)}'");
            string fileName = this._funcProcess.StartInfo.FileName;
            this._logger.LogInformation($"  File name:   '${fileName}' Exists: '{File.Exists(fileName)}'");

            FixtureHelpers.AddDurableBackendEnvironmentVariables(this._funcProcess, this._logger);

            FixtureHelpers.StartProcessWithLogging(this._funcProcess, this._logger);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // ensure child processes are cleaned up
                _jobObjectRegistry = new JobObjectRegistry();
                _jobObjectRegistry.Register(this._funcProcess);
            }

            using var httpClient = new HttpClient();
            this._logger.LogInformation("Waiting for host to be running...");
            await TestUtility.RetryAsync(async () =>
            {
                try
                {
                    var response = await httpClient.GetAsync($"{Constants.FunctionsHostUrl}/admin/host/status");
                    var content = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("state", out JsonElement value) &&
                        value.GetString() == "Running")
                    {
                        this._logger.LogInformation($"  Current state: Running");
                        return true;
                    }

                    this._logger.LogInformation($"  Current state: {value}");
                    return false;
                }
                catch
                {
                    if (_funcProcess.HasExited)
                    {
                        // Something went wrong starting the host - check the logs
                        this._logger.LogInformation($"  Current state: process exited - something may have gone wrong.");
                        return false;
                    }

                    // Can get exceptions before host is running.
                    this._logger.LogInformation($"  Current state: process starting");
                    return false;
                }
            }, userMessageCallback: () => string.Join(System.Environment.NewLine, TestLogs.CoreToolsLogs));
        }

        //TODO: This line would launch the jit debugger for func - still some issues here, however. 
        //      ISSUE 1: Windows only implementation
        //      ISSUE 2: For some reason, the loaded symbols for the WebJobs extension 
        //          a) don't load automatically
        //          b) don't match the version from the local repo
        //      ISSUE 3: See the worker attach comments above
        //Process.Start("cmd.exe", "/C vsjitdebugger.exe -p " + _funcProcess.Id.ToString());
    }

    internal abstract string GetAppPath();

    public Task DisposeAsync()
    {
        if (!this._disposed)
        {
            if (this._funcProcess != null)
            {
                try
                {
                    this._funcProcess.Kill();
                    this._funcProcess.Dispose();
                }
                catch
                {
                    // process may not have started
                }
            }

            this._jobObjectRegistry?.Dispose();
        }

        this._disposed = true;

        return Task.CompletedTask;
    }
}