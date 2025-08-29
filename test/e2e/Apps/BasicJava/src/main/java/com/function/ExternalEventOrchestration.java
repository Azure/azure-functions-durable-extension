package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.google.gson.Gson;
import com.microsoft.azure.functions.*;
import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

import java.util.*;


/**
 * ExternalEventOrchestration Java translation from C#
 */
public class ExternalEventOrchestration {

    /**
     * Orchestrator function that waits for an external event named "Approval".
     */
    @FunctionName("ExternalEventOrchestrator")
    public String externalEventOrchestrator(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext context) {
        // Wait for external event "Approval"
        context.waitForExternalEvent("Approval", Boolean.class).await();
        return "Orchestrator Finished!";
    }

    /**
     * HTTP-triggered function that sends an external event to the orchestrator.
     */
    @FunctionName("SendExternalEvent_HttpStart")
    public HttpResponseMessage sendExternalEvent_HttpStart(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS)
            HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {

        DurableTaskClient client = durableContext.getClient();
        String instanceId = null;
        HttpResponseMessage.Builder responseBuilder = request.createResponseBuilder(HttpStatus.OK);

        try {
            // Read instanceId from request body (as string)
            Gson gson = new Gson();
            try {
                String body = request.getBody().orElse("");
                instanceId = gson.fromJson(body, String.class);
            } catch (Exception e) {
                instanceId = null;
            }
            if (instanceId == null || instanceId.isEmpty()) {
                responseBuilder.status(HttpStatus.BAD_REQUEST)
                        .body("Missing or invalid instanceId in request body.");
                return responseBuilder.build();
            }

            client.raiseEvent(instanceId, "Approval", true);
            responseBuilder.status(HttpStatus.OK)
                    .body("External event sent to " + instanceId + ".");
        } catch (Exception ex) {
            // Handle gRPC or other exceptions
            responseBuilder.status(HttpStatus.BAD_REQUEST)
                    .body("gRPC error: " + ex.getClass().getSimpleName() + " - " + ex.getMessage());
        }

        return responseBuilder.build();
    }
}