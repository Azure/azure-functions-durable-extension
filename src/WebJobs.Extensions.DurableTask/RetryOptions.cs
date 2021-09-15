// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTaskCore = DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines retry policies that can be passed as parameters to various operations.
    /// </summary>
    public class RetryOptions
    {
        private readonly DurableTaskCore.RetryOptions retryOptions;

        // Would like to make this durability provider specific, but since this is a customer
        // facing type, that is difficult.
        private static readonly TimeSpan DefaultMaxRetryinterval = TimeSpan.FromDays(6);

        /// <summary>
        /// Creates a new instance RetryOptions with the supplied first retry and max attempts.
        /// </summary>
        /// <param name="firstRetryInterval">Timespan to wait for the first retry.</param>
        /// <param name="maxNumberOfAttempts">Max number of attempts to retry.</param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="firstRetryInterval"/> value must be greater than <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public RetryOptions(TimeSpan firstRetryInterval, int maxNumberOfAttempts)
        {
            this.retryOptions = new DurableTaskCore.RetryOptions(firstRetryInterval, maxNumberOfAttempts);
            this.MaxRetryInterval = DefaultMaxRetryinterval;
        }

        /// <summary>
        /// Gets or sets the first retry interval.
        /// </summary>
        /// <value>
        /// The TimeSpan to wait for the first retries.
        /// </value>
        public TimeSpan FirstRetryInterval
        {
            get { return this.retryOptions.FirstRetryInterval; }
            set { this.retryOptions.FirstRetryInterval = value; }
        }

        /// <summary>
        /// Gets or sets the max retry interval.
        /// </summary>
        /// <value>
        /// The TimeSpan of the max retry interval, defaults to <see cref="TimeSpan.MaxValue"/>.
        /// </value>
        public TimeSpan MaxRetryInterval
        {
            get { return this.retryOptions.MaxRetryInterval; }
            set { this.retryOptions.MaxRetryInterval = value; }
        }

        /// <summary>
        /// Gets or sets the backoff coefficient.
        /// </summary>
        /// <value>
        /// The backoff coefficient used to determine rate of increase of backoff. Defaults to 1.
        /// </value>
        public double BackoffCoefficient
        {
            get { return this.retryOptions.BackoffCoefficient; }
            set { this.retryOptions.BackoffCoefficient = value; }
        }

        /// <summary>
        /// Gets or sets the timeout for retries.
        /// </summary>
        /// <value>
        /// The TimeSpan timeout for retries, defaults to <see cref="TimeSpan.MaxValue"/>.
        /// </value>
        public TimeSpan RetryTimeout
        {
            get { return this.retryOptions.RetryTimeout; }
            set { this.retryOptions.RetryTimeout = value; }
        }

        /// <summary>
        /// Gets or sets the max number of attempts.
        /// </summary>
        /// <value>
        /// The maximum number of retry attempts.
        /// </value>
        public int MaxNumberOfAttempts
        {
            get { return this.retryOptions.MaxNumberOfAttempts; }
            set { this.retryOptions.MaxNumberOfAttempts = value; }
        }

        /// <summary>
        /// Gets or sets a delegate to call on exception to determine if retries should proceed.
        /// </summary>
        /// <value>
        /// The delegate to handle exception to determine if retries should proceed.
        /// </value>
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
