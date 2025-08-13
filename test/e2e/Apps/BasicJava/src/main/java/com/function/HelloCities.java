package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import java.time.Instant;
import java.time.format.DateTimeParseException;
import java.util.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

public class HelloCities {
    /**
     * Orchestrator function for HelloCities.
     */
    @FunctionName("HelloCities")
    public List<String> runOrchestrator(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext ctx, 
            final ExecutionContext context) {
        context.getLogger().info("Saying hello.");
        List<String> outputs = new ArrayList<>();
        outputs.add(ctx.callActivity("SayHello", "Tokyo", String.class).await());
        outputs.add(ctx.callActivity("SayHello", "Seattle", String.class).await());
        outputs.add(ctx.callActivity("SayHello", "London", String.class).await());
        return outputs;
    }

    /**
     * Activity function SayHello.
     */
    @FunctionName("SayHello")
    public String sayHello(
            @DurableActivityTrigger(name = "name") String name,
            final ExecutionContext context) {
        // Bug: https://github.com/microsoft/durabletask-java/issues/235
        name = name == null ? null : name.substring(1, name.length() - 1); 
        context.getLogger().info("Saying hello to " + name + ".");
        return "Hello " + name + "!";
    }

    /**
     * HTTP-triggered function to start orchestration.
     */
    @FunctionName("StartOrchestration")
    public HttpResponseMessage startOrchestration(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {
        DurableTaskClient client = durableContext.getClient();
        String orchestrationName = request.getQueryParameters().get("orchestrationName");
        String instanceId = client.scheduleNewOrchestrationInstance(orchestrationName);
        context.getLogger().info("Started orchestration with ID = '" + instanceId + "'.");
        return durableContext.createCheckStatusResponse(request, instanceId);
    }

    /**
     * HTTP-triggered function to start orchestration at a scheduled time.
     */
    @FunctionName("HelloCities_HttpStart_Scheduled")
    public HttpResponseMessage httpStartScheduled(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {
        DurableTaskClient client = durableContext.getClient();
        Instant scheduledStartTime;
        try {
            String scheduledStartTimeStr = request.getQueryParameters().get("ScheduledStartTime");
            scheduledStartTime = Instant.parse(scheduledStartTimeStr != null ? scheduledStartTimeStr : "");
        } catch (DateTimeParseException e) {
            scheduledStartTime = Instant.now();
        }
        NewOrchestrationInstanceOptions startOptions = new NewOrchestrationInstanceOptions();
        startOptions.setStartTime(scheduledStartTime);
        String instanceId = client.scheduleNewOrchestrationInstance("HelloCities", startOptions);
        context.getLogger().info("Started orchestration with ID = '" + instanceId + "'.");
        return durableContext.createCheckStatusResponse(request, instanceId);
    }
}