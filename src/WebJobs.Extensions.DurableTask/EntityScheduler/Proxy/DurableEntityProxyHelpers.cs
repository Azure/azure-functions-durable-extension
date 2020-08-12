// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class DurableEntityProxyHelpers
    {
        private static readonly ConcurrentDictionary<Type, Type> EntityNameMappings = new ConcurrentDictionary<Type, Type>();

        internal static string ResolveEntityName<TEntityInterface>()
        {
            var type = EntityNameMappings.GetOrAdd(typeof(TEntityInterface), CreateTypeMapping);

            return type.Name;
        }

        private static Type CreateTypeMapping(Type interfaceType)
        {
            var implementedTypes = interfaceType.Assembly
                                                .GetTypes()
                                                .Where(x => x.IsClass && !x.IsAbstract && interfaceType.IsAssignableFrom(x))
                                                .ToArray();

            if (!implementedTypes.Any())
            {
                throw new InvalidOperationException($"Could not find any types that implement {interfaceType.FullName}.");
            }

            if (implementedTypes.Length > 1)
            {
                throw new InvalidOperationException($"Found multiple types that implement {interfaceType.FullName}. Only one type is allowed to implement an interface used for entity proxy creation.");
            }

            return implementedTypes[0];
        }
    }
}
