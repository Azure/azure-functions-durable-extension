// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines retry policies that can be passed as parameters to various operations.
    /// </summary>
    public class RetryOptions : SerializableRetryOptions
    {
        /// <summary>
        /// Creates a new instance RetryOptions with the supplied first retry and max attempts.
        /// </summary>
        /// <param name="firstRetryInterval">Timespan to wait for the first retry.</param>
        /// <param name="maxNumberOfAttempts">Max number of attempts to retry.</param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="firstRetryInterval"/> value must be greater than <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public RetryOptions(TimeSpan firstRetryInterval, int maxNumberOfAttempts)
            : base(firstRetryInterval, maxNumberOfAttempts)
        {
        }

        /// <summary>
        /// Gets or sets a delegate to call on exception to determine if retries should proceed.
        /// </summary>
        /// <value>
        /// The delegate to handle exception to determine if retries should proceed.
        /// </value>
        public Func<Exception, bool> Handle
        {
            get { return this.CoreRetryOptions.Handle; }
            set { this.CoreRetryOptions.Handle = value; }
        }

        /// <summary>
        /// Creates a <see cref="RetryOptions"/> instance from a <see cref="SerializableRetryOptions"/> one.
        /// </summary>
        /// <param name="serializableOptions">The <see cref="SerializableRetryOptions"/> instance to convert from.</param>
        /// <returns>A new <see cref="RetryOptions"/> instance based off the given parameter.</returns>
        public static RetryOptions FromSerializable(SerializableRetryOptions serializableOptions)
        {
            return serializableOptions is null
                ? null
                : new RetryOptions(serializableOptions.FirstRetryInterval, serializableOptions.MaxNumberOfAttempts)
                {
                    BackoffCoefficient = serializableOptions.BackoffCoefficient,
                    MaxRetryInterval = serializableOptions.MaxRetryInterval,
                    RetryTimeout = serializableOptions.RetryTimeout,
                };
        }
    }
}
