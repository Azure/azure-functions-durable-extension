// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using DurableTaskCore = DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines retry policies that can be passed as parameters to various operations.
    /// </summary>
    public class HttpRetryOptions
    {
        private readonly DurableTaskCore.RetryOptions coreRetryOptions;

        // Would like to make this durability provider specific, but since this is a developer
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
        public HttpRetryOptions(TimeSpan firstRetryInterval, int maxNumberOfAttempts)
        {
            this.coreRetryOptions = new DurableTaskCore.RetryOptions(firstRetryInterval, maxNumberOfAttempts);
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
            get { return this.coreRetryOptions.FirstRetryInterval; }
            set { this.coreRetryOptions.FirstRetryInterval = value; }
        }

        /// <summary>
        /// Gets or sets the max retry interval.
        /// </summary>
        /// <value>
        /// The TimeSpan of the max retry interval, defaults to 6 days.
        /// </value>
        public TimeSpan MaxRetryInterval
        {
            get { return this.coreRetryOptions.MaxRetryInterval; }
            set { this.coreRetryOptions.MaxRetryInterval = value; }
        }

        /// <summary>
        /// Gets or sets the backoff coefficient.
        /// </summary>
        /// <value>
        /// The backoff coefficient used to determine rate of increase of backoff. Defaults to 1.
        /// </value>
        public double BackoffCoefficient
        {
            get { return this.coreRetryOptions.BackoffCoefficient; }
            set { this.coreRetryOptions.BackoffCoefficient = value; }
        }

        /// <summary>
        /// Gets or sets the timeout for retries.
        /// </summary>
        /// <value>
        /// The TimeSpan timeout for retries, defaults to <see cref="TimeSpan.MaxValue"/>.
        /// </value>
        public TimeSpan RetryTimeout
        {
            get { return this.coreRetryOptions.RetryTimeout; }
            set { this.coreRetryOptions.RetryTimeout = value; }
        }

        /// <summary>
        /// Gets or sets the max number of attempts.
        /// </summary>
        /// <value>
        /// The maximum number of retry attempts.
        /// </value>
        public int MaxNumberOfAttempts
        {
            get { return this.coreRetryOptions.MaxNumberOfAttempts; }
            set { this.coreRetryOptions.MaxNumberOfAttempts = value; }
        }

        /// <summary>
        /// Gets or sets the list of status codes upon which the
        /// retry logic specified by this object shall be triggered.
        /// If none are provided, all 4xx and 5xx status codes
        /// will be retried.
        /// </summary>
        public IList<HttpStatusCode> StatusCodesToRetry { get; set; } = new List<HttpStatusCode>();

        internal RetryOptions GetRetryOptions()
        {
            return new RetryOptions(this.FirstRetryInterval, this.MaxNumberOfAttempts)
                {
                    BackoffCoefficient = this.BackoffCoefficient,
                    MaxRetryInterval = this.MaxRetryInterval,
                    RetryTimeout = this.RetryTimeout,
                };
        }
    }
}
