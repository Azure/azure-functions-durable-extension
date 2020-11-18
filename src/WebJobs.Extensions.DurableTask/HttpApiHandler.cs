// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
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
    internal class HttpApiHandler : IDisposable
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
        private const string FetchStateParameter = "fetchState";
        private const string CreatedTimeFromParameter = "createdTimeFrom";
        private const string CreatedTimeToParameter = "createdTimeTo";
        private const string RuntimeStatusParameter = "runtimeStatus";
        private const string PageSizeParameter = "top";
        private const string ReturnInternalServerErrorOnFailure = "returnInternalServerErrorOnFailure";
        private const string LastOperationTimeFrom = "lastOperationTimeFrom";
        private const string LastOperationTimeTo = "lastOperationTimeTo";

        private const string EmptyEntityKeySymbol = "$";

        // API Routes
        private static readonly TemplateMatcher StartOrchestrationRoute = GetStartOrchestrationRoute();
        private static readonly TemplateMatcher EntityRoute = GetEntityRoute();
        private static readonly TemplateMatcher InstancesRoute = GetInstancesRoute();
        private static readonly TemplateMatcher InstanceRaiseEventRoute = GetInstanceRaiseEventRoute();

        private readonly ILogger logger;
        private readonly MessagePayloadDataConverter messageDataConverter;
        private readonly LocalHttpListener localHttpListener;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly DurableTaskOptions durableTaskOptions;
        private readonly DurableTaskExtension config;

        public HttpApiHandler(
            EndToEndTraceHelper traceHelper,
            MessagePayloadDataConverter messageDataConverter,
            DurableTaskOptions durableTaskOptions,
            ILogger logger)
        {
            this.messageDataConverter = messageDataConverter;
            this.logger = logger;
            this.durableTaskOptions = durableTaskOptions;
            this.traceHelper = traceHelper;

            // The listen URL must not include the path.
            this.localHttpListener = new LocalHttpListener(
                this.traceHelper,
                this.durableTaskOptions,
                this.HandleRequestAsync);
        }

        public HttpApiHandler(DurableTaskExtension config, ILogger logger)
            : this(config.TraceHelper, config.MessageDataConverter, config.Options, logger)
        {
            this.config = config;
        }

        public void Dispose()
        {
            this.localHttpListener.Dispose();
        }

        internal HttpResponseMessage CreateCheckStatusResponse(
            HttpRequestMessage request,
            string instanceId,
            DurableClientAttribute attribute,
            bool returnInternalServerErrorOnFailure = false)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName, returnInternalServerErrorOnFailure);
            return this.CreateCheckStatusResponseMessage(
                request,
                httpManagementPayload.Id,
                httpManagementPayload.StatusQueryGetUri,
                httpManagementPayload.SendEventPostUri,
                httpManagementPayload.TerminatePostUri,
                httpManagementPayload.PurgeHistoryDeleteUri);
        }

        // /orchestrators/{functionName}/{instanceId?}
        private static TemplateMatcher GetStartOrchestrationRoute()
        {
            var defaultRouteValues = RouteValueDictionaryFromArray(new KeyValuePair<string, object>[] { new KeyValuePair<string, object>(InstanceIdRouteParameter, string.Empty) });
            return new TemplateMatcher(TemplateParser.Parse($"{OrchestratorsControllerSegment}{{{FunctionNameRouteParameter}}}/{{{InstanceIdRouteParameter}?}}"), defaultRouteValues);
        }

        // /entities/{entityName}/{entityKey?}
        private static TemplateMatcher GetEntityRoute()
        {
            var defaultRouteValues = RouteValueDictionaryFromArray(new KeyValuePair<string, object>[] { new KeyValuePair<string, object>(EntityKeyRouteParameter, string.Empty) });
            return new TemplateMatcher(TemplateParser.Parse($"{EntitiesControllerSegment}{{{EntityNameRouteParameter}?}}/{{{EntityKeyRouteParameter}?}}"), defaultRouteValues);
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

        // /instances/{instanceId?}/{operation?}
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
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(
                null,
                instanceId,
                taskHub,
                connectionName);
            return httpManagementPayload;
        }

        internal async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            DurableClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval,
            bool returnInternalServerErrorOnFailure = false)
        {
            if (retryInterval > timeout)
            {
                throw new ArgumentException($"Total timeout {timeout.TotalSeconds} should be bigger than retry timeout {retryInterval.TotalSeconds}");
            }

            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(request, instanceId, attribute?.TaskHub, attribute?.ConnectionName, returnInternalServerErrorOnFailure);

            IDurableOrchestrationClient client = this.GetClient(request);
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
                if (status != null)
                {
                    if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Canceled ||
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
                        httpManagementPayload.PurgeHistoryDeleteUri);
                }
            }
        }

        public async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
        {
            try
            {
                string basePath;
                if (this.localHttpListener.IsListening
                    && request.RequestUri.IsLoopback
                    && request.RequestUri.Port == this.localHttpListener.InternalRpcUri.Port)
                {
                    basePath = this.localHttpListener.InternalRpcUri.AbsolutePath;
                }
                else if (this.durableTaskOptions.NotificationUrl != null)
                {
                    basePath = this.durableTaskOptions.NotificationUrl.AbsolutePath;
                }
                else
                {
                    throw new InvalidOperationException($"Don't know how to handle request to {request.RequestUri}.");
                }

                string path = "/" + request.RequestUri.AbsolutePath.Substring(basePath.Length).Trim('/');
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

                        if (request.Method == HttpMethod.Get)
                        {
                            if (!string.IsNullOrEmpty(entityKey))
                            {
                                EntityId entityId = new EntityId(entityName, entityKey);
                                return await this.HandleGetEntityRequestAsync(request, entityId);
                            }
                            else
                            {
                                return await this.HandleListEntitiesRequestAsync(request, entityName);
                            }
                        }
                        else if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Delete)
                        {
                            EntityId entityId = new EntityId(entityName, entityKey);
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

            var condition = new OrchestrationStatusQueryCondition();

            if (TryGetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeFromParameter, out DateTime createdTimeFrom))
            {
                condition.CreatedTimeFrom = createdTimeFrom;
            }

            if (TryGetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeToParameter, out DateTime createdTimeTo))
            {
                condition.CreatedTimeTo = createdTimeTo;
            }

            if (TryGetIEnumerableQueryParameterValue<OrchestrationRuntimeStatus>(queryNameValuePairs, RuntimeStatusParameter, out IEnumerable<OrchestrationRuntimeStatus> runtimeStatus))
            {
                condition.RuntimeStatus = runtimeStatus;
            }

            if (TryGetBooleanQueryParameterValue(queryNameValuePairs, ShowInputParameter, out bool showInput))
            {
                condition.ShowInput = showInput;
            }

            if (TryGetIntQueryParameterValue(queryNameValuePairs, PageSizeParameter, out int pageSize))
            {
                condition.PageSize = pageSize;
            }

            if (request.Headers.TryGetValues("x-ms-continuation-token", out var headerValues))
            {
                condition.ContinuationToken = headerValues.FirstOrDefault();
            }

            IList<DurableOrchestrationStatus> statusForAllInstances;

            var context = await client.ListInstancesAsync(condition, CancellationToken.None);
            statusForAllInstances = context.DurableOrchestrationState.ToList();
            var nextContinuationToken = context.ContinuationToken;

            var results = new List<StatusResponsePayload>(statusForAllInstances.Count);
            foreach (var state in statusForAllInstances)
            {
                results.Add(ConvertFrom(state));
            }

            var response = request.CreateResponse(HttpStatusCode.OK, results);

            response.Headers.Add("x-ms-continuation-token", nextContinuationToken);
            return response;
        }

        private async Task<HttpResponseMessage> HandleListEntitiesRequestAsync(
            HttpRequestMessage request, string entityName)
        {
            IDurableEntityClient client = this.GetClient(request);
            NameValueCollection queryNameValuePairs = request.GetQueryNameValuePairs();

            var query = new EntityQuery();
            query.EntityName = entityName;

            if (TryGetDateTimeQueryParameterValue(queryNameValuePairs, LastOperationTimeFrom, out DateTime lastOperationTimeFrom))
            {
                query.LastOperationFrom = lastOperationTimeFrom;
            }

            if (TryGetDateTimeQueryParameterValue(queryNameValuePairs, LastOperationTimeTo, out DateTime lastOperationTimeTo))
            {
                query.LastOperationTo = lastOperationTimeTo;
            }

            if (TryGetIntQueryParameterValue(queryNameValuePairs, PageSizeParameter, out int pageSize))
            {
                query.PageSize = pageSize;
            }

            if (TryGetBooleanQueryParameterValue(queryNameValuePairs, FetchStateParameter, out bool fetchState))
            {
                query.FetchState = fetchState;
            }

            if (request.Headers.TryGetValues("x-ms-continuation-token", out IEnumerable<string> headerValues))
            {
                query.ContinuationToken = headerValues.FirstOrDefault();
            }

            EntityQueryResult result = await client.ListEntitiesAsync(query, CancellationToken.None);
            HttpResponseMessage response = request.CreateResponse(HttpStatusCode.OK, result.Entities);

            response.Headers.Add("x-ms-continuation-token", result.ContinuationToken);
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
            if (!TryGetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeFromParameter, out DateTime createdTimeFrom))
            {
                var badRequestResponse = request.CreateResponse(
                    HttpStatusCode.BadRequest,
                    "Please provide value for 'createdTimeFrom' parameter.");
                return badRequestResponse;
            }

            if (!TryGetDateTimeQueryParameterValue(queryNameValuePairs, CreatedTimeToParameter, out DateTime createdTimeTo))
            {
                createdTimeTo = DateTime.UtcNow;
            }

            TryGetIEnumerableQueryParameterValue<OrchestrationStatus>(queryNameValuePairs, RuntimeStatusParameter, out IEnumerable<OrchestrationStatus> runtimeStatusCollection);

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

            if (!TryGetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryParameter, out bool showHistory))
            {
                showHistory = false;
            }

            if (!TryGetBooleanQueryParameterValue(queryNameValuePairs, ShowHistoryOutputParameter, out bool showHistoryOutput))
            {
                showHistoryOutput = false;
            }

            if (!TryGetBooleanQueryParameterValue(queryNameValuePairs, ShowInputParameter, out bool showInput))
            {
                showInput = true;
            }

            if (!TryGetBooleanQueryParameterValue(queryNameValuePairs, ReturnInternalServerErrorOnFailure, out bool returnInternalServerErrorOnFailure))
            {
                returnInternalServerErrorOnFailure = false;
            }

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
                    statusCode = returnInternalServerErrorOnFailure ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
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
                ConvertFrom(status));

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

        private static StatusResponsePayload ConvertFrom(DurableOrchestrationStatus status)
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

        private static bool TryGetIEnumerableQueryParameterValue<T>(NameValueCollection queryStringNameValueCollection, string queryParameterName, out IEnumerable<T> collection)
            where T : struct
        {
            collection = new List<T>();
            string[] parameters = queryStringNameValueCollection.GetValues(queryParameterName) ?? new string[] { };

            foreach (string value in parameters.SelectMany(x => x.Split(',')))
            {
                if (Enum.TryParse(value, out T result))
                {
                    ((List<T>)collection).Add(result);
                }
            }

            if (collection.Any())
            {
                return true;
            }

            return false;
        }

        private static bool TryGetDateTimeQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName, out DateTime dateTimeValue)
        {
            string value = queryStringNameValueCollection[queryParameterName];
            return DateTime.TryParse(value, out dateTimeValue);
        }

        private static bool TryGetBooleanQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName, out bool boolValue)
        {
            string value = queryStringNameValueCollection[queryParameterName];
            return bool.TryParse(value, out boolValue);
        }

        private static bool TryGetIntQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName, out int intValue)
        {
            string value = queryStringNameValueCollection[queryParameterName];
            return int.TryParse(value, out intValue);
        }

        private async Task<HttpResponseMessage> HandleTerminateInstanceRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
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
                if (request.Content != null && request.Content.Headers?.ContentLength != 0)
                {
                    string json = await request.Content.ReadAsStringAsync();
                    input = JsonConvert.DeserializeObject(json, this.messageDataConverter.JsonSettings);
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

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
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

#pragma warning disable 0618
            await client.RewindAsync(instanceId, reason);
#pragma warning restore 0618

            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        private async Task<HttpResponseMessage> HandleRaiseEventRequestAsync(
            HttpRequestMessage request,
            string instanceId,
            string eventName)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
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
                eventData = MessagePayloadDataConverter.ConvertToJToken(stringData);
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

            // This input for entity key parameter means that entity key is an empty string.
            if (entityId.EntityKey.Equals(EmptyEntityKeySymbol))
            {
                entityId = new EntityId(entityId.EntityName, "");
            }

            EntityStateResponse<JToken> response = await client.ReadEntityStateAsync<JToken>(entityId);

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

            string operationName;

            if (request.Method == HttpMethod.Delete)
            {
                operationName = "delete";
            }
            else
            {
                operationName = request.GetQueryNameValuePairs()["op"] ?? string.Empty;
            }

            if (request.Content == null || request.Content.Headers.ContentLength == 0)
            {
                await client.SignalEntityAsync(entityId, operationName);
                return request.CreateResponse(HttpStatusCode.Accepted);
            }
            else
            {
                string requestContent = await request.Content.ReadAsStringAsync();
                string mediaType = request.Content.Headers.ContentType?.MediaType;
                object operationInput;
                if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        operationInput = MessagePayloadDataConverter.ConvertToJToken(requestContent);
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

            NameValueCollection pairs = request.GetQueryNameValuePairs();
            foreach (string key in pairs.AllKeys)
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

            Uri notificationUri = this.durableTaskOptions.NotificationUrl;

            string hostUrl = notificationUri.GetLeftPart(UriPartial.Authority);
            return hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
        }

        internal string GetUniversalQueryStrings()
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.durableTaskOptions.NotificationUrl;

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
             string connectionName,
             bool returnInternalServerErrorOnFailure = false)
        {
            this.ThrowIfWebhooksNotConfigured();

            Uri notificationUri = this.durableTaskOptions.NotificationUrl;
            Uri baseUri = request?.RequestUri ?? notificationUri;

            // e.g. http://{host}/runtime/webhooks/durabletask?code={systemKey}
            string hostUrl = baseUri.GetLeftPart(UriPartial.Authority);
            string baseUrl = hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
            string allInstancesPrefix = baseUrl + "/" + InstancesControllerSegment;
            string instancePrefix = allInstancesPrefix + WebUtility.UrlEncode(instanceId);

            string taskHub = WebUtility.UrlEncode(taskHubName ?? this.durableTaskOptions.HubName);
            string connection = WebUtility.UrlEncode(connectionName ?? this.config.GetDefaultConnectionName());

            string querySuffix = $"{TaskHubParameter}={taskHub}&{ConnectionParameter}={connection}";
            if (!string.IsNullOrEmpty(notificationUri.Query))
            {
                // This is expected to include the auto-generated system key for this extension.
                querySuffix += "&" + notificationUri.Query.TrimStart('?');
            }

            var httpManagementPayload = new HttpManagementPayload
            {
                Id = instanceId,
                StatusQueryGetUri = instancePrefix + "?" + querySuffix,
                SendEventPostUri = instancePrefix + "/" + RaiseEventOperation + "/{eventName}?" + querySuffix,
                TerminatePostUri = instancePrefix + "/" + TerminateOperation + "?reason={text}&" + querySuffix,
                RewindPostUri = instancePrefix + "/" + RewindOperation + "?reason={text}&" + querySuffix,
                PurgeHistoryDeleteUri = instancePrefix + "?" + querySuffix,
            };

            if (returnInternalServerErrorOnFailure)
            {
                httpManagementPayload.StatusQueryGetUri += "&returnInternalServerErrorOnFailure=true";
            }

            return httpManagementPayload;
        }

        private HttpResponseMessage CreateCheckStatusResponseMessage(
            HttpRequestMessage request,
            string instanceId,
            string statusQueryGetUri,
            string sendEventPostUri,
            string terminatePostUri,
            string purgeHistoryDeleteUri)
        {
            HttpResponseMessage response = request.CreateResponse(
                HttpStatusCode.Accepted,
                new
                {
                    id = instanceId,
                    statusQueryGetUri,
                    sendEventPostUri,
                    terminatePostUri,
                    purgeHistoryDeleteUri,
                });

            // Implement the async HTTP 202 pattern.
            response.Headers.Location = new Uri(statusQueryGetUri);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return response;
        }

        private void ThrowIfWebhooksNotConfigured()
        {
            if (this.durableTaskOptions.NotificationUrl == null)
            {
                throw new InvalidOperationException("Webhooks are not configured");
            }
        }

        internal bool TryGetRpcBaseUrl(out Uri rpcBaseUrl)
        {
            if (this.durableTaskOptions.LocalRpcEndpointEnabled != false)
            {
                rpcBaseUrl = this.localHttpListener.InternalRpcUri;
                return true;
            }

            // The app owner explicitly disabled the local RPC endpoint.
            rpcBaseUrl = null;
            return false;
        }

#if !FUNCTIONS_V1
        internal async Task StartLocalHttpServerAsync()
        {
            if (!this.localHttpListener.IsListening)
            {
                this.traceHelper.ExtensionInformationalEvent(
                    this.durableTaskOptions.HubName,
                    instanceId: string.Empty,
                    functionName: string.Empty,
                    message: $"Opening local RPC endpoint: {this.localHttpListener.InternalRpcUri}",
                    writeToUserLogs: true);

                await this.localHttpListener.StartAsync();
            }
        }

        internal async Task StopLocalHttpServerAsync()
        {
            if (this.localHttpListener.IsListening)
            {
                this.traceHelper.ExtensionInformationalEvent(
                    this.durableTaskOptions.HubName,
                    instanceId: string.Empty,
                    functionName: string.Empty,
                    message: $"Closing local RPC endpoint: {this.localHttpListener.InternalRpcUri}",
                    writeToUserLogs: true);

                await this.localHttpListener.StopAsync();
            }
        }
#endif

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
