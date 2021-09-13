// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTaskCore = DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines retry policies that can be passed as parameters to various operations.
    /// </summary>
    public class SerializableRetryOptions
    {
        // Would like to make this durability provider specific, but since this is a customer
        // facing type, that is difficult.
        private static readonly TimeSpan DefaultMaxRetryinterval = TimeSpan.FromDays(6);

        /// <summary>
        /// Creates a new instance SerializableRetryOptions with the supplied first retry and max attempts.
        /// </summary>
        /// <param name="firstRetryInterval">Timespan to wait for the first retry.</param>
        /// <param name="maxNumberOfAttempts">Max number of attempts to retry.</param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="firstRetryInterval"/> value must be greater than <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public SerializableRetryOptions(TimeSpan firstRetryInterval, int maxNumberOfAttempts)
        {
            this.CoreRetryOptions = new DurableTaskCore.RetryOptions(firstRetryInterval, maxNumberOfAttempts);
            this.MaxRetryInterval = DefaultMaxRetryinterval;
        }

        /// <summary>
        /// Gets the core retry options.
        /// </summary>
        /// <value>
        /// The core retry options.
        /// </value>
        protected DurableTaskCore.RetryOptions CoreRetryOptions { get; }

        /// <summary>
        /// Gets or sets the first retry interval.
        /// </summary>
        /// <value>
        /// The TimeSpan to wait for the first retries.
        /// </value>
        public TimeSpan FirstRetryInterval
        {
            get { return this.CoreRetryOptions.FirstRetryInterval; }
            set { this.CoreRetryOptions.FirstRetryInterval = value; }
        }

        /// <summary>
        /// Gets or sets the max retry interval.
        /// </summary>
        /// <value>
        /// The TimeSpan of the max retry interval, defaults to 6 days.
        /// </value>
        public TimeSpan MaxRetryInterval
        {
            get { return this.CoreRetryOptions.MaxRetryInterval; }
            set { this.CoreRetryOptions.MaxRetryInterval = value; }
        }

        /// <summary>
        /// Gets or sets the backoff coefficient.
        /// </summary>
        /// <value>
        /// The backoff coefficient used to determine rate of increase of backoff. Defaults to 1.
        /// </value>
        public double BackoffCoefficient
        {
            get { return this.CoreRetryOptions.BackoffCoefficient; }
            set { this.CoreRetryOptions.BackoffCoefficient = value; }
        }

        /// <summary>
        /// Gets or sets the timeout for retries.
        /// </summary>
        /// <value>
        /// The TimeSpan timeout for retries, defaults to <see cref="TimeSpan.MaxValue"/>.
        /// </value>
        public TimeSpan RetryTimeout
        {
            get { return this.CoreRetryOptions.RetryTimeout; }
            set { this.CoreRetryOptions.RetryTimeout = value; }
        }

        /// <summary>
        /// Gets or sets the max number of attempts.
        /// </summary>
        /// <value>
        /// The maximum number of retry attempts.
        /// </value>
        public int MaxNumberOfAttempts
        {
            get { return this.CoreRetryOptions.MaxNumberOfAttempts; }
            set { this.CoreRetryOptions.MaxNumberOfAttempts = value; }
        }

        internal DurableTaskCore.RetryOptions GetRetryOptions()
        {
            return this.CoreRetryOptions;
        }
    }
}
