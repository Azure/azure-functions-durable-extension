using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

[assembly: FunctionsStartup(typeof(DurableClientSampleFunctionApp.Startup))]

namespace DurableClientSampleFunctionApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // AddDurableClientFactory() registers IDurableClientFactory as a service so the application
            // can consume it and and call the Durable Client APIs
            builder.Services.AddDurableClientFactory();
        }
    }
}
