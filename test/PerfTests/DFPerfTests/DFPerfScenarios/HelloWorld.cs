using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DFPerfScenarios.DFPerfScenarios
{
    public class HelloWorld
    {
        [FunctionName("HelloWorldOrchestrator")]
        public string HelloWorldOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var input = ctx.GetInput<dynamic>();
            return $"{input.Hello}, {input.Name}";
        }

        [FunctionName("FailedActivity")]
        public string FailedActivity(
        [ActivityTrigger] IDurableActivityContext ctx)
        {
            return null;
        }
    }
}
