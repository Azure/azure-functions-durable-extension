// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class PowerShellFunctionAppProcess : FunctionAppProcess
{
    public PowerShellFunctionAppProcess(ILogger logger, TestLoggerProvider TestLogs) : base(logger, TestLogs) { }

    internal override string GetAppPath()
    {
        string rootDir = Path.GetFullPath(@"../../../../../../");
        return Path.Combine(rootDir, @$"test/e2e/Apps/{this._appName}");
    }
}