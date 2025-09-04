package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

import java.time.Duration;
import java.util.Optional;


/**
 * HTTP-triggered function to start the TimeoutOrchestrator.
 */
public class TimeoutOrchestration {

    @FunctionName("TimeoutOrchestrator_HttpStart")
    public HttpResponseMessage timerHttpStart(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {

        context.getLogger().info("Java HTTP trigger processed a request.");

        // Parse timeoutSeconds from query or body
        int timeoutSeconds = 0;
        try {
            String timeoutStr = request.getQueryParameters().get("timeoutSeconds");
            if (timeoutStr == null && request.getBody().isPresent()) {
                timeoutStr = request.getBody().get();
            }
            timeoutSeconds = Integer.parseInt(timeoutStr);
        } catch (Exception e) {
            return request.createResponseBuilder(HttpStatus.BAD_REQUEST)
                    .body("Please pass a valid integer for timeoutSeconds.").build();
        }

        DurableTaskClient client = durableContext.getClient();
        String instanceId = client.scheduleNewOrchestrationInstance("TimeoutOrchestrator", timeoutSeconds);
        context.getLogger().info("Started orchestration with ID = '" + instanceId + "'.");

        return durableContext.createCheckStatusResponse(request, instanceId);
    }

    /**
     * Orchestrator function that runs an activity with a timeout.
     */
    @FunctionName("TimeoutOrchestrator")
    public String timeoutOrchestrator(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {

        int timeoutSeconds = ctx.getInput(Integer.class);

        Task<String> activityTask = ctx.callActivity("LongActivity", ctx.getInstanceId(), String.class);
        Task<Void> timeoutTask = ctx.createTimer(Duration.ofSeconds(timeoutSeconds));

        Task<?> winner = ctx.anyOf(activityTask, timeoutTask).await();

        if (winner == activityTask) {
            // success case
            return activityTask.await();
        } else {
            return "The activity function timed out";
        }
    }

    /**
     * Activity function that simulates a long-running operation.
     */
    @FunctionName("LongActivity")
    public String longActivity(
            @DurableActivityTrigger(name = "instanceId") String instanceId,
            final ExecutionContext context) {
        try {
            Thread.sleep(5000);
        } catch (InterruptedException e) {
            // Handle interruption if needed
        }
        return "The activity function completed successfully";
    }
}