// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E

{
    public static class FixtureHelpers
    {
        public static Process GetFuncHostProcess(bool enableAuth = false)
        {
            var funcProcess = new Process();
            var rootDir = Path.GetFullPath(@"../../../../..");
            var e2eAppBinPath = Path.Combine(rootDir, @"test/DotnetIsolatedE2EApps/MainApp/bin");
            string? e2eHostJson = Directory.GetFiles(e2eAppBinPath, "host.json", SearchOption.AllDirectories).FirstOrDefault();

            if (e2eHostJson == null)
            {
                throw new InvalidOperationException($"Could not find a built worker app under '{e2eAppBinPath}'");
            }

            var e2eAppPath = Path.GetDirectoryName(e2eHostJson);

            var cliPath = Path.Combine(rootDir, @"test/DotnetIsolatedE2ETests/Azure.Functions.Cli/func");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cliPath += ".exe";
            }

            if (!File.Exists(cliPath))
            {
                throw new InvalidOperationException($"Could not find '{cliPath}'. Try running '{Path.Combine(rootDir, "setup-e2e-tests.ps1")}' to install it.");
            }

            funcProcess.StartInfo.UseShellExecute = false;
            funcProcess.StartInfo.RedirectStandardError = true;
            funcProcess.StartInfo.RedirectStandardOutput = true;
            funcProcess.StartInfo.CreateNoWindow = true;
            funcProcess.StartInfo.WorkingDirectory = e2eAppPath;
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
            funcProcess.ErrorDataReceived += (sender, e) => logger.LogError(e?.Data);
            funcProcess.OutputDataReceived += (sender, e) => logger.LogInformation(e?.Data);

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
    }
}
