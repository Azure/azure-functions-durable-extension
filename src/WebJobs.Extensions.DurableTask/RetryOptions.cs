// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using DurableTaskCore = DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    ///     Contains retry policies that can be passed as parameters to various operations
    /// </summary>
    public class RetryOptions
    {
        private DurableTaskCore.RetryOptions retryOptions;

        /// <summary>
        /// Creates a new instance RetryOptions with the supplied first retry and max attempts
        /// </summary>
        /// <param name="firstRetryInterval">Timespan to wait for the first retry</param>
        /// <param name="maxNumberOfAttempts">Max number of attempts to retry</param>
        /// <exception cref="ArgumentException"></exception>
        public RetryOptions(TimeSpan firstRetryInterval, int maxNumberOfAttempts)
        {
            retryOptions = new DurableTaskCore.RetryOptions(firstRetryInterval, maxNumberOfAttempts);
        }

        /// <summary>
        /// Gets or sets the first retry interval
        /// </summary>
        public TimeSpan FirstRetryInterval
        {
            get { return this.retryOptions.FirstRetryInterval; }
            set { this.retryOptions.FirstRetryInterval = value; }
        }

        /// <summary>
        /// Gets or sets the max retry interval
        /// defaults to TimeSpan.MaxValue
        /// </summary>
        public TimeSpan MaxRetryInterval
        {
            get { return this.retryOptions.MaxRetryInterval; }
            set { this.retryOptions.MaxRetryInterval = value; }
        }


        /// <summary>
        /// Gets or sets the backoff coefficient
        /// defaults to 1, used to determine rate of increase of backoff
        /// </summary>
        public double BackoffCoefficient
        {
            get { return this.retryOptions.BackoffCoefficient; }
            set { this.retryOptions.BackoffCoefficient = value; }
        }

        /// <summary>
        /// Gets or sets the timeout for retries
        /// defaults to TimeSpan.MaxValue
        /// </summary>
        public TimeSpan RetryTimeout
        {
            get { return this.retryOptions.RetryTimeout; }
            set { this.retryOptions.RetryTimeout = value; }
        }

        /// <summary>
        /// Gets or sets the max number of attempts
        /// </summary>
        public int MaxNumberOfAttempts
        {
            get { return this.retryOptions.MaxNumberOfAttempts; }
            set { this.retryOptions.MaxNumberOfAttempts = value; }
        }

        /// <summary>
        /// Gets or sets a Func to call on exception to determine if retries should proceed
        /// </summary>
        public Func<Exception, bool> Handle
        {
            get { return this.retryOptions.Handle; }
            set { this.retryOptions.Handle = value; }
        }

        internal DurableTaskCore.RetryOptions GetRetryOptions()
        {
            return this.retryOptions;
        }
    }
}
