// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Grpc.Core.Interceptors;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal interface IExternalizedPayloads
{
    Interceptor ClientInterceptor { get; }

    void ValidateSize(string? payload);

    Task<string?> ExternalizeAsync(string? payload, CancellationToken cancellationToken);

    Task<string?> ResolveAsync(string? payload, CancellationToken cancellationToken);
}
