package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.function.exceptions.BusinessValidationException;
import com.microsoft.azure.functions.*;

import java.time.Instant;
import java.util.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;


/**
 * Azure Functions with Durable Task for custom exception properties in failure details.
 */
public class CustomExceptionPropertiesOrchestration {

    /**
     * Orchestrator: OrchestrationWithCustomException
     * Calls BusinessActivity and catches TaskFailedException, returning the FailureDetails.
     */
    @FunctionName("OrchestrationWithCustomException")
    public FailureDetails orchestrationWithCustomException(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        try {
            ctx.callActivity("BusinessActivity", null, Void.class).await();
        } catch (TaskFailedException ex) {
            return ex.getErrorDetails();
        }

        // Should never reach here.
        return null;
    }

    /**
     * Activity: BusinessActivity
     * Throws a BusinessValidationException with custom properties.
     */
    @FunctionName("BusinessActivity")
    public void businessActivity(
            @DurableActivityTrigger(name = "input") String input,
            final ExecutionContext context) throws BusinessValidationException {
        Map<String, Object> dictionaryProperty = new LinkedHashMap<>();
        dictionaryProperty.put("error_code", "VALIDATION_FAILED");
        dictionaryProperty.put("retry_count", 3);
        dictionaryProperty.put("is_critical", true);

        List<Object> listProperty = Arrays.asList("error1", "error2", 500, null);

        throw new BusinessValidationException(
                "Business logic validation failed",
                "validation-error-123",
                100,
                999999999L,
                Instant.parse("2025-10-15T14:30:00Z"),
                dictionaryProperty,
                listProperty,
                null);
    }
}
