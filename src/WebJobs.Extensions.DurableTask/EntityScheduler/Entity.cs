// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Statically accessible context for entity operations.
    /// </summary>
    public static class Entity
    {
        private static readonly AsyncLocal<IDurableEntityContext> EntityContext
            = new AsyncLocal<IDurableEntityContext>();

        /// <summary>
        /// The context of the currently executing entity.
        /// </summary>
        public static IDurableEntityContext Current => EntityContext.Value;

        internal static void SetContext(IDurableEntityContext context)
        {
            EntityContext.Value = context;
        }

        /// <summary>
        /// Sets the current context to a mocked context for unit testing.
        /// </summary>
        /// <param name="mockContext">The mocked context.</param>
        public static void SetMockContext(IDurableEntityContext mockContext)
        {
            if (mockContext is DurableEntityContext)
            {
                throw new InvalidOperationException("Only mocked entity contexts are supported, not real ones.");
            }

            EntityContext.Value = mockContext;
        }
    }
}
