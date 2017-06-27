// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Intended for internal use only.
    /// </summary>
    public interface IActivityReturnValue
    {
        /// <summary>
        /// Gets the return value of the orchestration.
        /// </summary>
        string ReturnValue { get; }

        /// <summary>
        /// Sets the return value of the activity.
        /// </summary>
        void SetReturnValue(object returnValue);
    }
}
