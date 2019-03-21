// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provides functionality available to actor operations running in interleaving mode.
    /// </summary>
    public interface IInterleavingActorContext :
        IDurableActorContext, IInterleavingContext
    {
    }
}
