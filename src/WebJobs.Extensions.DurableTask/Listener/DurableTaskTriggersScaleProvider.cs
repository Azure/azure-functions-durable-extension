// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if FUNCTIONS_V3_OR_GREATER

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class DurableTaskTriggersScaleProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private IScaleMonitor monitor;
        private ITargetScaler targetScaler;

        public DurableTaskTriggersScaleProvider(
            IServiceProvider serviceProvider,
            TriggerMetadata triggerMetadata)
        {
            string functionId = triggerMetadata.FunctionName;
            FunctionName functionName = new FunctionName(functionId); // TODO: this is wrong;

            var extension = serviceProvider.GetService<DurableTaskExtension>();
            var connectionName = extension.GetConnectionName();

            this.monitor = extension.GetScaleMonitor(
                functionId,
                functionName,
                connectionName);
            this.targetScaler = extension.GetTargetScaler(
                functionId,
                functionName,
                connectionName);
        }

        public IScaleMonitor GetMonitor()
        {
            return this.monitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this.targetScaler;
        }
    }
}
#endif