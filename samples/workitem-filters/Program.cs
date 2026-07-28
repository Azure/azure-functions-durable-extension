using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);
builder.Build().Run();
