using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class GeneratedCodeProvider
    {
        public const string Namespace = "Microsoft.Azure.WebJobs.Generated";

        public const string GeneratedDurableOrchestrationContext = "GeneratedDurableOrchestrationContext";
        public const string IGeneratedDurableOrchestrationContext = "IGeneratedDurableOrchestrationContext";

        public const string GeneratedDurableClient = "GeneratedDurableClient";
        public const string IGeneratedDurableClient = "IGeneratedDurableClient";

        public const string GeneratedDurableActivityCaller = "GeneratedDurableActivityCaller";
        public const string IGeneratedDurableActivityCaller = "IGeneratedDurableActivityCaller";

        public const string GeneratedDurableOrchestrationCaller = "GeneratedDurableOrchestrationCaller";
        public const string IGeneratedDurableOrchestrationCaller = "IGeneratedDurableOrchestrationCaller";

        public const string GeneratedDurableOrchestrationStarter = "GeneratedDurableOrchestrationStarter";
        public const string IGeneratedDurableOrchestrationStarter = "IGeneratedDurableOrchestrationStarter";

        private Type generatedDurableOrchestrationContextType;
        private Type generatedDurableClientType;

        private Type generatedDurableOrchestrationCallerType;
        private Type generatedDurableActivityCallerType;
        private Type generatedDurableOrchestrationStarterType;

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

            this.generatedDurableOrchestrationContextType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{GeneratedDurableOrchestrationContext}");
            this.generatedDurableClientType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{GeneratedDurableClient}");

            this.generatedDurableOrchestrationCallerType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{GeneratedDurableOrchestrationCaller}");
            this.generatedDurableActivityCallerType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{GeneratedDurableActivityCaller}");
            this.generatedDurableOrchestrationStarterType = types.FirstOrDefault(t => t.FullName == $"{Namespace}.{GeneratedDurableOrchestrationStarter}");
        }

        public object InstantiateGeneratedDurableOrchestrationContext(IDurableOrchestrationContext context)
        {
            var orchestrationCaller = InstantiateGeneratedOrchestrationCaller(context);
            var activityCaller = InstantiateGeneratedActivityCaller(context);

            return Activator.CreateInstance(this.generatedDurableOrchestrationContextType, context, orchestrationCaller, activityCaller);
        }

        public T InstantiateGeneratedDurableClient<T>(IDurableClient client)
        {
            var orchestrationStarter = InstantiateGeneratedDurableOrchestrationStarter(client);

            return (T)Activator.CreateInstance(this.generatedDurableClientType, client, orchestrationStarter);
        }

        public object InstantiateGeneratedDurableClient(IDurableClient client)
        {
            var orchestrationStarter = InstantiateGeneratedDurableOrchestrationStarter(client);

            return Activator.CreateInstance(this.generatedDurableClientType, client, orchestrationStarter);
        }

        private object InstantiateGeneratedOrchestrationCaller(IDurableOrchestrationContext context)
        {
            return Activator.CreateInstance(this.generatedDurableOrchestrationCallerType, context);
        }

        private object InstantiateGeneratedActivityCaller(IDurableOrchestrationContext context)
        {
            return Activator.CreateInstance(this.generatedDurableActivityCallerType, context);
        }

        private object InstantiateGeneratedDurableOrchestrationStarter(IDurableClient client)
        {
            return Activator.CreateInstance(this.generatedDurableOrchestrationStarterType, client);
        }
    }
}
