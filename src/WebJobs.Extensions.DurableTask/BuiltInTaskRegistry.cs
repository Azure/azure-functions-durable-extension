// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class BuiltInTaskRegistry
    {
        private readonly Dictionary<(string Name, string Version), ObjectCreator<TaskOrchestration>> orchestrations;
        private readonly Dictionary<(string Name, string Version), ObjectCreator<TaskActivity>> activities;
        private readonly HashSet<string> names;
        private readonly ConditionalWeakTable<object, object> resolvedTasks = new ConditionalWeakTable<object, object>();

        public BuiltInTaskRegistry(DurabilityProvider provider)
        {
            this.orchestrations = Snapshot(provider.GetBuiltInOrchestrations());
            this.activities = Snapshot(provider.GetBuiltInActivities());
            this.OrchestrationNames = this.orchestrations.Keys.Select(key => key.Name).Distinct(StringComparer.Ordinal).ToArray();
            this.ActivityNames = this.activities.Keys.Select(key => key.Name).Distinct(StringComparer.Ordinal).ToArray();
            this.names = new HashSet<string>(this.OrchestrationNames.Concat(this.ActivityNames), StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<string> OrchestrationNames { get; }

        public IReadOnlyCollection<string> ActivityNames { get; }

        public IEnumerable<string> UnversionedOrchestrationNames
            => this.orchestrations.Keys.Where(key => key.Version.Length == 0).Select(key => key.Name);

        public void ValidateCustomerName(string name)
        {
            if (this.names.Contains(name))
            {
                throw new InvalidOperationException($"The indexed function '{name}' conflicts with a provider built-in task.");
            }
        }

        public TaskOrchestration GetOrchestration(string name, string version)
        {
            TaskOrchestration task;
            if (this.orchestrations.TryGetValue((name, version ?? string.Empty), out var creator))
            {
                task = creator.Create() ?? throw new InvalidOperationException($"The provider returned a null orchestration for '{name}'.");
            }
            else if (this.OrchestrationNames.Contains(name, StringComparer.Ordinal))
            {
                task = new UnregisteredOrchestration(name, version);
            }
            else
            {
                return null;
            }

            // Track only objects resolved here, without retaining completed orchestration instances.
            this.resolvedTasks.GetValue(task, _ => new object());
            return task;
        }

        public TaskActivity GetActivity(string name, string version)
        {
            TaskActivity task;
            if (this.activities.TryGetValue((name, version ?? string.Empty), out var creator))
            {
                task = creator.Create() ?? throw new InvalidOperationException($"The provider returned a null activity for '{name}'.");
            }
            else if (this.ActivityNames.Contains(name, StringComparer.Ordinal))
            {
                task = new UnregisteredActivity(name, version);
            }
            else
            {
                return null;
            }

            this.resolvedTasks.GetValue(task, _ => new object());
            return task;
        }

        public bool IsResolvedTask(object task)
            => task != null && this.resolvedTasks.TryGetValue(task, out _);

        private static Dictionary<(string Name, string Version), ObjectCreator<T>> Snapshot<T>(IEnumerable<ObjectCreator<T>> creators)
        {
            if (creators == null)
            {
                throw new InvalidOperationException("Provider built-in task registrations must not be null.");
            }

            var result = new Dictionary<(string Name, string Version), ObjectCreator<T>>();
            foreach (ObjectCreator<T> creator in creators)
            {
                if (creator == null || string.IsNullOrWhiteSpace(creator.Name))
                {
                    throw new InvalidOperationException("Provider built-in task registrations must have a nonempty name.");
                }

                if (creator.Name.StartsWith("@", StringComparison.Ordinal) ||
                    string.Equals(creator.Name, HttpOptions.HttpTaskActivityReservedName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"The provider built-in task name '{creator.Name}' is reserved by Durable Functions.");
                }

                var key = (Name: creator.Name, Version: creator.Version ?? string.Empty);
                if (!result.TryAdd(key, creator))
                {
                    throw new InvalidOperationException($"Duplicate provider built-in task registration '{key.Name}' with version '{key.Version}'.");
                }
            }

            return result;
        }

        private static string MissingVersionMessage(string name, string version)
            => $"The provider built-in task '{name}' is not registered with version '{version}'.";

        private sealed class UnregisteredOrchestration : TaskOrchestration
        {
            private readonly string message;

            public UnregisteredOrchestration(string name, string version)
            {
                this.message = MissingVersionMessage(name, version);
            }

            public override Task<string> Execute(OrchestrationContext context, string input)
                => Task.FromException<string>(new OrchestrationFailureException(this.message, details: null));

            public override void RaiseEvent(OrchestrationContext context, string name, string input)
            {
            }

            public override string GetStatus() => null;
        }

        private sealed class UnregisteredActivity : TaskActivity
        {
            private readonly string message;

            public UnregisteredActivity(string name, string version)
            {
                this.message = MissingVersionMessage(name, version);
            }

            public override string Run(TaskContext context, string input)
                => throw new TaskFailureException(this.message);
        }
    }
}
