using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

[assembly: FunctionsStartup(typeof(DurableClientSampleFunctionApp.Startup))]

namespace DurableClientSampleFunctionApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddDurableTask();
        }
    }
}
