// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class ReadOnlyConfigurationValue : IConfigurationSection
    {
        private readonly string value;

        public ReadOnlyConfigurationValue(string path, string value)
        {
            this.Path = path ?? throw new ArgumentNullException(nameof(path));
            this.Key = GetKey(path);
            this.value = value;
        }

        public string Key { get; }

        public string Path { get; }

        public string Value
        {
            get => this.value;
            set => throw new NotSupportedException("Configuration section is read-only.");
        }

        public string this[string key]
        {
            get => null;
            set => throw new NotSupportedException("Configuration section is read-only.");
        }

        public IEnumerable<IConfigurationSection> GetChildren() =>
            Enumerable.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken()
            => new ChangeToken();

        public IConfigurationSection GetSection(string key) =>
            new ReadOnlyConfigurationValue(this.Path + ':' + key, null); // This behavior mimics ConfigurationSection

        private static string GetKey(string path)
        {
            int i = path.LastIndexOf(':');
            return i >= 0 ? path.Substring(i + 1) : path;
        }

        private sealed class ChangeToken : IChangeToken
        {
            public bool HasChanged => false;

            public bool ActiveChangeCallbacks => true; // Clients do not need poll

            public IDisposable RegisterChangeCallback(Action<object> callback, object state) =>
                new CallbackRegistration();

            private sealed class CallbackRegistration : IDisposable
            {
                public void Dispose()
                { }
            }
        }
    }
}
