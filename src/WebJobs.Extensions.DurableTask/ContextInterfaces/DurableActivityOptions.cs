// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Options for starting a durable activity.
    /// </summary>
    public sealed class DurableActivityOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DurableActivityOptions"/> class.
        /// </summary>
        /// <param name="functionName">The name of the activity function to invoke.</param>
        public DurableActivityOptions(string functionName)
        {
            this.FunctionName = functionName;
        }

        /// <summary>
        /// Gets the name of the activity function to invoke.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Gets or sets the input to the activity.
        /// </summary>
        public object Input { get; init; }

        /// <summary>
        /// Gets or sets the retry options for the activity.
        /// </summary>
        public RetryOptions RetryOptions { get; init; }

        /// <summary>
        /// Gets the tags associated with the activity.
        /// </summary>
        public IReadOnlyDictionary<string, string> Tags { get; init; } = ImmutableDictionary<string, string>.Empty;
    }
}