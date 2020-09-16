using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A singleton disposable that does nothing when disposed.
    /// From: https://github.com/StephenCleary/Disposables
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
