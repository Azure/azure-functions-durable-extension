// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;


namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

internal class FunctionAppProcess
{
    private bool disposed;
    private Process? funcProcess;
    internal string? appName;
    internal LanguageType testLanguage;

    private JobObjectRegistry? jobObjectRegistry;
    private ILogger logger;
    private TestLoggerProvider TestLogs;

    public FunctionAppProcess(ILogger logger, TestLoggerProvider TestLogs, LanguageType testLanguage)
    {
        this.logger = logger;
        this.TestLogs = TestLogs;
        this.appName = Environment.GetEnvironmentVariable("TEST_APP_NAME") ?? "BasicDotNetIsolated";
        this.testLanguage = testLanguage;
    }

    public async Task InitializeAsync()
    {
        // start host via CLI if testing locally
        if (Constants.FunctionsHostUrl.Contains("localhost"))
        {
            // kill existing func processes
            this.logger.LogInformation("Shutting down any running functions hosts..");
            FixtureHelpers.KillExistingProcessesMatchingName("func");

            // start functions process
            this.logger.LogInformation($"Starting functions host for {Constants.FunctionAppCollectionName}...");

            string? e2eAppPath;

            string rootDir = Path.GetFullPath(@"../../../../../../");
            string binDir = @$"test/e2e/Apps/{this.appName}/bin";

            switch (this.testLanguage)
            {
                case LanguageType.PowerShell:
                case LanguageType.Python:
                case LanguageType.Node:
                    e2eAppPath = Path.Combine(rootDir, @$"test/e2e/Apps/{this.appName}");
                    break;
                case LanguageType.Java:
                case LanguageType.DotnetIsolated:
                default:
                    string e2eAppBuiltLocationPath = "";

                    if (this.testLanguage == LanguageType.Java)
                        e2eAppBuiltLocationPath = Path.Combine(rootDir, @$"test/e2e/Apps/{this.appName}/target");
                    else
                        e2eAppBuiltLocationPath = Path.Combine(rootDir, @$"test/e2e/Apps/{this.appName}/bin");

                    if (!Path.Exists(e2eAppBuiltLocationPath))
                    {
                        throw new InvalidOperationException($"The app bin path {e2eAppBuiltLocationPath} does not exist!");
                    }

                    string? e2eHostJson = Directory.GetFiles(e2eAppBuiltLocationPath, "host.json", SearchOption.AllDirectories).FirstOrDefault();

                    if (e2eHostJson == null)
                    {
                        throw new InvalidOperationException($"Could not find a built worker app under '{e2eAppBuiltLocationPath}'");
                    }

                    e2eAppPath = Path.GetDirectoryName(e2eHostJson);
                    break;
            }

            if (e2eAppPath == null)
            {
                throw new InvalidOperationException($"Could not resolve app path for app name {this.appName}.");
            }

            this.funcProcess = FixtureHelpers.GetFuncHostProcess(e2eAppPath);
            string workingDir = this.funcProcess.StartInfo.WorkingDirectory;
            this.logger.LogInformation($"  Working dir: '${workingDir}' Exists: '{Directory.Exists(workingDir)}'");
            string fileName = this.funcProcess.StartInfo.FileName;
            this.logger.LogInformation($"  File name:   '${fileName}' Exists: '{File.Exists(fileName)}'");

            FixtureHelpers.AddDurableBackendEnvironmentVariables(this.funcProcess, this.logger);

            FixtureHelpers.StartProcessWithLogging(this.funcProcess, this.logger);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // ensure child processes are cleaned up
                this.jobObjectRegistry = new JobObjectRegistry();
                this.jobObjectRegistry.Register(this.funcProcess);
            }

            using var httpClient = new HttpClient();
            this.logger.LogInformation("Waiting for host to be running...");
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
                        this.logger.LogInformation($"  Current state: Running");
                        return true;
                    }

                    this.logger.LogInformation($"  Current state: {value}");
                    return false;
                }
                catch
                {
                    if (this.funcProcess.HasExited)
                    {
                        // Something went wrong starting the host - check the logs
                        this.logger.LogInformation($"  Current state: process exited - something may have gone wrong.");
                        return false;
                    }

                    // Can get exceptions before host is running.
                    this.logger.LogInformation($"  Current state: process starting");
                    return false;
                }
            }, userMessageCallback: () => string.Join(System.Environment.NewLine, TestLogs.CoreToolsLogs));
        }
    }

    public Task DisposeAsync()
    {
        if (!this.disposed)
        {
            if (this.funcProcess != null)
            {
                try
                {
                    this.funcProcess.Kill();
                    this.funcProcess.Dispose();
                }
                catch
                {
                    // process may not have started
                }
            }

            this.jobObjectRegistry?.Dispose();
        }

        this.disposed = true;

        return Task.CompletedTask;
    }
}