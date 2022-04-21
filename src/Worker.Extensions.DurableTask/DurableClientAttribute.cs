// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Azure Functions attribute for binding a function parameter to a Durable Task client object.
/// </summary>
public sealed class DurableClientAttribute : InputBindingAttribute
{
    /// <summary>
    /// Optional. Gets or sets the name of the task hub in which the orchestration data lives.
    /// </summary>
    /// <value>The task hub used by this binding.</value>
    /// <remarks>
    /// <para>
    /// If not specified, the task hub name used by this binding will be the value specified in host.json.
    /// If a task hub name is not configured in host.json and if the function app is running in the 
    /// Azure Functions hosted service, then task hub name is derived from the function app's name.
    /// Otherwise, a constant value is used for the task hub name.
    /// </para>
    /// <para>
    /// In general, you should <i>not</i> set a value for the task hub name here unless you intend to
    /// configure the client to interact with orchestrations in another app.
    /// </para>
    /// </remarks>
    public string? TaskHub { get; set; }

    /// <summary>
    /// Optional. Gets or sets the name of the app setting or environment variable that contains the
    /// configured storage provider's connection string credentials.
    /// </summary>
    /// <value>The name of an app setting or environment variable containing a connection string.</value>
    /// <remarks>
    /// The connection name used typically comes from the appropriate storage provider configuration value
    /// in host.json. If using the default Azure Storage provider and if no connection name value is explicitly
    /// configured, the <c>AzureWebJobsStorage</c> connection name is used.
    /// </remarks>
    public string? ConnectionName { get; set; }
}
