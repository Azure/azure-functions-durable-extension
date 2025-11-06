// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using DotNetIsolated.Typed;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Hosting;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureDurableWorker()
    .AddTasks(r => r
        .AddOrchestrator<HelloCitiesTyped>()
        .AddOrchestrator<EntityOrchestration>()
        .AddActivity<SayHelloTyped>()
        .AddEntity<CountingEntity>());

IHost app = builder.Build();

app.Run();
