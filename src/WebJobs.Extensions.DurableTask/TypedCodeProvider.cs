// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
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

        private const string TypedDurableOrchestrationContextFullName =
            Namespace + "." + TypedDurableOrchestrationContext;

        private const string TypedDurableClientFullName =
            Namespace + "." + TypedDurableClient;

        private const string TypedDurableOrchestrationCallerFullName =
            Namespace + "." + TypedDurableOrchestrationCaller;

        private const string TypedDurableActivityCallerFullName =
            Namespace + "." + TypedDurableActivityCaller;

        private const string TypedDurableOrchestrationStarterFullName =
            Namespace + "." + TypedDurableOrchestrationStarter;

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
            this.Initialize(AppDomain.CurrentDomain.GetAssemblies());
        }

        internal void Initialize(Assembly[] assemblies)
        {
            this.typedDurableOrchestrationContextType = null;
            this.typedDurableClientType = null;
            this.typedDurableOrchestrationCallerType = null;
            this.typedDurableActivityCallerType = null;
            this.typedDurableOrchestrationStarterType = null;
            this.IsInitialized = false;

            foreach (Assembly assembly in assemblies)
            {
                Type typedDurableOrchestrationContextType = this.typedDurableOrchestrationContextType;
                Type typedDurableClientType = this.typedDurableClientType;
                Type typedDurableOrchestrationCallerType = this.typedDurableOrchestrationCallerType;
                Type typedDurableActivityCallerType = this.typedDurableActivityCallerType;
                Type typedDurableOrchestrationStarterType = this.typedDurableOrchestrationStarterType;

                try
                {
                    typedDurableOrchestrationContextType ??= assembly.GetType(
                        TypedDurableOrchestrationContextFullName,
                        throwOnError: false,
                        ignoreCase: false);
                    typedDurableClientType ??= assembly.GetType(
                        TypedDurableClientFullName,
                        throwOnError: false,
                        ignoreCase: false);
                    typedDurableOrchestrationCallerType ??= assembly.GetType(
                        TypedDurableOrchestrationCallerFullName,
                        throwOnError: false,
                        ignoreCase: false);
                    typedDurableActivityCallerType ??= assembly.GetType(
                        TypedDurableActivityCallerFullName,
                        throwOnError: false,
                        ignoreCase: false);
                    typedDurableOrchestrationStarterType ??= assembly.GetType(
                        TypedDurableOrchestrationStarterFullName,
                        throwOnError: false,
                        ignoreCase: false);
                }
                catch (Exception exception) when (IsAssemblyInspectionException(exception))
                {
                    continue;
                }

                this.typedDurableOrchestrationContextType = typedDurableOrchestrationContextType;
                this.typedDurableClientType = typedDurableClientType;
                this.typedDurableOrchestrationCallerType = typedDurableOrchestrationCallerType;
                this.typedDurableActivityCallerType = typedDurableActivityCallerType;
                this.typedDurableOrchestrationStarterType = typedDurableOrchestrationStarterType;

                if (this.AreAllTypesResolved())
                {
                    this.IsInitialized = true;
                    return;
                }
            }
        }

        private static bool IsAssemblyInspectionException(Exception exception)
        {
            return exception is FileNotFoundException ||
                exception is FileLoadException ||
                exception is BadImageFormatException ||
                exception is TypeLoadException ||
                exception is ReflectionTypeLoadException;
        }

        private bool AreAllTypesResolved()
        {
            return this.typedDurableOrchestrationContextType != null &&
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
