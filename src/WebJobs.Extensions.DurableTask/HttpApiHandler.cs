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
using System.Web;
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
        private const string AppLeaseMakePrimaryControllerSegment = "makeprimary/";

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
        private const string RestartOperation = "restart";
        private const string ShowHistoryParameter = "showHistory";
        private const string ShowHistoryOutputParameter = "showHistoryOutput";
        private const string ShowInputParameter = "showInput";
        private const string FetchStateParameter = "fetchState";
        private const string InstanceIdPrefixParameter = "instanceIdPrefix";
        private const string CreatedTimeFromParameter = "createdTimeFrom";
        private const string CreatedTimeToParameter = "createdTimeTo";
        private const string RuntimeStatusParameter = "runtimeStatus";
        private const string PageSizeParameter = "top";
        private const string ReturnInternalServerErrorOnFailure = "returnInternalServerErrorOnFailure";
        private const string LastOperationTimeFrom = "lastOperationTimeFrom";
        private const string LastOperationTimeTo = "lastOperationTimeTo";
        private const string RestartWithNewInstanceId = "restartWithNewInstanceId";
        private const string TimeoutParameter = "timeout";
        private const string PollingInterval = "pollingInterval";
        private const string SuspendOperation = "suspend";
        private const string ResumeOperation = "resume";

        private const string EmptyEntityKeySymbol = "$";

        // API Routes
        private static readonly TemplateMatcher StartOrchestrationRoute = GetStartOrchestrationRoute();
        private static readonly TemplateMatcher EntityRoute = GetEntityRoute();
        private static readonly TemplateMatcher InstancesRoute = GetInstancesRoute();
        private static readonly TemplateMatcher InstanceRaiseEventRoute = GetInstanceRaiseEventRoute();
        private static readonly TemplateMatcher AppLeaseMakePrimaryRoute = MakePrimaryRoute();

        private readonly ILogger logger;
        private readonly MessagePayloadDataConverter messageDataConverter;
        private readonly LocalHttpListener localHttpListener;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly DurableTaskOptions durableTaskOptions;
        private readonly DurableTaskExtension config;
        private Func<Uri> webhookUrlProvider;

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

            this.webhookUrlProvider = this.durableTaskOptions.WebhookUriProviderOverride;

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
                httpManagementPayload.PurgeHistoryDeleteUri,
                httpManagementPayload.RestartPostUri,
                httpManagementPayload.SuspendPostUri,
                httpManagementPayload.ResumePostUri);
        }

        public void RegisterWebhookProvider(Func<Uri> webhookProvider)
        {
            this.webhookUrlProvider = webhookProvider;
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

        // /makeprimary
        private static TemplateMatcher MakePrimaryRoute()
        {
            return new TemplateMatcher(TemplateParser.Parse($"{AppLeaseMakePrimaryControllerSegment}"), new RouteValueDictionary());
        }

        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHub,
            string connectionName,
            bool returnInternalServerErrorOnFailure = false)
        {
            HttpManagementPayload httpManagementPayload = this.GetClientResponseLinks(
                null,
                instanceId,
                taskHub,
                connectionName,
                returnInternalServerErrorOnFailure);
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

            IDurableOrchestrationClient client = this.GetClient(request, attribute);

            DurableOrchestrationStatus status = null;

            if (client is DurableClient durableClient && durableClient.DurabilityProvider.SupportsPollFreeWait)
            {
                // For durability providers that support long polling, WaitForOrchestrationAsync is more efficient than GetStatusAsync
                // so we ignore the retryInterval argument and instead wait for the entire timeout duration

                var state = await durableClient.DurabilityProvider.WaitForOrchestrationAsync(instanceId, null, timeout, CancellationToken.None);
                if (state != null)
                {
                    status = DurableClient.ConvertOrchestrationStateToStatus(state);
                }
            }
            else
            {
                // This retry loop completes either when the
                // orchestration has completed, or when the timeout is reached.

                Stopwatch stopwatch = Stopwatch.StartNew();
                while (true)
                {
                    status = await client.GetStatusAsync(instanceId);

                    if (status != null && (
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Canceled ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated))
                    {
                        break;
                    }

                    TimeSpan elapsed = stopwatch.Elapsed;
                    if (elapsed < timeout)
                    {
                        TimeSpan remainingTime = timeout.Subtract(elapsed);
                        await Task.Delay(remainingTime > retryInterval ? retryInterval : remainingTime);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            switch (status?.RuntimeStatus)
            {
                case OrchestrationRuntimeStatus.Completed:
                    return request.CreateResponse(HttpStatusCode.OK, status.Output);

                case OrchestrationRuntimeStatus.Canceled:
                case OrchestrationRuntimeStatus.Failed:
                case OrchestrationRuntimeStatus.Terminated:
                    return await this.HandleGetStatusRequestAsync(request, instanceId, returnInternalServerErrorOnFailure, client);

                default:
                    return this.CreateCheckStatusResponseMessage(
                                  request,
                                  instanceId,
                                  httpManagementPayload.StatusQueryGetUri,
                                  httpManagementPayload.SendEventPostUri,
                                  httpManagementPayload.TerminatePostUri,
                                  httpManagementPayload.PurgeHistoryDeleteUri,
                                  httpManagementPayload.RestartPostUri,
                                  httpManagementPayload.SuspendPostUri,
                                  httpManagementPayload.ResumePostUri);
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
                else
                {
                    basePath = this.GetWebhookUri().AbsolutePath;
                }

                string path = "/" + WebUtility.UrlDecode(request.RequestUri.AbsolutePath).Substring(basePath.Length).Trim('/');
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
                    else if (request.Method == HttpMethod.Post)
                    {
                        if (string.Equals(operation, TerminateOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleTerminateInstanceRequestAsync(request, instanceId);
                        }
                        else if (string.Equals(operation, RewindOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleRewindInstanceRequestAsync(request, instanceId);
                        }
                        else if (string.Equals(operation, RestartOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleRestartInstanceRequestAsync(request, instanceId);
                        }
                        else if (string.Equals(operation, SuspendOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleSuspendInstanceRequestAsync(request, instanceId);
                        }
                        else if (string.Equals(operation, ResumeOperation, StringComparison.OrdinalIgnoreCase))
                        {
                            return await this.HandleResumeInstanceRequestAsync(request, instanceId);
                        }
                    }
                    else
                    {
                        return request.CreateResponse(HttpStatusCode.NotFound);
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

                if (AppLeaseMakePrimaryRoute.TryMatch(path, routeValues))
                {
                    return await this.HandleMakePrimaryRequestAsync(request);
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

            if (TryGetStringQueryParameterValue(queryNameValuePairs, InstanceIdPrefixParameter, out string instanceIdPrefix))
            {
                condition.InstanceIdPrefix = instanceIdPrefix;
            }

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
            string instanceId,
            bool? returnInternalServerErrorOnFailure = null,
            IDurableOrchestrationClient existingClient = null)
        {
            IDurableOrchestrationClient client = existingClient ?? this.GetClient(request);
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

            bool finalReturnInternalServerErrorOnFailure;
            if (returnInternalServerErrorOnFailure.HasValue)
            {
                finalReturnInternalServerErrorOnFailure = returnInternalServerErrorOnFailure.Value;
            }
            else
            {
                if (!TryGetBooleanQueryParameterValue(queryNameValuePairs, ReturnInternalServerErrorOnFailure, out finalReturnInternalServerErrorOnFailure))
                {
                    finalReturnInternalServerErrorOnFailure = false;
                }
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
                case OrchestrationRuntimeStatus.Suspended:
                    statusCode = HttpStatusCode.Accepted;
                    location = request.RequestUri;
                    break;

                // The orchestration has failed - return 500 w/out Location header
                case OrchestrationRuntimeStatus.Failed:
                    statusCode = finalReturnInternalServerErrorOnFailure ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
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

        private async Task<HttpResponseMessage> HandleMakePrimaryRequestAsync(HttpRequestMessage request)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            await client.MakeCurrentAppPrimaryAsync();

            return request.CreateResponse(HttpStatusCode.OK);
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

        private static bool TryGetStringQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName, out string stringValue)
        {
            stringValue = queryStringNameValueCollection[queryParameterName];
            return stringValue != null;
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

        private static bool TryGetTimeSpanQueryParameterValue(NameValueCollection queryStringNameValueCollection, string queryParameterName, out TimeSpan? timeSpanValue)
        {
            timeSpanValue = null;
            string value = queryStringNameValueCollection[queryParameterName];
            if (double.TryParse(value, out double doubleValue))
            {
                timeSpanValue = TimeSpan.FromSeconds(doubleValue);
            }

            return timeSpanValue != null;
        }

        private static bool IsCompletedStatus(OrchestrationRuntimeStatus status)
        {
            return status == OrchestrationRuntimeStatus.Completed ||
                status == OrchestrationRuntimeStatus.Terminated ||
                status == OrchestrationRuntimeStatus.Canceled ||
                status == OrchestrationRuntimeStatus.Failed;
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

            if (IsCompletedStatus(status.RuntimeStatus))
            {
                return request.CreateResponse(HttpStatusCode.Gone);
            }

            string reason = request.GetQueryNameValuePairs()["reason"];

            await client.TerminateAsync(instanceId, reason);

            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        private async Task<HttpResponseMessage> HandleSuspendInstanceRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            if (IsCompletedStatus(status.RuntimeStatus))
            {
                return request.CreateResponse(HttpStatusCode.Gone);
            }

            string reason = request.GetQueryNameValuePairs()["reason"];

            await client.SuspendAsync(instanceId, reason);

            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        private async Task<HttpResponseMessage> HandleResumeInstanceRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            IDurableOrchestrationClient client = this.GetClient(request);

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);
            if (status == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            if (IsCompletedStatus(status.RuntimeStatus))
            {
                return request.CreateResponse(HttpStatusCode.Gone);
            }

            string reason = request.GetQueryNameValuePairs()["reason"];

            await client.ResumeAsync(instanceId, reason);

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

                var queryNameValuePairs = request.GetQueryNameValuePairs();

                object input = null;
                if (request.Content != null && request.Content.Headers?.ContentLength != 0)
                {
                    string json = await request.Content.ReadAsStringAsync();
                    input = JsonConvert.DeserializeObject(json, this.messageDataConverter.JsonSettings);
                }

                string id = await client.StartNewAsync(functionName, instanceId, input);

                TryGetTimeSpanQueryParameterValue(queryNameValuePairs, TimeoutParameter, out TimeSpan? timeout);
                TryGetTimeSpanQueryParameterValue(queryNameValuePairs, PollingInterval, out TimeSpan? pollingInterval);

                // for durability providers that support poll-free waiting, we override the specified polling interval
                if (client is DurableClient durableClient && durableClient.DurabilityProvider.SupportsPollFreeWait)
                {
                    pollingInterval = timeout;
                }

                if (timeout.HasValue && pollingInterval.HasValue)
                {
                    return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, id, timeout, pollingInterval);
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

        private async Task<HttpResponseMessage> HandleRestartInstanceRequestAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            try
            {
                IDurableOrchestrationClient client = this.GetClient(request);

                var queryNameValuePairs = request.GetQueryNameValuePairs();

                string newInstanceId;
                if (TryGetBooleanQueryParameterValue(queryNameValuePairs, RestartWithNewInstanceId, out bool restartWithNewInstanceId))
                {
                    newInstanceId = await client.RestartAsync(instanceId, restartWithNewInstanceId);
                }
                else
                {
                    newInstanceId = await client.RestartAsync(instanceId);
                }

                TryGetTimeSpanQueryParameterValue(queryNameValuePairs, TimeoutParameter, out TimeSpan? timeout);
                TryGetTimeSpanQueryParameterValue(queryNameValuePairs, PollingInterval, out TimeSpan? pollingInterval);

                // for durability providers that support poll-free waiting, we override the specified polling interval
                if (client is DurableClient durableClient && durableClient.DurabilityProvider.SupportsPollFreeWait)
                {
                    pollingInterval = timeout;
                }

                if (timeout.HasValue && pollingInterval.HasValue)
                {
                    return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, newInstanceId, timeout, pollingInterval);
                }
                else
                {
                    return client.CreateCheckStatusResponse(request, newInstanceId);
                }
            }
            catch (ArgumentException e)
            {
                return request.CreateErrorResponse(HttpStatusCode.BadRequest, "InstanceId does not match a valid orchestration instance.", e);
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

        private IDurableClient GetClient(HttpRequestMessage request, DurableClientAttribute existingAttribute = null)
        {
            string taskHub = existingAttribute?.TaskHub;
            string connectionName = existingAttribute?.ConnectionName;

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
            Uri notificationUri = this.GetWebhookUri();

            string hostUrl = notificationUri.GetLeftPart(UriPartial.Authority);
            return hostUrl + notificationUri.AbsolutePath.TrimEnd('/');
        }

        internal string GetUniversalQueryStrings()
        {
            Uri notificationUri = this.GetWebhookUri();

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
            Uri notificationUri = this.GetWebhookUri();
            Uri baseUri = request?.RequestUri ?? notificationUri;

            bool useForwardedHost = this.config.Options.HttpSettings.UseForwardedHost;

            if (useForwardedHost && request != null)
            {
                if (request.Headers.TryGetValues("X-Forwarded-Host", out var forwardedHost) &&
                    request.Headers.TryGetValues("X-Forwarded-Proto", out var forwardedProto))
                {
                    baseUri = new UriBuilder(baseUri)
                    {
                        Host = forwardedHost.FirstOrDefault(),
                        Scheme = forwardedProto.FirstOrDefault(),
                    }.Uri;
                }
            }

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
                RestartPostUri = instancePrefix + "/" + RestartOperation + "?" + querySuffix,
                SuspendPostUri = instancePrefix + "/" + SuspendOperation + "?reason={text}&" + querySuffix,
                ResumePostUri = instancePrefix + "/" + ResumeOperation + "?reason={text}&" + querySuffix,
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
            string purgeHistoryDeleteUri,
            string restartPostUri,
            string suspendPostUri,
            string resumePostUri)
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
                    restartPostUri,
                    suspendPostUri,
                    resumePostUri,
                });

            // Implement the async HTTP 202 pattern.
            response.Headers.Location = new Uri(statusQueryGetUri);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            return response;
        }

        internal Uri GetWebhookUri()
        {
            string errorMessage = "Webhooks are not configured. This may occur if the environment variable `WEBSITE_HOSTNAME` is not set (should be automatically set for Azure Functions). " +
                "Try setting it to the appropiate URI to reach your app. For example: the DNS name of the app, or a value of the form <ip-address>:<port>.";
            return this.webhookUrlProvider?.Invoke() ?? throw new InvalidOperationException(errorMessage);
        }

        internal bool TryGetRpcBaseUrl(out Uri rpcBaseUrl)
        {
            if (this.durableTaskOptions.LocalRpcEndpointEnabled != false)
            {
                rpcBaseUrl = this.localHttpListener.InternalRpcUri;
                return true;
            }

            // The app owner explicitly disabled the local RPC endpoint
            rpcBaseUrl = null;
            return false;
        }

        internal async Task StartLocalHttpServerAsync()
        {
            if (!this.localHttpListener.IsListening)
            {
                await this.localHttpListener.StartAsync();

                this.traceHelper.ExtensionInformationalEvent(
                    this.durableTaskOptions.HubName,
                    instanceId: string.Empty,
                    functionName: string.Empty,
                    message: $"Opened local http RPC endpoint: {this.localHttpListener.InternalRpcUri}",
                    writeToUserLogs: true);
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
                    message: $"Closing local http RPC endpoint: {this.localHttpListener.InternalRpcUri}",
                    writeToUserLogs: true);

                await this.localHttpListener.StopAsync();
            }
        }
    }
}
