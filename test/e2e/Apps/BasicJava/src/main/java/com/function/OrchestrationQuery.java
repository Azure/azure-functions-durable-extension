package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.function.JsonHelpers.DurableMetadataGsonProvider;
import com.microsoft.azure.functions.*;

import java.util.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;


public class OrchestrationQuery {
    /**
     * This HTTP-triggered function returns all orchestration instances.
     */
    @FunctionName("GetAllInstances")
    public HttpResponseMessage getAllInstances(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {
        DurableTaskClient client = durableContext.getClient();
        try {
            // Java SDK: getAllInstancesAsync returns a CompletableFuture<List<OrchestrationInstanceStatus>>
            OrchestrationStatusQueryResult instances = client.queryInstances(new OrchestrationStatusQuery());
            String instanceString = DurableMetadataGsonProvider.createGson().toJson(instances.getOrchestrationState());
            return request.createResponseBuilder(HttpStatus.OK)
                    .header("Content-Type", "application/json")
                    .body(instanceString)
                    .build();
        } catch (Exception ex) {
            context.getLogger().severe("Error querying all instances: " + ex.getMessage());
            return request.createResponseBuilder(HttpStatus.BAD_REQUEST)
                    .header("Content-Type", "text/plain")
                    .body(ex.getMessage())
                    .build();
        }
    }

    /**
     * This HTTP-triggered function returns running, pending, and suspended orchestration instances.
     */
    @FunctionName("GetRunningInstances")
    public HttpResponseMessage getRunningInstances(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {
        DurableTaskClient client = durableContext.getClient();
        try {
            List<OrchestrationRuntimeStatus> statuses = Arrays.asList(
                    OrchestrationRuntimeStatus.RUNNING,
                    OrchestrationRuntimeStatus.PENDING,
                    OrchestrationRuntimeStatus.SUSPENDED
            );
            OrchestrationStatusQuery query = new OrchestrationStatusQuery();
            query.setRuntimeStatusList(statuses);
            OrchestrationStatusQueryResult instances = client.queryInstances(query);
            String instanceString = DurableMetadataGsonProvider.createGson().toJson(instances.getOrchestrationState());
            return request.createResponseBuilder(HttpStatus.OK)
                    .header("Content-Type", "application/json")
                    .body(instanceString)
                    .build();
        } catch (Exception ex) {
            context.getLogger().severe("Error querying running instances: " + ex.getMessage());
            return request.createResponseBuilder(HttpStatus.BAD_REQUEST)
                    .header("Content-Type", "text/plain")
                    .body(ex.getMessage())
                    .build();
        }
    }
}