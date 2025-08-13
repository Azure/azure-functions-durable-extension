package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import java.time.Instant;
import java.util.*;
import java.util.logging.Logger;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;


/**
 * Azure Functions with HTTP Trigger to purge orchestration history.
 */
public class PurgeOrchestrationHistory {
    /**
     * This HTTP-triggered function purges orchestration history.
     */
    @FunctionName("PurgeOrchestrationHistory")
    public HttpResponseMessage purgeHistory(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {

        Logger logger = context.getLogger();
        logger.info("Starting purge all instance history");

        DurableTaskClient client = durableContext.getClient();

        // Parse optional query parameters for purgeStartTime and purgeEndTime
        String purgeStartTimeStr = request.getQueryParameters().get("purgeStartTime");
        String purgeEndTimeStr = request.getQueryParameters().get("purgeEndTime");
        Instant purgeStartTime = null;
        Instant purgeEndTime = null;
        try {
            if (purgeStartTimeStr != null && !purgeStartTimeStr.isEmpty()) {
                purgeStartTime = Instant.parse(purgeStartTimeStr);
            }
            if (purgeEndTimeStr != null && !purgeEndTimeStr.isEmpty()) {
                purgeEndTime = Instant.parse(purgeEndTimeStr);
            }
        } catch (Exception e) {
            logger.severe("Invalid date format for purgeStartTime or purgeEndTime: " + e.getMessage());
            return request.createResponseBuilder(HttpStatus.BAD_REQUEST)
                .header("Content-Type", "text/plain")
                .body("Invalid date format for purgeStartTime or purgeEndTime.")
                .build();
        }

        try {
            PurgeInstanceCriteria filter = new PurgeInstanceCriteria();
            filter.setCreatedTimeFrom(purgeStartTime);
            filter.setCreatedTimeTo(purgeEndTime);
            filter.setRuntimeStatusList(Arrays.asList(
                OrchestrationRuntimeStatus.COMPLETED,
                OrchestrationRuntimeStatus.FAILED,
                OrchestrationRuntimeStatus.TERMINATED
            ));

            PurgeResult purgeResult = client.purgeInstances(filter);

            logger.info("Finished purge all instance history");

            return request.createResponseBuilder(HttpStatus.OK)
                .header("Content-Type", "text/plain")
                .body("Purged " + purgeResult.getDeletedInstanceCount() + " records")
                .build();
        } catch (Exception ex) {
            logger.severe("Failed to purge all instance history: " + ex.getMessage());
            return request.createResponseBuilder(HttpStatus.INTERNAL_SERVER_ERROR)
                .header("Content-Type", "text/plain")
                .body("Failed to purge all instance history: " + ex.getMessage())
                .build();
        }
    }
}