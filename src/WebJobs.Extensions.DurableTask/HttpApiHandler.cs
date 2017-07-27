// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
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
            if (this.config.NotificationUrl == null)
            {
                throw new InvalidOperationException("Webhooks are not configured");
            }

            // e.g. http://{host}/admin/extensions/DurableTaskExtension
            string hostUrl = request.RequestUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + this.config.NotificationUrl.AbsolutePath.TrimEnd('/');
            string instancePrefix = baseUrl + InstancesControllerSegment + WebUtility.UrlEncode(instanceId);

            string taskHub = WebUtility.UrlEncode(attribute.TaskHub ?? config.HubName);
            string connection = WebUtility.UrlEncode(attribute.ConnectionName ?? config.AzureStorageConnectionStringName ?? ConnectionStringNames.Storage);
            string querySuffix = $"{TaskHubParameter}={taskHub}&{ConnectionParameter}={connection}";

            Uri statusQueryGetUri = new Uri(instancePrefix + "?" + querySuffix);
            Uri sendEventPostUri = new Uri(instancePrefix + "/" + RaiseEventOperation + "/{eventName}?" + querySuffix);
            Uri terminatePostUri = new Uri(instancePrefix + "/" + TerminateOperation + "?reason={text}&" + querySuffix);

            HttpResponseMessage response = request.CreateResponse(
                HttpStatusCode.Accepted,
                new
                {
                    id = instanceId,
                    statusQueryGetUri = statusQueryGetUri,
                    sendEventPostUri = sendEventPostUri,
                    terminatePostUri = terminatePostUri
                });
            response.Headers.Location = statusQueryGetUri;
            return response;
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

            switch (status.OrchestrationStatus)
            {
                case OrchestrationStatus.Running:
                case OrchestrationStatus.Pending:
                    statusCode = HttpStatusCode.Accepted;
                    location = request.RequestUri;
                    break;
                case OrchestrationStatus.Failed:
                case OrchestrationStatus.Canceled:
                case OrchestrationStatus.Terminated:
                    statusCode = HttpStatusCode.BadRequest;
                    location = null;
                    break;
                case OrchestrationStatus.Completed:
                    statusCode = HttpStatusCode.OK;
                    location = null;
                    break;
                default:
                    this.traceWriter.Error($"Unknown runtime state '{status.OrchestrationStatus}'.");
                    statusCode = HttpStatusCode.InternalServerError;
                    location = null;
                    break;
            }

            var response = request.CreateResponse(
                statusCode,
                new
                {
                    runtimeStatus = status.RuntimeStatus,
                    input = status.Input,
                    output = status.Output,
                    createdTime = status.CreatedTime.ToString("s") + "Z",
                    lastUpdatedTime = status.LastUpdatedTime.ToString("s") + "Z",
                });

            if (location != null)
            {
                response.Headers.Location = location;
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

            switch (status.OrchestrationStatus)
            {
                case OrchestrationStatus.Failed:
                case OrchestrationStatus.Canceled:
                case OrchestrationStatus.Terminated:
                case OrchestrationStatus.Completed:
                    return request.CreateResponse(HttpStatusCode.Gone);
            }

            string reason = request.GetQueryNameValuePairs().FirstOrDefault(
                pair => pair.Key.Equals("reason", StringComparison.OrdinalIgnoreCase)).Value;

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

            switch (status.OrchestrationStatus)
            {
                case OrchestrationStatus.Failed:
                case OrchestrationStatus.Canceled:
                case OrchestrationStatus.Terminated:
                case OrchestrationStatus.Completed:
                    return request.CreateResponse(HttpStatusCode.Gone);
            }

            string mediaType = request.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrEmpty(mediaType) && 
                !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "Only application/json request content is supported");
            }

            string stringData = await request.Content.ReadAsStringAsync();

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

            foreach (var pair in request.GetQueryNameValuePairs())
            {
                if (taskHub == null 
                    && pair.Key.Equals(TaskHubParameter, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    taskHub = pair.Value;
                }
                else if (connectionName == null 
                    && pair.Key.Equals(ConnectionParameter, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    connectionName = pair.Value;
                }
            }

            var attribute = new OrchestrationClientAttribute
            {
                TaskHub = taskHub,
                ConnectionName = connectionName,
            };

            return this.config.GetClient(attribute);
        }
    }
}
