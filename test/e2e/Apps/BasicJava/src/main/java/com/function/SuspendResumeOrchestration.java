package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;

import java.util.Optional;


/**
 * Azure Functions with HTTP Trigger for suspending and resuming orchestration instances.
 */
public class SuspendResumeOrchestration {

    /**
     * HTTP-triggered function to suspend an orchestration instance.
     */
    @FunctionName("SuspendInstance")
    public HttpResponseMessage suspendInstance(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            @BindingName("instanceId") String instanceId,
            final ExecutionContext context) {

        String suspendReason = "Suspending the instance for test.";
        DurableTaskClient client = durableContext.getClient();

        try {
            client.suspendInstance(instanceId, suspendReason);
            return request.createResponseBuilder(HttpStatus.OK).build();
        } catch (Exception ex) {
            HttpResponseMessage.Builder responseBuilder = request.createResponseBuilder(HttpStatus.BAD_REQUEST)
                .header("Content-Type", "text/plain")
                .body(ex.getMessage());
            return responseBuilder.build();
        }
    }

    /**
     * HTTP-triggered function to resume an orchestration instance.
     */
    @FunctionName("ResumeInstance")
    public HttpResponseMessage resumeInstance(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            @BindingName("instanceId") String instanceId,
            final ExecutionContext context) {

        String resumeReason = "Resuming the instance for test.";
        DurableTaskClient client = durableContext.getClient();

        try {
            client.resumeInstance(instanceId, resumeReason);
            return request.createResponseBuilder(HttpStatus.OK).build();
        } catch (Exception ex) {
            HttpResponseMessage.Builder responseBuilder = request.createResponseBuilder(HttpStatus.BAD_REQUEST)
                .header("Content-Type", "text/plain")
                .body(ex.getMessage());
            return responseBuilder.build();
        }
    }
}