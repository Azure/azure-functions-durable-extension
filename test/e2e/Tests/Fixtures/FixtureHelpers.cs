// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public static class FixtureHelpers
{
    public static Process GetFuncHostProcess(string appPath, bool enableAuth = false)
    {
        var cliPath = Path.Combine(Path.GetTempPath(), @"DurableTaskExtensionE2ETests/Azure.Functions.Cli/func");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cliPath += ".exe";
        }

        if (!File.Exists(cliPath))
        {
            throw new InvalidOperationException($"Could not find '{cliPath}'. Try running '{Path.Combine("build-e2e-test.ps1")}' to install it.");
        }

        var funcProcess = new Process();

        funcProcess.StartInfo.UseShellExecute = false;
        funcProcess.StartInfo.RedirectStandardError = true;
        funcProcess.StartInfo.RedirectStandardOutput = true;
        funcProcess.StartInfo.CreateNoWindow = true;
        funcProcess.StartInfo.WorkingDirectory = appPath;
        funcProcess.StartInfo.FileName = cliPath;
        funcProcess.StartInfo.ArgumentList.Add("host");
        funcProcess.StartInfo.ArgumentList.Add("start");
        funcProcess.StartInfo.ArgumentList.Add("--csharp");
        funcProcess.StartInfo.ArgumentList.Add("--verbose");

        if (enableAuth)
        {
            funcProcess.StartInfo.ArgumentList.Add("--enableAuth");
        }

        return funcProcess;
    }

    public static void StartProcessWithLogging(Process funcProcess, ILogger logger)
    {
        funcProcess.ErrorDataReceived += (sender, e) => { 
            try { logger.LogError(e?.Data); } 
            catch (InvalidOperationException) { } 
        };
        funcProcess.OutputDataReceived += (sender, e) => { 
            try { logger.LogInformation(e?.Data); } 
            catch (InvalidOperationException) { } 
        };

        funcProcess.Start();

        logger.LogInformation($"Started '{funcProcess.StartInfo.FileName}'");

        funcProcess.BeginErrorReadLine();
        funcProcess.BeginOutputReadLine();
    }

    public static void KillExistingProcessesMatchingName(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Best effort
            }
        }
    }

    internal static void AddDurableBackendEnvironmentVariables(Process funcProcess, ILogger testLogger)
    {
        string? durableBackendEnvVarValue = Environment.GetEnvironmentVariable("E2E_TEST_DURABLE_BACKEND");
        switch ((durableBackendEnvVarValue ?? "").ToLowerInvariant())
        {
            case "azurestorage":
                return;
            case "mssql":
                string? sqlPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
                if (string.IsNullOrEmpty(sqlPassword))
                {
                    testLogger.LogWarning("Environment variable MSSQL_SA_PASSWORD not set, connection string to SQL emulator may fail");
                }
                funcProcess.StartInfo.EnvironmentVariables["SQLDB_Connection"] = $"Server=localhost,1433;Database=DurableDB;User Id=sa;Password={sqlPassword};";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__type"] = "mssql";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__connectionStringName"] = "SQLDB_Connection";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__createDatabaseIfNotExists"] = "true";
                return;
            default:
                testLogger.LogWarning("Environment variable E2E_TEST_DURABLE_BACKEND not set, tests configured for Azure Storage");
                return;
        }
    }
}