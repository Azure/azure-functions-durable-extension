// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// The name of a durable function.
    /// </summary>
    public struct FunctionName
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionName"/> struct.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        public FunctionName(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the name of the function without the version.
        /// </summary>
        /// <value>
        /// The name of the activity function without the version.
        /// </value>
        public string Name { get; }
    }
}
