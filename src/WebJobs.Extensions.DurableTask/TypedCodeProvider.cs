// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class TypedCodeProvider
    {
        public const string Namespace = "Microsoft.Azure.WebJobs.Extensions.DurableTask.TypedInterfaces";

        public const string TypedDurableOrchestrationContext = "TypedDurableOrchestrationContext";
        public const string ITypedDurableOrchestrationContext = "ITypedDurableOrchestrationContext";

        public const string TypedDurableClient = "TypedDurableClient";
        public const string ITypedDurableClient = "ITypedDurableClient";

        public const string TypedDurableActivityCaller = "TypedDurableActivityCaller";
        public const string ITypedDurableActivityCaller = "ITypedDurableActivityCaller";

        public const string TypedDurableOrchestrationCaller = "TypedDurableOrchestrationCaller";
        public const string ITypedDurableOrchestrationCaller = "ITypedDurableOrchestrationCaller";

        public const string TypedDurableOrchestrationStarter = "TypedDurableOrchestrationStarter";
        public const string ITypedDurableOrchestrationStarter = "ITypedDurableOrchestrationStarter";

        private Type typedDurableOrchestrationContextType;
        private Type typedDurableClientType;

        private Type typedDurableOrchestrationCallerType;
        private Type typedDurableActivityCallerType;
        private Type typedDurableOrchestrationStarterType;

        public bool IsInitialized { get; private set; } = false;

        public Type TypedDurableOrchestrationContextType => this.typedDurableOrchestrationContextType;

        public Type TypedDurableClientType => this.typedDurableClientType;

        public void Initialize()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(t =>
            {
                try
                {
                    return t.DefinedTypes;
                }
                catch
                {
                    return new List<TypeInfo>();
                }
            }).ToList();

            this.typedDurableOrchestrationContextType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{TypedDurableOrchestrationContext}");
            this.typedDurableClientType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{TypedDurableClient}");

            this.typedDurableOrchestrationCallerType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{TypedDurableOrchestrationCaller}");
            this.typedDurableActivityCallerType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{TypedDurableActivityCaller}");
            this.typedDurableOrchestrationStarterType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{TypedDurableOrchestrationStarter}");

            this.IsInitialized = this.typedDurableOrchestrationContextType != null &&
                this.typedDurableClientType != null &&
                this.typedDurableOrchestrationCallerType != null &&
                this.typedDurableActivityCallerType != null &&
                this.typedDurableOrchestrationStarterType != null;
        }

        public object InstantiateTypedDurableOrchestrationContext(IDurableOrchestrationContext context)
        {
            if (!this.IsInitialized)
            {
                throw new InvalidOperationException("Attempting to instantiate TypedDurableOrchestrationContext without being properly initialized.");
            }

            var orchestrationCaller = this.InstantiateTypedOrchestrationCaller(context);
            var activityCaller = this.InstantiateTypedActivityCaller(context);

            return Activator.CreateInstance(this.typedDurableOrchestrationContextType, context, orchestrationCaller, activityCaller);
        }

        public object InstantiateTypedDurableClient(IDurableClient client)
        {
            if (!this.IsInitialized)
            {
                throw new InvalidOperationException("Attempting to instantiate TypedDurableClient without being properly initialized.");
            }

            var orchestrationStarter = this.InstantiateTypedDurableOrchestrationStarter(client);

            return Activator.CreateInstance(this.typedDurableClientType, client, orchestrationStarter);
        }

        private object InstantiateTypedOrchestrationCaller(IDurableOrchestrationContext context)
        {
            return Activator.CreateInstance(this.typedDurableOrchestrationCallerType, context);
        }

        private object InstantiateTypedActivityCaller(IDurableOrchestrationContext context)
        {
            return Activator.CreateInstance(this.typedDurableActivityCallerType, context);
        }

        private object InstantiateTypedDurableOrchestrationStarter(IDurableClient client)
        {
            return Activator.CreateInstance(this.typedDurableOrchestrationStarterType, client);
        }
    }
}
