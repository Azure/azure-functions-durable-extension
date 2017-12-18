// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class HttpApiHandler
    {
        private const string InstancesControllerSegment = "/instances/";
        private const string TaskHubParameter = "taskHub";
        private const string ConnectionParameter = "connection";
        private const string RaiseEventOperation = "raiseEvent";
        private const string TerminateOperation = "terminate";
        private const string TimeoutParameter = "timeout";
        private const string RetryIntervalParameter = "retryInterval";

        private const int DefaultTimeoutSeconds = 10;
        private const int DefaultRetryIntervalSeconds = 1;

        private readonly DurableTaskExtension config;
        private readonly TraceWriter traceWriter;

        public HttpApiHandler(DurableTaskExtension config, TraceWriter traceWriter)
        {
            this.config = config;
            this.traceWriter = traceWriter;
        }

        internal HttpResponseMessage CreateCheckStatusResponse(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute)
        {
            this.GetClientResponseLinks(request, instanceId, attribute, out var statusQueryGetUri, out var sendEventPostUri, out var terminatePostUri);
            return this.CreateCheckStatusResponseMessage(request, instanceId, statusQueryGetUri, sendEventPostUri, terminatePostUri);
        }


        internal async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            if (retryInterval > timeout)
            {
                throw new ArgumentException($"Total timeout {timeout.TotalSeconds} should be bigger than retry timeout {retryInterval.TotalSeconds}");
            }

            this.GetClientResponseLinks(request, instanceId, attribute, out var statusQueryGetUri, out var sendEventPostUri, out var terminatePostUri);

            var client = this.GetClient(request);
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var status = await client.GetStatusAsync(instanceId);
                if (status != null && status.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
                {
                    return request.CreateResponse(HttpStatusCode.OK, status.Output);
                }
                if (status != null && (status.RuntimeStatus == OrchestrationRuntimeStatus.Canceled || status.RuntimeStatus == OrchestrationRuntimeStatus.Failed || status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated))
                {
                    return await this.HandleGetStatusRequestAsync(request, instanceId);
                }
                
                var elapsed = stopwatch.Elapsed;
                if (elapsed < timeout)
                {
                    var remainingTime = timeout.Subtract(elapsed);
                    await Task.Delay(remainingTime > retryInterval ? retryInterval : remainingTime);
                }
                else
                {
                    return this.CreateCheckStatusResponseMessage(request, instanceId, statusQueryGetUri, sendEventPostUri, terminatePostUri);
                }
            }
        }


        public async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
        {
            string path = request.RequestUri.AbsolutePath.TrimEnd('/');
            int i = path.IndexOf(InstancesControllerSegment);
            if (i < 0)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            i += InstancesControllerSegment.Length;
            int nextSlash = path.IndexOf('/', i);

            if (nextSlash < 0)
            {
                string instanceId = path.Substring(i);
                if (request.Method == HttpMethod.Get)
                {
                    return await HandleGetStatusRequestAsync(request, instanceId);
                }
            }
            else if (request.Method == HttpMethod.Post)
            {
                string instanceId = path.Substring(i, nextSlash - i);
                i = nextSlash + 1;
                nextSlash = path.IndexOf('/', i);
                if (nextSlash < 0)
                {
                    string operation = path.Substring(i);
                    if (string.Equals(operation, TerminateOperation, StringComparison.OrdinalIgnoreCase))
                    {
                        return await HandleTerminateInstanceRequestAsync(request, instanceId);
                    }
                }
                else
                {
                    string operation = path.Substring(i, nextSlash - i);
                    if (string.Equals(operation, RaiseEventOperation, StringComparison.OrdinalIgnoreCase))
                    {
                        i = nextSlash + 1;
                        nextSlash = path.IndexOf('/', i);
                        if (nextSlash < 0)
                        {
                            string eventName = path.Substring(i);
                            return await HandleRaiseEventRequestAsync(request, instanceId, eventName);
                        }
                    }
                }
            }

            return request.CreateErrorResponse(HttpStatusCode.BadRequest, "No such API");
        }

        private async Task<HttpResponseMessage> HandleGetStatusRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            DurableOrchestrationClient client = this.GetClient(request);

            var status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            HttpStatusCode statusCode;
            Uri location;

            switch (status.RuntimeStatus)
            {
                // The orchestration is running - return 202 w/Location header
                case OrchestrationRuntimeStatus.Running:
                case OrchestrationRuntimeStatus.Pending:
                case OrchestrationRuntimeStatus.ContinuedAsNew:
                    statusCode = HttpStatusCode.Accepted;
                    location = request.RequestUri;
                    break;
                // The orchestration is not running - return 202 w/out Location header
                case OrchestrationRuntimeStatus.Failed:
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    statusCode = HttpStatusCode.OK;
                    location = null;
                    break;
                default:
                    this.traceWriter.Error($"Unknown runtime state '{status.RuntimeStatus}'.");
                    statusCode = HttpStatusCode.InternalServerError;
                    location = null;
                    break;
            }

            var response = request.CreateResponse(
                statusCode,
                new
                {
                    runtimeStatus = status.RuntimeStatus.ToString(),
                    input = status.Input,
                    output = status.Output,
                    createdTime = status.CreatedTime.ToString("s") + "Z",
                    lastUpdatedTime = status.LastUpdatedTime.ToString("s") + "Z",
                });

            if (location != null)
            {
                response.Headers.Location = location;
            }

            if (statusCode == HttpStatusCode.Accepted)
            {
                // Ask for 5 seconds before retry. Some clients will otherwise retry in a tight loop.
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
            }

            return response;
        }

        private async Task<HttpResponseMessage> HandleTerminateInstanceRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            DurableOrchestrationClient client = this.GetClient(request);

            var status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            switch (status.RuntimeStatus)
            {
                case OrchestrationRuntimeStatus.Failed:
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    return request.CreateResponse(HttpStatusCode.Gone);
            }

            string reason = request.GetQueryNameValuePairs()["reason"];

            await client.TerminateAsync(instanceId, reason);

            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        private async Task<HttpResponseMessage> HandleRaiseEventRequestAsync(
            HttpRequestMessage request,
            string instanceId,
            string eventName)
        {
            DurableOrchestrationClient client = this.GetClient(request);

            var status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            switch (status.RuntimeStatus)
            {
                case OrchestrationRuntimeStatus.Failed:
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    return request.CreateResponse(HttpStatusCode.Gone);
            }

            var mediaType = request.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "Only application/json request content is supported");
            }

            var stringData = await request.Content.ReadAsStringAsync();

            object eventData;
            try
            {
                eventData = !string.IsNullOrEmpty(stringData) ? JToken.Parse(stringData) : null;
            }
            catch (JsonReaderException e)
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid JSON content", e);
            }

            await client.RaiseEventAsync(instanceId, eventName, eventData);
            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        private DurableOrchestrationClient GetClient(HttpRequestMessage request)
        {
            string taskHub = null; 
            string connectionName = null;

            var pairs = request.GetQueryNameValuePairs();
            foreach (var key in pairs.AllKeys)
            {
              if (taskHub == null 
                    && key.Equals(TaskHubParameter, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pairs[key]))
                {
                    taskHub = pairs[key];
                }
                else if (connectionName == null
                    && key.Equals(ConnectionParameter, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pairs[key]))
                {
                    connectionName = pairs[key];
                }
             }

            var attribute = new OrchestrationClientAttribute
            {
                TaskHub = taskHub,
                ConnectionName = connectionName,
            };

            return this.config.GetClient(attribute);
        }

        private void GetClientResponseLinks(
           HttpRequestMessage request,
           string instanceId,
           OrchestrationClientAttribute attribute,
           out string statusQueryGetUri,
           out string sendEventPostUri,
           out string terminatePostUri)
        {
            if (this.config.NotificationUrl == null)
            {
                throw new InvalidOperationException("Webhooks are not configured");
            }

            var notificationUri = this.config.NotificationUrl;

            // e.g. http://{host}/admin/extensions/DurableTaskExtension?code={systemKey}
            var hostUrl = request.RequestUri.GetLeftPart(UriPartial.Authority);
            var baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            var instancePrefix = baseUrl + InstancesControllerSegment + WebUtility.UrlEncode(instanceId);

            var taskHub = WebUtility.UrlEncode(attribute.TaskHub ?? config.HubName);
            var connection = WebUtility.UrlEncode(attribute.ConnectionName ?? config.AzureStorageConnectionStringName ?? ConnectionStringNames.Storage);

            var querySuffix = $"{TaskHubParameter}={taskHub}&{ConnectionParameter}={connection}";
            if (!string.IsNullOrEmpty(notificationUri.Query))
            {
                // This is expected to include the auto-generated system key for this extension.
                querySuffix += "&" + notificationUri.Query.TrimStart('?');
            }

            statusQueryGetUri = instancePrefix + "?" + querySuffix;
            sendEventPostUri = instancePrefix + "/" + RaiseEventOperation + "/{eventName}?" + querySuffix;
            terminatePostUri = instancePrefix + "/" + TerminateOperation + "?reason={text}&" + querySuffix;
        }

        private HttpResponseMessage CreateCheckStatusResponseMessage(HttpRequestMessage request, string instanceId, string statusQueryGetUri, string sendEventPostUri, string terminatePostUri)
        {
            var response = request.CreateResponse(
                HttpStatusCode.Accepted,
                new
                {
                    id = instanceId,
                    statusQueryGetUri,
                    sendEventPostUri,
                    terminatePostUri
                });

            // Implement the async HTTP 202 pattern.
            response.Headers.Location = new Uri(statusQueryGetUri);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return response;
        }
    }
}
