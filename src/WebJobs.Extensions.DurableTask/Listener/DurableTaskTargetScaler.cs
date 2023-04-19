// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !FUNCTIONS_V1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;


namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class DurableTaskTargetScaler : ITargetScaler
    {
        public TargetScalerDescriptor TargetScalerDescriptor => throw new NotImplementedException();

        public Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            throw new NotImplementedException();
        }
    }
}
#endif