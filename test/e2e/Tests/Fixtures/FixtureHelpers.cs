// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public static class FixtureHelpers
{
    internal static Process GetFuncHostProcess(string appPath, LanguageType language, bool enableAuth = false)
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

        // For Python apps, if a virtual environment exists in the app folder, launch
        // func through a shell that sources the activate script first. This ensures
        // full venv activation (PATH, VIRTUAL_ENV, and any other env vars the activate
        // script sets) rather than manually replicating its behavior.
        string venvDir = Path.Combine(appPath, ".venv");
        if (language == LanguageType.Python && Directory.Exists(venvDir))
        {
            string funcArgs = "--verbose";
            if (enableAuth)
            {
                funcArgs += " --enableAuth";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string activateBat = Path.Combine(venvDir, "Scripts", "activate.bat");
                funcProcess.StartInfo.FileName = "cmd.exe";
                // Use Arguments (not ArgumentList) because ArgumentList escapes
                // quotes with backslashes which cmd.exe does not understand.
                // The outer pair of double-quotes is stripped by cmd.exe's /c rule.
                funcProcess.StartInfo.Arguments = $"/c \"\"{activateBat}\" && \"{cliPath}\" host start {funcArgs}\"";
            }
            else
            {
                string activateSh = Path.Combine(venvDir, "bin", "activate");
                funcProcess.StartInfo.FileName = "bash";
                funcProcess.StartInfo.ArgumentList.Add("-c");
                funcProcess.StartInfo.ArgumentList.Add($"source '{activateSh}' && '{cliPath}' host start {funcArgs}");
            }
        }
        else
        {
            funcProcess.StartInfo.FileName = cliPath;
            funcProcess.StartInfo.ArgumentList.Add("host");
            funcProcess.StartInfo.ArgumentList.Add("start");
            funcProcess.StartInfo.ArgumentList.Add("--verbose");

            if (enableAuth)
            {
                funcProcess.StartInfo.ArgumentList.Add("--enableAuth");
            }
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
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
                // Best effort
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    internal static void AddDurableBackendEnvironmentVariables(Process funcProcess, ILogger testLogger)
    {
        string? durableBackendEnvVarValue = Environment.GetEnvironmentVariable("E2E_TEST_DURABLE_BACKEND");
        switch ((durableBackendEnvVarValue ?? "").ToLowerInvariant())
        {
            case "azurestorage":
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__MaxGrpcMessageSizeInBytes"] = "6291456";
                return;
            case "mssql":
                string? sqlPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
                if (string.IsNullOrEmpty(sqlPassword))
                {
                    testLogger.LogWarning("Environment variable MSSQL_SA_PASSWORD not set, connection string to SQL emulator may fail");
                }
                funcProcess.StartInfo.EnvironmentVariables["SQLDB_Connection"] = $"Server=localhost,1433;Database=DurableDB;User Id=sa;Password={sqlPassword};TrustServerCertificate=True;Encrypt=False;";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__type"] = "mssql";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__connectionStringName"] = "SQLDB_Connection";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__createDatabaseIfNotExists"] = "true";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__MaxGrpcMessageSizeInBytes"] = "6291456";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__ThrowStatusExceptionsOnRaiseEvent"] = "true";
                return;
            case "azuremanaged":
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__hubName"] = "default";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__type"] = "azureManaged";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__storageProvider__connectionStringName"] = "DURABLE_TASK_SCHEDULER_CONNECTION_STRING";
                funcProcess.StartInfo.EnvironmentVariables["DURABLE_TASK_SCHEDULER_CONNECTION_STRING"] = $"Endpoint=http://localhost:8080;Authentication=None";
                funcProcess.StartInfo.EnvironmentVariables["AzureFunctionsJobHost__extensions__durableTask__ThrowStatusExceptionsOnRaiseEvent"] = "true";
                return;
            default:
                testLogger.LogWarning("Environment variable E2E_TEST_DURABLE_BACKEND not set, tests configured for Azure Storage");
                return;
        }
    }
}
