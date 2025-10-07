// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.DurableTask.Worker;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// Default implementation of IExceptionPropertiesProvider that returns no custom properties.
/// </summary>
internal class DefaultExceptionPropertiesProvider : IExceptionPropertiesProvider
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?>? GetExceptionProperties(Exception exception)
    {
        // Default implementation returns null, indicating no custom properties should be added
        return null;
    }

    IDictionary<string, object>? IExceptionPropertiesProvider.GetExceptionProperties(Exception exception)
    {
        throw new NotImplementedException();
    }
}
