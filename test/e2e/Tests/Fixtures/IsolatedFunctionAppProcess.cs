// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class IsolatedFunctionAppProcess : FunctionAppProcess
{
    public IsolatedFunctionAppProcess(ILogger logger, TestLoggerProvider TestLogs) : base(logger, TestLogs) { }

    internal override string GetAppPath()
    {
        string rootDir = Path.GetFullPath(@"../../../../../../");
        string e2eAppBinPath = Path.Combine(rootDir, @$"test/e2e/Apps/{this._appName}/bin");
        string? e2eHostJson = Directory.GetFiles(e2eAppBinPath, "host.json", SearchOption.AllDirectories).FirstOrDefault();

        if (e2eHostJson == null)
        {
            throw new InvalidOperationException($"Could not find a built worker app under '{e2eAppBinPath}'");
        }

        string? e2eAppPath = Path.GetDirectoryName(e2eHostJson);

        if (e2eAppPath == null)
        {
            throw new InvalidOperationException($"Located host.json for app at {e2eHostJson} but could not resolve the app base directory");
        }

        return e2eAppPath;
    }
}