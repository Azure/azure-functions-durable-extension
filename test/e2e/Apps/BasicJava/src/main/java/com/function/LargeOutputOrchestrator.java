package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.function.JsonHelpers.DurableMetadataGsonProvider;
import com.google.gson.Gson;
import com.microsoft.azure.functions.*;

import java.util.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

/**
 * LargeOutputOrchestrator Java implementation.
 */
public class LargeOutputOrchestrator {

    @FunctionName("LargeOutputOrchestrator")
    public List<String> runOrchestrator(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx, 
            final ExecutionContext context) {
        int sizeInKB = ctx.getInput(Integer.class);

        context.getLogger().info("Saying hello.");
        List<String> outputs = new ArrayList<>();

        outputs.add(ctx.callActivity("LargeOutputSayHello", "Tokyo", String.class).await());

        // Add a large message to the outputs that exceeds the Azure Storage Queue message size limit (64 KB),
        // so that blobs will be used instead.
        outputs.add(generateLargeString(sizeInKB));

        return outputs;
    }

    @FunctionName("LargeOutputSayHello")
    public String largeOutputSayHello(
            @DurableActivityTrigger(name = "name") String name,
            final ExecutionContext context) {
        return "Hello " + name + "!";
    }

    @FunctionName("LargeOutputOrchestrator_HttpStart")
    public HttpResponseMessage httpStart(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {
        int sizeInKB = 0;
        Gson gson = DurableMetadataGsonProvider.createGson();
        try {
            String body = request.getBody().orElse("0");
            sizeInKB = gson.fromJson(body, int.class);
        } catch (Exception e) {
            sizeInKB = 0;
        }

        DurableTaskClient client = durableContext.getClient();
        String instanceId = client.scheduleNewOrchestrationInstance("LargeOutputOrchestrator", sizeInKB);
        context.getLogger().info("Started orchestration with ID = '" + instanceId + "'.");

        return durableContext.createCheckStatusResponse(request, instanceId);
    }

    @FunctionName("LargeOutputOrchestrator_Query_Output")
    public HttpResponseMessage queryOutput(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS, route = "LargeOutputOrchestrator_Query_Output") 
            HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") 
            DurableClientContext durableContext,
            final ExecutionContext context) {
        DurableTaskClient client = durableContext.getClient();
        String instanceId = request.getQueryParameters().get("id");
        OrchestrationMetadata metadata = client.getInstanceMetadata(instanceId, true);

        if (metadata == null) {
            HttpResponseMessage response = request.createResponseBuilder(HttpStatus.NOT_FOUND)
                .body("Orchestration metadata not found.").build();
            return response;
        }

        Gson gson = DurableMetadataGsonProvider.createGson();
        String outputJson = gson.toJson(metadata);

        HttpResponseMessage response = request.createResponseBuilder(HttpStatus.OK)
            .header("Content-Type", "application/json")
            .body(outputJson)
            .build();
        return response;
    }

    private static String generateLargeString(int sizeInKB) {
        char[] chars = new char[sizeInKB * 1024];
        Arrays.fill(chars, 'A');
        return new String(chars);
    }
}