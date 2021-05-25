using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    public class TypedDurableClientBindingProvider : IBindingProvider
    {
        private readonly Func<DurableClientAttribute, IDurableClient> clientGenerator;
        private readonly TypedCodeProvider typedCodeProvider;

        internal TypedDurableClientBindingProvider(
            TypedCodeProvider typedCodeProvider,
            Func<DurableClientAttribute, IDurableClient> clientGenerator)
        {
            this.clientGenerator = clientGenerator;
            this.typedCodeProvider = typedCodeProvider;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var parameter = context.Parameter;
            var parameterType = parameter.ParameterType;

            if (parameterType.Name != TypedCodeProvider.ITypedDurableClient)
            {
                return Task.FromResult<IBinding>(null);
            }

            DurableClientAttribute clientAttr = parameter.GetCustomAttribute<DurableClientAttribute>(inherit: false);
            if (clientAttr == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            var client = this.clientGenerator(clientAttr);
            if (client == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            return Task.FromResult<IBinding>(new TypedDurableClientBinding(this.typedCodeProvider, client, parameter));
        }

        private class TypedDurableClientValueProvider : IValueProvider
        {
            private object client;

            public TypedDurableClientValueProvider(Type type, object client)
            {
                this.Type = type;
                this.client = client;
            }

            public Type Type { get; }

            public Task<object> GetValueAsync()
            {
                return Task.FromResult(this.client);
            }

            public string ToInvokeString()
            {
                return "invoke-string";
            }
        }

        private class TypedDurableClientBinding : IBinding
        {
            private readonly IDurableClient client;
            private readonly TypedCodeProvider typedCodeProvider;
            private readonly ParameterInfo parameterInfo;

            public TypedDurableClientBinding(
                TypedCodeProvider typedCodeProvider,
                IDurableClient client,
                ParameterInfo parameterInfo)
            {
                this.client = client;
                this.typedCodeProvider = typedCodeProvider;
                this.parameterInfo = parameterInfo;
            }

            public bool FromAttribute => false;

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                var type = this.typedCodeProvider.TypedDurableClientType;
                var client = this.typedCodeProvider.InstantiateTypedDurableClient(this.client);

                var valueProvider = new TypedDurableClientValueProvider(type, client);

                return Task.FromResult<IValueProvider>(valueProvider);
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                var type = this.typedCodeProvider.TypedDurableClientType;
                var client = this.typedCodeProvider.InstantiateTypedDurableClient(this.client);

                var valueProvider = new TypedDurableClientValueProvider(type, client);

                return Task.FromResult<IValueProvider>(valueProvider);
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor { Name = this.parameterInfo.Name };
            }
        }
    }
}
