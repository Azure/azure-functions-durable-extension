// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microsoft.Azure.WebJobs
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
        public static IDurableEntityContext Context => EntityContext.Value;

        /// <summary>
        /// The key of the currently executing entity.
        /// </summary>
        public static string Key => EntityContext.Value.Key;

        /// <summary>
        /// The entity reference for the currently executing entity.
        /// </summary>
        public static EntityId Self => EntityContext.Value.Self;

        internal static void SetContext(IDurableEntityContext context)
        {
            EntityContext.Value = context;
        }
    }
}
