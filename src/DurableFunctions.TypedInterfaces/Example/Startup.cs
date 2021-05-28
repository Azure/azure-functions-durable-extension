// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using WebJobs.Extensions.DurableTask.CodeGen.Example;

[assembly: WebJobsStartup(typeof(Startup))]
namespace WebJobs.Extensions.DurableTask.CodeGen.Example
{
    public class Startup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
        }
    }
}
