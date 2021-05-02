// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace DFPerfScenarios
{
    public static class Common
	{
        [FunctionName(nameof(HelloSequence))]
		public static async Task<List<string>> HelloSequence(
			[OrchestrationTrigger] IDurableOrchestrationContext context)
		{
			List<string> outputs = new List<string>();
            outputs.Add(await context.CallActivityAsync<string>("SayHello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("SayHello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("SayHello", "London"));
			return outputs;
		}

		[FunctionName(nameof(SayHello))]
		public static string SayHello([ActivityTrigger] string name) => $"Hello {name}!";
	}
}
