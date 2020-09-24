// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A singleton disposable that does nothing when disposed.
    /// From: https://github.com/StephenCleary/Disposables.
    /// </summary>
    public sealed class NoOpDisposable : IDisposable
    {
        private NoOpDisposable()
        {
        }

        /// <summary>
        /// Gets the instance of <see cref="NoOpDisposable"/>.
        /// </summary>
        public static NoOpDisposable Instance { get; } = new NoOpDisposable();

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
