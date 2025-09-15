package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import java.util.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

import java.util.logging.Logger;


/**
 * Orchestration and activity functions for simulating long-running orchestration and termination.
 */
public class TerminateOrchestration {

    /**
     * This is the orchestrator function.
     */
    @FunctionName("LongRunningOrchestrator")
    public List<String> longRunningOrchestrator(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx,
            final ExecutionContext context) {
        Logger logger = context.getLogger();
        logger.info("Starting long-running orchestration.");
        List<String> outputs = new ArrayList<>();

        // Call our fake activity 100,000 times to simulate an orchestration that might run for >= 10,000s (2.7 hours)
        for (int i = 0; i < 100000; i++) {
            outputs.add(ctx.callActivity("SimulatedWorkActivity", 100, String.class).await());
        }

        return outputs;
    }

    /**
     * This is the activity function.
     */
    @FunctionName("SimulatedWorkActivity")
    public String simulatedWorkActivity(
            @DurableActivityTrigger(name = "sleepMs") int sleepMs,
            final ExecutionContext context) {
        context.getLogger().info("Sleeping for " + sleepMs + "ms.");
        try {
            Thread.sleep(sleepMs);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            return "Sleep interrupted.";
        }
        return "Slept for " + sleepMs + "ms.";
    }

    /**
     * HTTP-triggered function to terminate an orchestration instance.
     */
    @FunctionName("TerminateInstance")
    public HttpResponseMessage run(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            @BindingName("instanceId") String instanceId,
            final ExecutionContext context) {
        DurableTaskClient client = durableContext.getClient();
        String reason = "Long-running orchestration was terminated early.";
        try {
            client.terminate(instanceId, reason);
            return request.createResponseBuilder(HttpStatus.OK).build();
        } catch (Exception ex) {
            context.getLogger().severe(ex.getMessage());
            return request.createResponseBuilder(HttpStatus.BAD_REQUEST)
                    .header("Content-Type", "text/plain")
                    .body(ex.getMessage())
                    .build();
        }
    }
}