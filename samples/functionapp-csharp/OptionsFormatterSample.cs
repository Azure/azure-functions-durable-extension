using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableClientSampleFunctionApp
{
    /// <summary>
    /// Sample demonstrating how to use IOptionsFormatter with DurableTaskOptions.
    /// IOptionsFormatter provides a way to format configuration options as JSON for diagnostics and logging.
    /// This is automatically used by Azure Functions infrastructure for troubleshooting.
    /// </summary>
    public class OptionsFormatterSample
    {
        private readonly DurableTaskOptions durableTaskOptions;

        public OptionsFormatterSample(IOptions<DurableTaskOptions> options)
        {
            this.durableTaskOptions = options.Value;
        }

        /// <summary>
        /// HTTP trigger function that demonstrates retrieving and logging formatted DurableTaskOptions.
        /// This pattern is useful for diagnostics and troubleshooting configuration issues.
        /// </summary>
        /// <example>
        /// GET http://localhost:7071/api/GetDurableTaskOptions
        /// </example>
        [FunctionName("GetDurableTaskOptions")]
        public IActionResult GetFormattedOptions(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Getting formatted DurableTaskOptions for diagnostics");

            // Cast to IOptionsFormatter to access the Format() method
            // Note: DurableTaskOptions implements IOptionsFormatter (added in version 3.x)
            IOptionsFormatter formatter = (IOptionsFormatter)this.durableTaskOptions;

            // Get the formatted JSON representation of the options
            string formattedOptions = formatter.Format();

            // Log the formatted options for diagnostics
            log.LogInformation("Current DurableTaskOptions configuration:\n{FormattedOptions}", formattedOptions);

            // Return the formatted options in the response
            return new ContentResult
            {
                Content = formattedOptions,
                ContentType = "application/json",
                StatusCode = 200
            };
        }

        /// <summary>
        /// Example showing how to use formatted options in custom diagnostics or monitoring.
        /// </summary>
        [FunctionName("LogDurableTaskOptionsOnStartup")]
        public IActionResult LogOptionsOnStartup(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            // This pattern can be used during application startup or health checks
            // to verify configuration is as expected

            // Cast to IOptionsFormatter to access the Format() method
            IOptionsFormatter formatter = (IOptionsFormatter)this.durableTaskOptions;
            string formattedOptions = formatter.Format();

            // Example: Log specific configuration values for monitoring
            log.LogInformation("Checking DurableTask configuration:");
            log.LogInformation("- HubName: {HubName}", this.durableTaskOptions.HubName);
            log.LogInformation("- MaxConcurrentActivityFunctions: {MaxActivity}", 
                this.durableTaskOptions.MaxConcurrentActivityFunctions);
            log.LogInformation("- MaxConcurrentOrchestratorFunctions: {MaxOrchestrator}", 
                this.durableTaskOptions.MaxConcurrentOrchestratorFunctions);
            log.LogInformation("- ExtendedSessionsEnabled: {ExtendedSessions}", 
                this.durableTaskOptions.ExtendedSessionsEnabled);

            // Full formatted output for detailed diagnostics
            log.LogDebug("Full configuration: {FormattedOptions}", formattedOptions);

            return new OkObjectResult(new
            {
                Message = "DurableTaskOptions logged successfully",
                HubName = this.durableTaskOptions.HubName
            });
        }
    }
}
