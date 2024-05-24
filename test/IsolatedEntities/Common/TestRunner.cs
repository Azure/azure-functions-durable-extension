using System;
using System.Collections.Generic;
using System.Diagnostics;
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IsolatedEntities;

internal static class TestRunner
{
    public static async Task<string> RunAsync(TestContext context, string? filter = null, bool listOnly = false)
    {
        var output = new StringBuilder();

        foreach (var test in All.GetAllTests())
        {
            if (filter == null || test.Name.ToLowerInvariant().Equals(filter.ToLowerInvariant()))
            {
                if (listOnly)
                {
                    output.AppendLine(test.Name);
                }
                else
                {
                    context.Logger.LogWarning("------------ starting {testName}", test.Name);

                    // if debugging, time out after 60m
                    // otherwise, time out either when the http request times out or when the individual test time limit is exceeded
                    using CancellationTokenSource cancellationTokenSource
                        = Debugger.IsAttached ? new() : CancellationTokenSource.CreateLinkedTokenSource(context.ExecutionContext.CancellationToken);
                    cancellationTokenSource.CancelAfter(Debugger.IsAttached ? TimeSpan.FromMinutes(60) : test.Timeout);
                    context.CancellationToken = cancellationTokenSource.Token;

                    try
                    {
                        await test.RunAsync(context);
                        output.AppendLine($"PASSED {test.Name}");
                    }
                    catch (Exception ex)
                    {
                        context.Logger.LogError(ex, "test {testName} failed", test.Name);
                        output.AppendLine($"FAILED {test.Name} {ex.ToString()}");
                        break;
                    }
                }
            }
        }

        return output.ToString();
    }
}
