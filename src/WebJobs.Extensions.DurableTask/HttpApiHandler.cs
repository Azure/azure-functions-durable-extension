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
using DurableTask.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class HttpApiHandler
    {
        // Route segments. Note that these segments cannot start with `/` due to limitations with TemplateMatcher.
        private const string InstancesControllerSegment = "instances/";
        private const string OrchestratorsControllerSegment = "orchestrators/";
        private const string EntitiesControllerSegment = "entities/";

        // Route parameters
        private const string FunctionNameRouteParameter = "functionName";
        private const string InstanceIdRouteParameter = "instanceId";
        private const string EntityNameRouteParameter = "entityName";
        private const string EntityKeyRouteParameter = "entityKey";
        private const string OperationRouteParameter = "operation";
        private const string EventNameRouteParameter = "eventName";

        // Query string parameters
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

        // API Routes
        private static readonly TemplateMatcher StartOrchestrationRoute = GetStartOrchestrationRoute();
        private static readonly TemplateMatcher EntityRoute = GetEntityRoute();
        private static readonly TemplateMatcher InstancesRoute = GetInstancesRoute();
        private static readonly TemplateMatcher InstanceRaiseEventRoute = GetInstanceRaiseEventRoute();

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
            DurableClientAttribute attribute)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName);
            return this.CreateCheckStatusResponseMessage(
                request,
                httpManagementPayload.Id,
                httpManagementPayload.StatusQueryGetUri,
                httpManagementPayload.SendEventPostUri,
                httpManagementPayload.TerminatePostUri,
                httpManagementPayload.RewindPostUri,
                httpManagementPayload.PurgeHistoryDeleteUri);
        }

        // /orchestrators/{functionName}/{instanceId?}
        private static TemplateMatcher GetStartOrchestrationRoute()
        {
            var defaultRouteValues = RouteValueDictionaryFromArray(new KeyValuePair<string, object>[] { new KeyValuePair<string, object>(InstanceIdRouteParameter, string.Empty) });
            return new TemplateMatcher(TemplateParser.Parse($"{OrchestratorsControllerSegment}{{{FunctionNameRouteParameter}}}/{{{InstanceIdRouteParameter}?}}"), defaultRouteValues);
        }

        // /entity/{entityId}/{entityKey?}
        private static TemplateMatcher GetEntityRoute()
        {
            var defaultRouteValues = RouteValueDictionaryFromArray(new KeyValuePair<string, object>[] { new KeyValuePair<string, object>(EntityKeyRouteParameter, string.Empty) });
            return new TemplateMatcher(TemplateParser.Parse($"{EntitiesControllerSegment}{{{EntityNameRouteParameter}}}/{{{EntityKeyRouteParameter}?}}"), defaultRouteValues);
        }

        // Can't use RouteValueDictionary.FromArray() due to it only being available in the version we use in Functions V2.
        // This custom implementation should be equivalent.
        private static RouteValueDictionary RouteValueDictionaryFromArray(KeyValuePair<string, object>[] values)
        {
            var routeValueDictionary = new RouteValueDictionary();
            foreach (var pair in values)
            {
                routeValueDictionary[pair.Key] = pair.Value;
            }

            return routeValueDictionary;
        }

        // /instances/{instanceId}/{operation}
        private static TemplateMatcher GetInstancesRoute()
        {
            return new TemplateMatcher(TemplateParser.Parse($"{InstancesControllerSegment}{{{InstanceIdRouteParameter}?}}/{{{OperationRouteParameter}?}}"), new RouteValueDictionary());
        }

        // /instances/{instanceId}/raiseEvent/{eventName}
        private static TemplateMatcher GetInstanceRaiseEventRoute()
        {
            return new TemplateMatcher(TemplateParser.Parse($"{InstancesControllerSegment}{{{InstanceIdRouteParameter}?}}/{RaiseEventOperation}/{{{EventNameRouteParameter}}}"), new RouteValueDictionary());
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
            DurableClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            if (retryInterval > timeout)
            {
                throw new ArgumentException($"Total timeout {timeout.TotalSeconds} should be bigger than retry timeout {retryInterval.TotalSeconds}");
            }

            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName);

            IDurableOrchestrationClient client = this.GetClient(request);
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
                    return this.CreateCheckStatusResponseMessage(
                        request,
                        instanceId,
                        httpManagementPayload.StatusQueryGetUri,
                        httpManagementPayload.SendEventPostUri,
                        httpManagementPayload.TerminatePostUri,
                        httpManagementPayload.RewindPostUri,
                        httpManagementPayload.PurgeHistoryDeleteUri);
                }
            }
        }

        public async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
        {
            try
            {
                string path = request.RequestUri.AbsolutePath;
                string basePath = this.config.Options.NotificationUrl.AbsolutePath;
                path = path.Substring(basePath.Length);
                var routeValues = new RouteValueDictionary();
                if (StartOrchestrationRoute.TryMatch(path, routeValues))
                {
                    string functionName = (string)routeValues[FunctionNameRouteParameter];
                    string instanceId = (string)routeValues[InstanceIdRouteParameter];
                    if (request.Method == HttpMethod.Post)
                    {
                        return await this.HandleStartOrchestratorRequestAsync(request, functionName, instanceId);
                    }
                    else
                    {
                        return request.CreateResponse(HttpStatusCode.NotFound);
                    }
                }

                if (EntityRoute.TryMatch(path, routeValues))
                {
                    try
                    {
                        string entityName = (string)routeValues[EntityNameRouteParameter];
                        string entityKey = (string)routeValues[EntityKeyRouteParameter];
                        EntityId entityId = new EntityId(entityName, entityKey);
                        if (request.Method == HttpMethod.Get)
                        {
                            return await this.HandleGetEntityRequestAsync(request, entityId);
                        }
                        else if (request.Method == HttpMethod.Post)
                        {
                            return await this.HandlePostEntityOperationRequestAsync(request, entityId);
                        }
                        else
                        {
                            return request.CreateResponse(HttpStatusCode.NotFound);
                        }
                    }
                    catch (ArgumentException e)
                    {
                        return request.CreateErrorResponse(HttpStatusCode.BadRequest, e.Message);
                    }
                }

                if (InstancesRoute.TryMatch(path, routeValues))
                {
                    routeValues.TryGetValue(InstanceIdRouteParameter, out object instanceIdValue);
                    routeValues.TryGetValue(OperationRouteParameter, out object operationValue);
                    var instanceId = instanceIdValue as string;
                    var operation = operationValue as string;

                    if (instanceId == null)
                    {
                        // Retrieve All Status or conditional query in case of the request URL ends e.g. /instances/
                        if (request.Method == HttpMethod.Get)
                        {
                            return await this.HandleGetStatusRequestAsync(request);
                        }
                        else if (request.Method == HttpMethod.Delete)
                        {
                            return await this.HandleDeleteHistoryWithFiltersRequestAsync(request);
                        }
                        else
                        {
                            return request.CreateResponse(HttpStatusCode.NotFound);
                        }
                    }
                    else if (instanceId != null && operation == null)
                    {
                        if (request.Method == HttpMethod.Get)
                        {
                            return await this.HandleGetStatusRequestAsync(request, instanceId);
                        }
                        else if (request.Method == HttpMethod.Delete)
                        {
                            return await this.HandleDeleteHistoryByIdRequestAsync(request, instanceId);
                        }
                        else
                        {
                            return request.CreateResponse(HttpStatusCode.NotFound);
                        }
                    }
                    else
                    {
                        if (string.Equals(operation, TerminateOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleTerminateInstanceRequestAsync(request, instanceId);
                        }
                        else if (string.Equals(operation, RewindOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleRewindInstanceRequestAsync(request, instanceId);
                        }
                        else
                        {
                            return request.CreateResponse(HttpStatusCode.NotFound);
                        }
                    }
                }

                if (InstanceRaiseEventRoute.TryMatch(path, routeValues))
                {
                    string instanceId = (string)routeValues[InstanceIdRouteParameter];
                    string eventName = (string)routeValues[EventNameRouteParameter];
                    if (request.Method == HttpMethod.Post)
                    {
                        return await this.HandleRaiseEventRequestAsync(request, instanceId, eventName);
                    }
                    else
                    {
                        return request.CreateResponse(HttpStatusCode.NotFound);
                    }
                }

                return request.CreateResponse(HttpStatusCode.NotFound);
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
            IDurableOrchestrationClient client = this.GetClient(request);
            var queryNameValuePairs = request.GetQueryNameValuePairs();
            var createdTimeFrom = GetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeFromParameter, default(DateTime));
            var createdTimeTo = GetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeToParameter, default(DateTime));
            var runtimeStatus = GetIEnumerableQueryParameterValue<OrchestrationRuntimeStatus>(queryNameValuePairs, RuntimeStatusParameter);
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
                var condition = new OrchestrationStatusQueryCondition()
                {
                    CreatedTimeFrom = createdTimeFrom,
                    CreatedTimeTo = createdTimeTo,
                    RuntimeStatus = runtimeStatus,
                    PageSize = pageSize,
                    ContinuationToken = continuationToken,
                };
                var context = await client.GetStatusAsync(condition, CancellationToken.None);
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

        private async Task<HttpResponseMessage> HandleDeleteHistoryByIdRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);
            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId, showHistory: false);
            if (status == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            PurgeHistoryResult purgeHistoryResult = await client.PurgeInstanceHistoryAsync(instanceId);
            return request.CreateResponse(HttpStatusCode.OK, purgeHistoryResult);
        }

        private async Task<HttpResponseMessage> HandleDeleteHistoryWithFiltersRequestAsync(HttpRequestMessage request)
        {
            IDurableOrchestrationClient client = this.GetClient(request);
            var queryNameValuePairs = request.GetQueryNameValuePairs();
            var createdTimeFrom =
                GetDateTimeQueryParameterValue(queryNameValuePairs, "createdTimeFrom", DateTime.MinValue);

            if (createdTimeFrom == DateTime.MinValue)
            {
                var badRequestResponse = request.CreateResponse(
                    HttpStatusCode.BadRequest,
                    "Please provide value for 'createdTimeFrom' parameter.");
                return badRequestResponse;
            }

            var createdTimeTo =
                GetDateTimeQueryParameterValue(queryNameValuePairs, "createdTimeTo", DateTime.UtcNow);
            var runtimeStatusCollection =
                GetIEnumerableQueryParameterValue<OrchestrationStatus>(queryNameValuePairs, "runtimeStatus");

            PurgeHistoryResult purgeHistoryResult = await client.PurgeInstanceHistoryAsync(createdTimeFrom, createdTimeTo, runtimeStatusCollection);

            if (purgeHistoryResult == null || purgeHistoryResult.InstancesDeleted == 0)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            return request.CreateResponse(HttpStatusCode.OK, purgeHistoryResult);
        }

        private async Task<HttpResponseMessage> HandleGetStatusRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

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
                Name = status.Name,
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

        private static IEnumerable<T> GetIEnumerableQueryParameterValue<T>(NameValueCollection queryStringNameValueCollection, string queryParameterName)
            where T : struct
        {
            var results = new List<T>();
            var parameters = queryStringNameValueCollection.GetValues(queryParameterName) ?? new string[] { };

            foreach (var value in parameters.SelectMany(x => x.Split(',')))
            {
                if (Enum.TryParse(value, out T result))
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
            IDurableOrchestrationClient client = this.GetClient(request);

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
                IDurableOrchestrationClient client = this.GetClient(request);

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
            IDurableOrchestrationClient client = this.GetClient(request);

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
            IDurableOrchestrationClient client = this.GetClient(request);

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

        private async Task<HttpResponseMessage> HandleGetEntityRequestAsync(
            HttpRequestMessage request,
            EntityId entityId)
        {
            IDurableEntityClient client = this.GetClient(request);

            var response = await client.ReadEntityStateAsync<JToken>(entityId);

            if (!response.EntityExists)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }
            else
            {
                return request.CreateResponse(HttpStatusCode.OK, response.EntityState);
            }
        }

        private async Task<HttpResponseMessage> HandlePostEntityOperationRequestAsync(
            HttpRequestMessage request,
            EntityId entityId)
        {
            IDurableEntityClient client = this.GetClient(request);

            string operationName = request.GetQueryNameValuePairs()["op"] ?? string.Empty;

            if (request.Content == null || request.Content.Headers.ContentLength == 0)
            {
                await client.SignalEntityAsync(entityId, operationName);
                return request.CreateResponse(HttpStatusCode.Accepted);
            }
            else
            {
                var requestContent = await request.Content.ReadAsStringAsync();
                string mediaType = request.Content.Headers.ContentType?.MediaType;
                object operationInput;
                if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        operationInput = JToken.Parse(requestContent);
                    }
                    catch (JsonException e)
                    {
                        return request.CreateErrorResponse(HttpStatusCode.BadRequest, "Could not parse JSON content: " + e.Message);
                    }
                }
                else
                {
                    operationInput = requestContent;
                }

                await client.SignalEntityAsync(entityId, operationName, operationInput);
                return request.CreateResponse(HttpStatusCode.Accepted);
            }
        }

        private IDurableClient GetClient(HttpRequestMessage request)
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

            var attribute = new DurableClientAttribute
            {
                TaskHub = taskHub,
                ConnectionName = connectionName,
            };

            return this.GetClient(attribute);
        }

        // protected virtual to allow mocking in unit tests.
        protected virtual IDurableClient GetClient(DurableClientAttribute attribute)
        {
            return this.config.GetClient(attribute);
        }

        internal string GetBaseUrl()
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.config.Options.NotificationUrl;

            string hostUrl = notificationUri.GetLeftPart(UriPartial.Authority);
            return hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
        }

        internal string GetUniversalQueryStrings()
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.config.Options.NotificationUrl;

            return !string.IsNullOrEmpty(notificationUri.Query)
                ? notificationUri.Query.TrimStart('?')
                : string.Empty;
        }

        internal HttpCreationPayload GetInstanceCreationLinks()
        {
            string baseUrl = this.GetBaseUrl();
            string instancePrefix = baseUrl + "/" + OrchestratorsControllerSegment + "{functionName}[/{instanceId}]";

            string querySuffix = this.GetUniversalQueryStrings();

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
            string allInstancesPrefix = baseUrl + "/" + InstancesControllerSegment;
            string instancePrefix = allInstancesPrefix + WebUtility.UrlEncode(instanceId);

            string taskHub = WebUtility.UrlEncode(taskHubName ?? this.config.Options.HubName);
            string connection = WebUtility.UrlEncode(connectionName ?? this.config.Options.GetConnectionStringName() ?? ConnectionStringNames.Storage);

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
                PurgeHistoryDeleteUri = instancePrefix + "?" + querySuffix,
            };

            return httpManagementPayload;
        }

        private HttpResponseMessage CreateCheckStatusResponseMessage(HttpRequestMessage request, string instanceId, string statusQueryGetUri, string sendEventPostUri, string terminatePostUri, string rewindPostUri, string purgeHistoryDeleteUri)
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
                    purgeHistoryDeleteUri,
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
