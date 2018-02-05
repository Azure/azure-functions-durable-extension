// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    // Boilerplate code...
    internal class ObjectValueProvider : IValueProvider
    {
        private readonly object value;
        private readonly Task<object> valueAsTask;
        private readonly Type valueType;

        public ObjectValueProvider(object value, Type valueType)
        {
            if (value != null && !valueType.IsAssignableFrom(value.GetType()))
            {
                throw new ArgumentException($"Cannot convert {value} to {valueType.Name}.");
            }

            this.value = value;
            this.valueAsTask = Task.FromResult(value);
            this.valueType = valueType;
        }

        public Type Type
        {
            get { return this.valueType; }
        }

        public Task<object> GetValueAsync()
        {
            return this.valueAsTask;
        }

        public string ToInvokeString()
        {
            return this.value?.ToString();
        }
    }
}
