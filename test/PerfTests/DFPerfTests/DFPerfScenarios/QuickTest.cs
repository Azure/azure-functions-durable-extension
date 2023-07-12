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
    public class QuickTest
    {
        [FunctionName("QuickTest")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            var testObject = new
            {
                Name = "StartNew",
                Hello = "World",
            };

            object input = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(testObject));

            string instanceId = await starter.StartNewAsync("HelloWorldOrchestrator", input);
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
