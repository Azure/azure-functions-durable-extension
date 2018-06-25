// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
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
        private const string ShowHistoryParameter = "showHistory";
        private const string ShowHistoryOutputParameter = "showHistoryOutput";

        private readonly DurableTaskExtension config;
        private readonly ILogger logger;

        public HttpApiHandler(DurableTaskExtension config, ILogger logger)
        {
            this.config = config;
            this.logger = logger;
        }

        internal async Task<HttpResponse> CreateCheckStatusResponse(
            HttpRequest request,
            string instanceId,
            OrchestrationClientAttribute attribute)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName);
            return await this.CreateCheckStatusResponseMessage(request, httpManagementPayload.Id, httpManagementPayload.StatusQueryGetUri, httpManagementPayload.SendEventPostUri, httpManagementPayload.TerminatePostUri);
        }

        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHub,
            string connectionName)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(null, instanceId, taskHub, connectionName);
            return httpManagementPayload;
        }

        internal async Task<HttpResponse> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequest request,
            string instanceId,
            OrchestrationClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            if (retryInterval > timeout)
            {
                throw new ArgumentException($"Total timeout {timeout.TotalSeconds} should be bigger than retry timeout {retryInterval.TotalSeconds}");
            }

            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName);

            DurableOrchestrationClientBase client = this.GetClient(request);
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
                if (status != null)
                {
                    if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
                    {
                        return await request.CreateResponse(HttpStatusCode.OK, status.Output);
                    }

                    if (status.RuntimeStatus == OrchestrationRuntimeStatus.Canceled ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                    {
                        return await this.HandleGetStatusRequestAsync(request, instanceId);
                    }
                }

                TimeSpan elapsed = stopwatch.Elapsed;
                if (elapsed < timeout)
                {
                    TimeSpan remainingTime = timeout.Subtract(elapsed);
                    await Task.Delay(remainingTime > retryInterval ? retryInterval : remainingTime);
                }
                else
                {
                    return await this.CreateCheckStatusResponseMessage(request, instanceId, httpManagementPayload.StatusQueryGetUri, httpManagementPayload.SendEventPostUri, httpManagementPayload.TerminatePostUri);
                }
            }
        }

        public async Task<HttpResponse> HandleRequestAsync(HttpRequest request)
        {
            string path = request.Path.ToString().TrimEnd('/');
            int i = path.IndexOf(InstancesControllerSegment, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                // Retrive All Status in case of the request URL ends e.g. /instances/
                if (request.Method == "GET"
                    && path.EndsWith(InstancesControllerSegment.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                {
                    return await this.HandleGetStatusRequestAsync(request);
                }

                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            i += InstancesControllerSegment.Length;
            int nextSlash = path.IndexOf('/', i);

            if (nextSlash < 0)
            {
                string instanceId = path.Substring(i);
                if (request.Method == "Get")
                {
                    return await this.HandleGetStatusRequestAsync(request, instanceId);
                }
            }
            else if (request.Method == "Post")
            {
                string instanceId = path.Substring(i, nextSlash - i);
                i = nextSlash + 1;
                nextSlash = path.IndexOf('/', i);
                if (nextSlash < 0)
                {
                    string operation = path.Substring(i);
                    if (string.Equals(operation, TerminateOperation, StringComparison.OrdinalIgnoreCase))
                    {
                        return await this.HandleTerminateInstanceRequestAsync(request, instanceId);
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
                            return await this.HandleRaiseEventRequestAsync(request, instanceId, eventName);
                        }
                    }
                }
            }

            return await request.CreateErrorResponse(HttpStatusCode.BadRequest, "No such API");
        }

        private async Task<HttpResponse> HandleGetStatusRequestAsync(
            HttpRequest request)
        {
            DurableOrchestrationClientBase client = this.GetClient(request);
            IList<DurableOrchestrationStatus> statusForAllInstances = await client.GetStatusAsync();

            var results = new List<StatusResponsePayload>(statusForAllInstances.Count);
            foreach (var state in statusForAllInstances)
            {
                results.Add(this.ConvertFrom(state));
            }

            return await request.CreateResponse(HttpStatusCode.OK, results);
        }

        private async Task<HttpResponse> HandleGetStatusRequestAsync(
            HttpRequest request,
            string instanceId)
        {
            DurableOrchestrationClientBase client = this.GetClient(request);

            var queryNameValuePairs = request.GetQueryNameValuePairs();
            var showHistory = GetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryParameter);
            var showHistoryOutput = GetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryOutputParameter);
            var status = await client.GetStatusAsync(instanceId, showHistory, showHistoryOutput);
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
                    location = new Uri(request.GetDisplayUrl());
                    break;

                // The orchestration has failed - return 500 w/out Location header
                case OrchestrationRuntimeStatus.Failed:
                    statusCode = HttpStatusCode.InternalServerError;
                    location = null;
                    break;

                // The orchestration is not running - return 200 w/out Location header
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    statusCode = HttpStatusCode.OK;
                    location = null;
                    break;
                default:
                    this.logger.LogError($"Unknown runtime state '{status.RuntimeStatus}'.");
                    statusCode = HttpStatusCode.InternalServerError;
                    location = null;
                    break;
            }

            var response =
                await request.CreateResponse(
                statusCode,
                this.ConvertFrom(status));

            if (location != null)
            {
               response.Headers.Add("Location", location.ToString());
            }

            if (statusCode == HttpStatusCode.Accepted)
            {
                // Ask for 5 seconds before retry. Some clients will otherwise retry in a tight loop.
                RetryConditionHeaderValue retryConditionHeaderValue =
                    new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
                response.Headers.Add("RetryAfter", retryConditionHeaderValue.ToString());
            }

            return response;
        }

        private StatusResponsePayload ConvertFrom(DurableOrchestrationStatus status)
        {
            return new StatusResponsePayload
            {
                InstanceId = status.InstanceId,
                RuntimeStatus = status.RuntimeStatus.ToString(),
                Input = status.Input,
                CustomStatus = status.CustomStatus,
                Output = status.Output,
                CreatedTime = status.CreatedTime.ToString("s") + "Z",
                LastUpdatedTime = status.LastUpdatedTime.ToString("s") + "Z",
                HistoryEvents = status.History,
            };
        }

        private static bool GetBooleanQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName)
        {
            var value = queryStringNameValueCollection[queryParameterName];
            return bool.TryParse(value, out bool parsedValue) && parsedValue;
        }

        private async Task<HttpResponse> HandleTerminateInstanceRequestAsync(
            HttpRequest request,
            string instanceId)
        {
            DurableOrchestrationClientBase client = this.GetClient(request);

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

        private async Task<HttpResponse> HandleRaiseEventRequestAsync(
            HttpRequest request,
            string instanceId,
            string eventName)
        {
            DurableOrchestrationClientBase client = this.GetClient(request);

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

            string mediaType = request.Headers["ContentType"];
            if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return await request.CreateErrorResponse(HttpStatusCode.BadRequest, "Only application/json request content is supported");
            }

            string stringData;
            using (StreamReader reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                stringData = await reader.ReadToEndAsync();
            }

            object eventData;
            try
            {
               eventData = !string.IsNullOrEmpty(stringData) ? JToken.Parse(stringData) : null;
            }
            catch (JsonReaderException e)
            {
                return await request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid JSON content", e);
            }

            await client.RaiseEventAsync(instanceId, eventName, eventData);
            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        private DurableOrchestrationClientBase GetClient(HttpRequest request)
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

            return this.GetClient(attribute);
        }

        // protected virtual to allow mocking in unit tests.
        protected virtual DurableOrchestrationClientBase GetClient(OrchestrationClientAttribute attribute)
        {
            return this.config.GetClient(attribute);
        }

        private HttpManagementPayload GetClientResponseLinks(
            HttpRequest request,
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            if (this.config.NotificationUrl == null)
            {
                throw new InvalidOperationException("Webhooks are not configured");
            }

            Uri notificationUri = this.config.NotificationUrl;
            Uri baseUri;
            if (request?.GetDisplayUrl() != null)
            {
                baseUri = new Uri(request.GetDisplayUrl());
            }
            else
            {
                baseUri = notificationUri;
            }

            // e.g. http://{host}/admin/extensions/DurableTaskExtension?code={systemKey}
            string hostUrl = baseUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            string instancePrefix = baseUrl + InstancesControllerSegment + WebUtility.UrlEncode(instanceId);

            string taskHub = WebUtility.UrlEncode(taskHubName ?? this.config.HubName);
            string connection = WebUtility.UrlEncode(connectionName ?? this.config.AzureStorageConnectionStringName ?? ConnectionStringNames.Storage);

            string querySuffix = $"{TaskHubParameter}={taskHub}&{ConnectionParameter}={connection}";
            if (!string.IsNullOrEmpty(notificationUri.Query))
            {
                // This is expected to include the auto-generated system key for this extension.
                querySuffix += "&" + notificationUri.Query.TrimStart('?');
            }

            HttpManagementPayload httpManagementPayload = new HttpManagementPayload
            {
                Id = instanceId,
                StatusQueryGetUri = instancePrefix + "?" + querySuffix,
                SendEventPostUri = instancePrefix + "/" + RaiseEventOperation + "/{eventName}?" + querySuffix,
                TerminatePostUri = instancePrefix + "/" + TerminateOperation + "?reason={text}&" + querySuffix,
            };

            return httpManagementPayload;
        }

        private async Task<HttpResponse> CreateCheckStatusResponseMessage(HttpRequest request, string instanceId, string statusQueryGetUri, string sendEventPostUri, string terminatePostUri)
        {
            dynamic httpWebhooks = new
            {
                id = instanceId,
                statusQueryGetUri,
                sendEventPostUri,
                terminatePostUri,
            };
            var response = new DefaultHttpResponse(request.HttpContext)
            {
                StatusCode = 202,
                ContentType = "application/json",
            };

            var json = JsonConvert.SerializeObject(httpWebhooks);
            response.Body = new MemoryStream();
            await HttpResponseWritingExtensions.WriteAsync(response, json, Encoding.UTF8);
            return response;
        }
    }
}
