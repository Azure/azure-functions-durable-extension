using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    public class GeneratedDurableClientBindingProvider : IBindingProvider
    {
        private readonly Func<DurableClientAttribute, IDurableClient> clientGenerator;
        private readonly GeneratedCodeProvider generatedCodeProvider;

        internal GeneratedDurableClientBindingProvider(
            GeneratedCodeProvider generatedCodeProvider,
            Func<DurableClientAttribute, IDurableClient> clientGenerator)
        {
            this.clientGenerator = clientGenerator;
            this.generatedCodeProvider = generatedCodeProvider;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var parameter = context.Parameter;
            var parameterType = parameter.ParameterType;

            if (parameterType.Name != GeneratedCodeProvider.IGeneratedDurableClient)
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

            return Task.FromResult<IBinding>(new GeneratedDurableClientBinding(this.generatedCodeProvider, client, parameter));
        }

        private class GeneratedDurableClientValueProvider : IValueProvider
        {
            private object client;

            public GeneratedDurableClientValueProvider(Type type, object client)
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

        private class GeneratedDurableClientBinding : IBinding
        {
            private readonly IDurableClient client;
            private readonly GeneratedCodeProvider generatedCodeProvider;
            private readonly ParameterInfo parameterInfo;

            public GeneratedDurableClientBinding(
                GeneratedCodeProvider generatedCodeProvider,
                IDurableClient client,
                ParameterInfo parameterInfo)
            {
                this.client = client;
                this.generatedCodeProvider = generatedCodeProvider;
                this.parameterInfo = parameterInfo;
            }

            public bool FromAttribute => false;

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                var type = this.generatedCodeProvider.GeneratedDurableClientType;
                var client = this.generatedCodeProvider.InstantiateGeneratedDurableClient(this.client);

                var valueProvider = new GeneratedDurableClientValueProvider(type, client);

                return Task.FromResult<IValueProvider>(valueProvider);
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                var type = this.generatedCodeProvider.GeneratedDurableClientType;
                var client = this.generatedCodeProvider.InstantiateGeneratedDurableClient(this.client);

                var valueProvider = new GeneratedDurableClientValueProvider(type, client);

                return Task.FromResult<IValueProvider>(valueProvider);
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor { Name = this.parameterInfo.Name };
            }
        }
    }
}
