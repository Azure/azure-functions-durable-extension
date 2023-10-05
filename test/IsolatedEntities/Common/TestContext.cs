// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace IsolatedEntities;

internal class TestContext
{
    public TestContext(DurableTaskClient client, FunctionContext executionContext)
    {
        this.ExecutionContext = executionContext;
        this.Client = client;
        this.Logger = executionContext.GetLogger(nameof(IsolatedEntities));
    }

    public FunctionContext ExecutionContext { get; }

    public DurableTaskClient Client { get; }

    public ILogger Logger { get; }

    public CancellationToken CancellationToken { get; set; }
}
