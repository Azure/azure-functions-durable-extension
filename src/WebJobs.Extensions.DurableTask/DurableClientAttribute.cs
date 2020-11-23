// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Attribute used to bind a function parameter to a <see cref="IDurableClient"/>, <see cref="IDurableEntityClient"/>, or <see cref="IDurableOrchestrationClient"/> instance.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [DebuggerDisplay("TaskHub={TaskHub}, ConnectionName={ConnectionName}")]
    [Binding]
    public class DurableClientAttribute : Attribute, IEquatable<DurableClientAttribute>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableClientAttribute"/> class.
        /// </summary>
        public DurableClientAttribute() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableClientAttribute"/> class.
        /// </summary>
        /// <param name="durableClientOptions">Options to configure the IDurableClient created.</param>
        public DurableClientAttribute(DurableClientOptions durableClientOptions)
        {
            this.TaskHub = durableClientOptions.TaskHub;
            this.ConnectionName = durableClientOptions.ConnectionName;
            this.ExternalClient = durableClientOptions.IsExternalClient;
        }

        /// <summary>
        /// Optional. Gets or sets the name of the task hub in which the orchestration data lives.
        /// </summary>
        /// <value>The task hub used by this binding.</value>
        /// <remarks>
        /// The default behavior is to use the task hub name specified in <see cref="DurableTaskOptions.HubName"/>.
        /// If no value exists there, then a default value will be used.
        /// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
        [AutoResolve]
#pragma warning restore CS0618 // Type or member is obsolete
        public string TaskHub { get; set; }

        /// <summary>
        /// Optional. Gets or sets the setting name for the app setting containing connection details used by this binding to connect
        /// to instances of the storage provider other than the default one this application communicates with.
        /// </summary>
        /// <value>The name of an app setting containing connection details.</value>
        /// <remarks>
        /// For Azure Storage the default behavior is to use the value of <see cref="AzureStorageOptions.ConnectionStringName"/>.
        /// If no value exists there, then the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        public string ConnectionName { get; set; }

        /// <summary>
        ///     Indicate if the client is External from the azure function where orchestrator functions are hosted.
        /// </summary>
        public bool ExternalClient { get; set; }

        /// <summary>
        /// Returns a hash code for this attribute.
        /// </summary>
        /// <returns>A hash code for this attribute.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return
                    this.TaskHub?.GetHashCode() ?? 0 +
                    this.ConnectionName?.GetHashCode() ?? 0;
            }
        }

        /// <summary>
        /// Compares two <see cref="DurableClientAttribute"/> instances for value equality.
        /// </summary>
        /// <param name="obj">The <see cref="DurableClientAttribute"/> object to compare with.</param>
        /// <returns><c>true</c> if the two attributes have the same configuration; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as DurableClientAttribute);
        }

        /// <summary>
        /// Compares two <see cref="DurableClientAttribute"/> instances for value equality.
        /// </summary>
        /// <param name="other">The <see cref="DurableClientAttribute"/> object to compare with.</param>
        /// <returns><c>true</c> if the two attributes have the same configuration; otherwise <c>false</c>.</returns>
        public bool Equals(DurableClientAttribute other)
        {
            if (other == null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.TaskHub, other.TaskHub, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.ConnectionName, other.ConnectionName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
