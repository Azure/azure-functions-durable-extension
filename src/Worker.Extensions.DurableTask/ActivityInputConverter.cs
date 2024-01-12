﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal class ActivityInputConverter : IInputConverter
{
    private readonly DurableTaskWorkerOptions options;

    public ActivityInputConverter(IOptions<DurableTaskWorkerOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Source is null)
        {
            return new(ConversionResult.Success(null));
        }

        if (context.Source is not string source)
        {
            throw new InvalidOperationException($"Expected converter source to be a string, received {context.Source?.GetType()}.");
        }

        object? value = this.options.DataConverter.Deserialize(source, context.TargetType);
        return new(ConversionResult.Success(value));
    }
}
