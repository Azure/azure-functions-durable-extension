// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

// TODO: move to DurableTask.Core if needed.
namespace DurableTask.Core.Entities
{
    internal class EntityFailureException : Exception
    {
        public EntityFailureException(string message)
            : base(message)
        {
        }
    }
}
