// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ActivityFunctionDefinition
    {
        public string FunctionName { get; set; }
        public string InputType { get; set; }
        public string ReturnType { get; set; }
    }
}