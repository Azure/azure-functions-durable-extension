// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The name of a durable function.
    /// </summary>
    internal struct FunctionName : IEquatable<FunctionName>
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

        /// <summary>
        /// Compares two <see cref="FunctionName"/> objects for equality.
        /// </summary>
        /// <param name="a">The first <see cref="FunctionName"/> to compare.</param>
        /// <param name="b">The second <see cref="FunctionName"/> to compare.</param>
        /// <returns><c>true</c> if the two <see cref="FunctionName"/> objects are equal; otherwise <c>false</c>.</returns>
        public static bool operator ==(FunctionName a, FunctionName b)
        {
            return a.Equals(b);
        }

        /// <summary>
        /// Compares two <see cref="FunctionName"/> objects for inequality.
        /// </summary>
        /// <param name="a">The first <see cref="FunctionName"/> to compare.</param>
        /// <param name="b">The second <see cref="FunctionName"/> to compare.</param>
        /// <returns><c>true</c> if the two <see cref="FunctionName"/> objects are not equal; otherwise <c>false</c>.</returns>
        public static bool operator !=(FunctionName a, FunctionName b)
        {
            return !a.Equals(b);
        }

        /// <summary>
        /// Gets a value indicating whether to <see cref="FunctionName"/> objects
        /// are equal using value semantics.
        /// </summary>
        /// <param name="other">The other object to compare to.</param>
        /// <returns><c>true</c> if the two objects are equal using value semantics; otherwise <c>false</c>.</returns>
        public bool Equals(FunctionName other)
        {
            return string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets a value indicating whether to <see cref="FunctionName"/> objects
        /// are equal using value semantics.
        /// </summary>
        /// <param name="other">The other object to compare to.</param>
        /// <returns><c>true</c> if the two objects are equal using value semantics; otherwise <c>false</c>.</returns>
        public override bool Equals(object other)
        {
            if (!(other is FunctionName))
            {
                return false;
            }

            return this.Equals((FunctionName)other);
        }

        /// <summary>
        /// Calculates a hash code value for the current <see cref="FunctionName"/> instance.
        /// </summary>
        /// <returns>A 32-bit hash code value.</returns>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name);
        }

        /// <summary>
        /// Gets the string value of the current <see cref="FunctionName"/> instance.
        /// </summary>
        /// <returns>The name and optional version of the current <see cref="FunctionName"/> instance.</returns>
        public override string ToString()
        {
            return this.Name;
        }
    }
}
