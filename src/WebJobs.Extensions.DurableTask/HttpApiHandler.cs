// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class HttpApiHandler
    {
        private const string InstancesControllerSegment = "/instances/";
        private const string OrchestratorsControllerSegment = "/orchestrators/";
        private const string TaskHubParameter = "taskHub";
        private const string ConnectionParameter = "connection";
        private const string RaiseEventOperation = "raiseEvent";
        private const string TerminateOperation = "terminate";
        private const string RewindOperation = "rewind";
        private const string ShowHistoryParameter = "showHistory";
        private const string ShowHistoryOutputParameter = "showHistoryOutput";
        private const string ShowInputParameter = "showInput";
        private const string CreatedTimeFromParameter = "createdTimeFrom";
        private const string CreatedTimeToParameter = "createdTimeTo";
        private const string RuntimeStatusParameter = "runtimeStatus";
        private const string PageSizeParameter = "top";

        private readonly DurableTaskExtension config;
        private readonly ILogger logger;

        public HttpApiHandler(DurableTaskExtension config, ILogger logger)
        {
            this.config = config;
            this.logger = logger;
        }

        internal HttpResponseMessage CreateCheckStatusResponse(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName);
            return this.CreateCheckStatusResponseMessage(request, httpManagementPayload.Id, httpManagementPayload.StatusQueryGetUri, httpManagementPayload.SendEventPostUri, httpManagementPayload.TerminatePostUri, httpManagementPayload.RewindPostUri);
        }

        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHub,
            string connectionName)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(null, instanceId, taskHub, connectionName);
            return httpManagementPayload;
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
                        return request.CreateResponse(HttpStatusCode.OK, status.Output);
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
                    return this.CreateCheckStatusResponseMessage(request, instanceId, httpManagementPayload.StatusQueryGetUri, httpManagementPayload.SendEventPostUri, httpManagementPayload.TerminatePostUri, httpManagementPayload.RewindPostUri);
                }
            }
        }

        public async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
        {
            try
            {
                string path = request.RequestUri.AbsolutePath.TrimEnd('/');
                int i = path.IndexOf(OrchestratorsControllerSegment, StringComparison.OrdinalIgnoreCase);
                int nextSlash = -1;
                if (i >= 0 && request.Method == HttpMethod.Post)
                {
                    string functionName;
                    string instanceId = string.Empty;

                    i += OrchestratorsControllerSegment.Length;
                    nextSlash = path.IndexOf('/', i);

                    if (nextSlash < 0)
                    {
                        functionName = path.Substring(i);
                    }
                    else
                    {
                        functionName = path.Substring(i, nextSlash - i);
                        i = nextSlash + 1;
                        instanceId = path.Substring(i);
                    }

                    return await this.HandleStartOrchestratorRequestAsync(request, functionName, instanceId);
                }

                i = path.IndexOf(InstancesControllerSegment, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                {
                    // Retrive All Status or conditional query in case of the request URL ends e.g. /instances/
                    if (request.Method == HttpMethod.Get
                        && path.EndsWith(InstancesControllerSegment.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    {
                        return await this.HandleGetStatusRequestAsync(request);
                    }

                    return request.CreateResponse(HttpStatusCode.NotFound);
                }

                i += InstancesControllerSegment.Length;
                nextSlash = path.IndexOf('/', i);

                if (nextSlash < 0)
                {
                    string instanceId = path.Substring(i);
                    if (request.Method == HttpMethod.Get)
                    {
                        return await this.HandleGetStatusRequestAsync(request, instanceId);
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
                            return await this.HandleTerminateInstanceRequestAsync(request, instanceId);
                        }
                        else if (string.Equals(operation, RewindOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleRewindInstanceRequestAsync(request, instanceId);
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

                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "No such API");
            }

            /* Some handler methods throw ArgumentExceptions in specialized cases which should be returned to the client, such as when:
             *     - the function name is not found (starting a new function)
             *     - the orchestration instance is not in a Failed state (rewinding an orchestration instance)
            */
            catch (ArgumentException e)
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "One or more of the arguments submitted is incorrect", e);
            }
            catch (Exception e)
            {
                return request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Something went wrong while processing your request", e);
            }
        }

        private async Task<HttpResponseMessage> HandleGetStatusRequestAsync(
            HttpRequestMessage request)
        {
            DurableOrchestrationClientBase client = this.GetClient(request);
            var queryNameValuePairs = request.GetQueryNameValuePairs();
            var createdTimeFrom = GetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeFromParameter, default(DateTime));
            var createdTimeTo = GetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeToParameter, default(DateTime));
            var runtimeStatus = GetIEnumerableQueryParameterValue(queryNameValuePairs, RuntimeStatusParameter);
            var pageSize = GetIntQueryParameterValue(queryNameValuePairs, PageSizeParameter);

            var continuationToken = "";
            if (request.Headers.TryGetValues("x-ms-continuation-token", out var headerValues))
            {
                continuationToken = headerValues.FirstOrDefault();
            }

            IList<DurableOrchestrationStatus> statusForAllInstances;
            var nextContinuationToken = "";

            if (pageSize > 0)
            {
                var context = await client.GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus, pageSize, continuationToken);
                statusForAllInstances = context.DurableOrchestrationState.ToList();
                nextContinuationToken = context.ContinuationToken;
            }
            else
            {
                statusForAllInstances = await client.GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus);
            }

            var results = new List<StatusResponsePayload>(statusForAllInstances.Count);
            foreach (var state in statusForAllInstances)
            {
                results.Add(this.ConvertFrom(state));
            }

            var response = request.CreateResponse(HttpStatusCode.OK, results);

            response.Headers.Add("x-ms-continuation-token", nextContinuationToken);
            return response;
        }

        private async Task<HttpResponseMessage> HandleGetStatusRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            DurableOrchestrationClientBase client = this.GetClient(request);

            var queryNameValuePairs = request.GetQueryNameValuePairs();
            var showHistory = GetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryParameter, defaultValue: false);
            var showHistoryOutput = GetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryOutputParameter, defaultValue: false);

            bool showInput = GetBooleanQueryParameterValue(queryNameValuePairs, ShowInputParameter, defaultValue: true);

            var status = await client.GetStatusAsync(instanceId, showHistory, showHistoryOutput, showInput);
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
                request.CreateResponse(
                statusCode,
                this.ConvertFrom(status));

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

        private static IEnumerable<OrchestrationRuntimeStatus> GetIEnumerableQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName)
        {
            var results = new List<OrchestrationRuntimeStatus>();
            var parameters = queryStringNameValueCollection.GetValues(queryParameterName) ?? new string[] { };

            foreach (var value in parameters.SelectMany(x => x.Split(',')))
            {
                if (Enum.TryParse<OrchestrationRuntimeStatus>(value, out OrchestrationRuntimeStatus result))
                {
                    results.Add(result);
                }
            }

            return results;
        }

        private static DateTime GetDateTimeQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName, DateTime defaultDateTime)
        {
            var value = queryStringNameValueCollection[queryParameterName];
            return DateTime.TryParse(value, out DateTime dateTime) ? dateTime : defaultDateTime;
        }

        private static bool GetBooleanQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName, bool defaultValue)
        {
            var value = queryStringNameValueCollection[queryParameterName];
            return bool.TryParse(value, out bool parsedValue) ? parsedValue : defaultValue;
        }

        private static int GetIntQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName)
        {
            var value = queryStringNameValueCollection[queryParameterName];
            return int.TryParse(value, out var intValue) ? intValue : 0;
        }

        private async Task<HttpResponseMessage> HandleTerminateInstanceRequestAsync(
            HttpRequestMessage request,
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

        private async Task<HttpResponseMessage> HandleStartOrchestratorRequestAsync(
            HttpRequestMessage request,
            string functionName,
            string instanceId)
        {
            try
            {
                DurableOrchestrationClientBase client = this.GetClient(request);

                object input = null;
                if (request.Content != null)
                {
                    using (Stream s = await request.Content.ReadAsStreamAsync())
                    using (StreamReader sr = new StreamReader(s))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        JsonSerializer serializer = JsonSerializer.Create(MessagePayloadDataConverter.MessageSettings);
                        input = serializer.Deserialize<object>(reader);
                    }
                }

                string id = await client.StartNewAsync(functionName, instanceId, input);

                TimeSpan? timeout = GetTimeSpan(request, "timeout");
                TimeSpan? pollingInterval = GetTimeSpan(request, "pollingInterval");

                if (timeout.HasValue && pollingInterval.HasValue)
                {
                    return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, id, timeout.Value, pollingInterval.Value);
                }
                else
                {
                    return client.CreateCheckStatusResponse(request, id);
                }
            }
            catch (JsonReaderException e)
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid JSON content", e);
            }
        }

        private async Task<HttpResponseMessage> HandleRewindInstanceRequestAsync(
           HttpRequestMessage request,
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
                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Terminated:
                case OrchestrationRuntimeStatus.Completed:
                    return request.CreateResponse(HttpStatusCode.Gone);
            }

            string reason = request.GetQueryNameValuePairs()["reason"];

            await client.RewindAsync(instanceId, reason);

            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        private async Task<HttpResponseMessage> HandleRaiseEventRequestAsync(
            HttpRequestMessage request,
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

            string mediaType = request.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
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

        private DurableOrchestrationClientBase GetClient(HttpRequestMessage request)
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

        internal HttpCreationPayload GetInstanceCreationLinks()
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.config.Options.NotificationUrl;

            string hostUrl = notificationUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            string instancePrefix = baseUrl + OrchestratorsControllerSegment + "{functionName}[/{instanceId}]";

            string querySuffix = !string.IsNullOrEmpty(notificationUri.Query)
                ? notificationUri.Query.TrimStart('?')
                : string.Empty;

            HttpCreationPayload httpCreationPayload = new HttpCreationPayload
            {
                CreateNewInstancePostUri = instancePrefix + "?" + querySuffix,
                CreateAndWaitOnNewInstancePostUri = instancePrefix + "?timeout={timeoutInSeconds}&pollingInterval={intervalInSeconds}&" + querySuffix,
            };

            return httpCreationPayload;
        }

        private HttpManagementPayload GetClientResponseLinks(
            HttpRequestMessage request,
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.config.Options.NotificationUrl;
            Uri baseUri = request?.RequestUri ?? notificationUri;

            // e.g. http://{host}/admin/extensions/DurableTaskExtension?code={systemKey}
            string hostUrl = baseUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            string instancePrefix = baseUrl + InstancesControllerSegment + WebUtility.UrlEncode(instanceId);

            string taskHub = WebUtility.UrlEncode(taskHubName ?? this.config.Options.HubName);
            string connection = WebUtility.UrlEncode(connectionName ?? this.config.Options.AzureStorageConnectionStringName ?? ConnectionStringNames.Storage);

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
                RewindPostUri = instancePrefix + "/" + RewindOperation + "?reason={text}&" + querySuffix,
            };

            return httpManagementPayload;
        }

        private HttpResponseMessage CreateCheckStatusResponseMessage(HttpRequestMessage request, string instanceId, string statusQueryGetUri, string sendEventPostUri, string terminatePostUri, string rewindPostUri)
        {
            var response = request.CreateResponse(
                HttpStatusCode.Accepted,
                new
                {
                    id = instanceId,
                    statusQueryGetUri,
                    sendEventPostUri,
                    terminatePostUri,
                    rewindPostUri,
                });

            // Implement the async HTTP 202 pattern.
            response.Headers.Location = new Uri(statusQueryGetUri);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return response;
        }

        private void ThrowIfWebhooksNotConfigured()
        {
            if (this.config.Options.NotificationUrl == null)
            {
                throw new InvalidOperationException("Webhooks are not configured");
            }
        }

        private static TimeSpan? GetTimeSpan(HttpRequestMessage request, string queryParameterName)
        {
            string queryParameterStringValue = request.GetQueryNameValuePairs()[queryParameterName];
            if (string.IsNullOrEmpty(queryParameterStringValue))
            {
                return null;
            }

            return TimeSpan.FromSeconds(double.Parse(queryParameterStringValue));
        }
    }
}
