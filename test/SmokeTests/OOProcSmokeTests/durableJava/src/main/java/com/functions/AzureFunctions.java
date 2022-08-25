package com.functions;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;
import java.util.*;

import com.microsoft.durabletask.DurableTaskClient;
import com.microsoft.durabletask.OrchestrationRunner;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

/**
 * Azure Durable Functions with HTTP trigger.
 */
public class AzureFunctions {
    /**
     * This HTTP-triggered function starts the orchestration.
     */
    @FunctionName("StartOrchestration")
    public HttpResponseMessage startOrchestration(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {
        context.getLogger().info("Java HTTP trigger processed a request.");

        DurableTaskClient client = durableContext.getClient();
        String instanceId = client.scheduleNewOrchestrationInstance("Cities");
        context.getLogger().info("Created new Java orchestration with instance ID = " + instanceId);
        return durableContext.createCheckStatusResponse(request, instanceId);
    }

    /**
     * This is the orchestrator function. The OrchestrationRunner.loadAndRun() static
     * method is used to take the function input and execute the orchestrator logic.
     */
    @FunctionName("Cities")
    public String citiesOrchestrator(
            @DurableOrchestrationTrigger(name = "orchestratorRequestProtoBytes") String orchestratorRequestProtoBytes) {
        return OrchestrationRunner.loadAndRun(orchestratorRequestProtoBytes, ctx -> {
            String result = "";
            result += ctx.callActivity("Capitalize", "Tokyo", String.class).await() + ", ";
            result += ctx.callActivity("Capitalize", "London", String.class).await() + ", ";
            result += ctx.callActivity("Capitalize", "Seattle", String.class).await() + ", ";
            result += ctx.callActivity("Capitalize", "Austin", String.class).await();
            return result;
        });
    }

    /**
     * This is the activity function that gets invoked by the orchestration.
     */
    @FunctionName("Capitalize")
    public String capitalize(
            @DurableActivityTrigger(name = "name") String name,
            final ExecutionContext context) {
        context.getLogger().info("Capitalizing: " + name);
        return name.toUpperCase();
    }
}
