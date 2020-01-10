// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public static class SupportedCategories
    {
        public const string Orchestrator = nameof(Orchestrator);
        public const string EntityInterface = nameof(EntityInterface);
        public const string Entity = nameof(Entity);
        public const string Binding = nameof(Binding);
        public const string Activity = nameof(Activity);
    }
}
