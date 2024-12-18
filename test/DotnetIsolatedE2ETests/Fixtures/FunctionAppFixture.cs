// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E
{
    public class FunctionAppFixture : IAsyncLifetime
    {
        private readonly ILogger _logger;
        private bool _disposed;
        private Process _funcProcess;

        private JobObjectRegistry _jobObjectRegistry;

        public FunctionAppFixture(IMessageSink messageSink)
        {
            // initialize logging            
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLogs = new TestLoggerProvider(messageSink);
            loggerFactory.AddProvider(TestLogs);
            _logger = loggerFactory.CreateLogger<FunctionAppFixture>();
        }

        public async Task InitializeAsync()
        {
            // start host via CLI if testing locally
            if (Constants.FunctionsHostUrl.Contains("localhost"))
            {
                // kill existing func processes
                _logger.LogInformation("Shutting down any running functions hosts..");
                FixtureHelpers.KillExistingProcessesMatchingName("func");

                // start functions process
                _logger.LogInformation($"Starting functions host for {Constants.FunctionAppCollectionName}...");
                _funcProcess = FixtureHelpers.GetFuncHostProcess();
                string workingDir = _funcProcess.StartInfo.WorkingDirectory;
                _logger.LogInformation($"  Working dir: '${workingDir}' Exists: '{Directory.Exists(workingDir)}'");
                string fileName = _funcProcess.StartInfo.FileName;
                _logger.LogInformation($"  File name:   '${fileName}' Exists: '{File.Exists(fileName)}'");

                //TODO: This may be added back if we want cosmos tests
                //await CosmosDBHelpers.TryCreateDocumentCollectionsAsync(_logger);

                //TODO: WORKER ATTACH ISSUES
                //      Abandoning this attach method for now - It seems like Debugger.Launch() from the app can't detect the running VS instance.
                //      Not sure if this is because VS is the parent process, or because it is already attached to testhost.exe, but for now we 
                //      will rely on manual attach. Some possible solution with DTE might exist but for now, it relies on a specific VS version
                //if (Debugger.IsAttached)
                //{
                //    _funcProcess.StartInfo.EnvironmentVariables["DURABLE_ATTACH_DEBUGGER"] = "True";
                //}

                FixtureHelpers.StartProcessWithLogging(_funcProcess, _logger);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // ensure child processes are cleaned up
                    _jobObjectRegistry = new JobObjectRegistry();
                    _jobObjectRegistry.Register(_funcProcess);
                }

                var httpClient = new HttpClient();
                _logger.LogInformation("Waiting for host to be running...");
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
                            _logger.LogInformation($"  Current state: Running");
                            return true;
                        }

                        _logger.LogInformation($"  Current state: {value}");
                        return false;
                    }
                    catch
                    {
                        if (_funcProcess.HasExited)
                        {
                            // Something went wrong starting the host - check the logs
                            _logger.LogInformation($"  Current state: process exited - something may have gone wrong.");
                            return false;
                        }

                        // Can get exceptions before host is running.
                        _logger.LogInformation($"  Current state: process starting");
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

        internal TestLoggerProvider TestLogs { get; private set; }


        public Task DisposeAsync()
        {
            if (!_disposed)
            {
                if (_funcProcess != null)
                {
                    try
                    {
                        _funcProcess.Kill();
                        _funcProcess.Dispose();
                    }
                    catch
                    {
                        // process may not have started
                    }
                }

                _jobObjectRegistry?.Dispose();
            }

            _disposed = true;

            return Task.CompletedTask;
        }
    }

    [CollectionDefinition(Constants.FunctionAppCollectionName)]
    public class FunctionAppCollection : ICollectionFixture<FunctionAppFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
