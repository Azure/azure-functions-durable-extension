// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.AzureBlobPayloads;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class LargePayloadPurgeFunctionsTests
{
    [Fact]
    public void OptionalAssembly_SuppliesFourOrdinaryFunctionMethods()
    {
        MethodInfo[] functions = typeof(LargePayloadPurgeFunctions).Assembly.GetTypes().SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() != null).ToArray();
        Assert.Equal(4, functions.Length);
        Assert.Equal(
            new[] { nameof(BlobPurgeJobOrchestrator), nameof(DeleteExternalBlobActivity), nameof(GetLargePayloadTombstonesActivity), nameof(ReportLargePayloadPurgeResultsActivity) },
            functions.Select(method => method.GetCustomAttribute<FunctionAttribute>()!.Name).OrderBy(name => name));
        MethodInfo orchestrator = functions.Single(method => method.Name == nameof(LargePayloadPurgeFunctions.RunOrchestrator));
        Assert.True(orchestrator.GetParameters()[0].GetCustomAttribute<OrchestrationTriggerAttribute>()!.LargePayloadPurge);
        Assert.Equal(3, functions.Count(method => method.GetParameters()[0].GetCustomAttribute<ActivityTriggerAttribute>() != null));
        Assert.Equal(2, functions.Count(method => method.GetParameters().Any(parameter => parameter.GetCustomAttribute<DurableClientAttribute>() != null)));
    }

    [Fact]
    public void BaseExtension_DoesNotInjectAnyFunctionDefinitions()
    {
        Assert.NotEqual(typeof(DurableClientAttribute).Assembly, typeof(LargePayloadPurgeFunctions).Assembly);
        Assert.DoesNotContain(typeof(DurableClientAttribute).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods()), method => method.GetCustomAttribute<FunctionAttribute>() != null);
    }
}
