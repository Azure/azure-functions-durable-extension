package com.function;

import java.time.Instant;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.function.exceptions.BusinessValidationException;
import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.durabletask.FailureDetails;
import com.microsoft.durabletask.TaskFailedException;
import com.microsoft.durabletask.TaskOrchestrationContext;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

/**
 * Validates the custom-exception-properties feature for Durable Functions Java.
 *
 * <p>The {@code BusinessActivity} throws a {@link BusinessValidationException} carrying structured
 * properties. A registered {@link BusinessExceptionPropertiesProvider} attaches those properties to
 * the failure, which the Durable Task host extension surfaces on {@code FailureDetails.Properties}.
 * The orchestration catches the propagated failure and returns its {@link FailureDetails} so the
 * e2e test can assert the custom properties survived the round-trip. Mirrors the BasicNode and
 * BasicDotNetIsolated reference implementations.
 */
public class CustomExceptionProperties {

    /**
     * Orchestrator: OrchestrationWithCustomException.
     */
    @FunctionName("OrchestrationWithCustomException")
    public FailureDetails orchestrationWithCustomException(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        try {
            ctx.callActivity("BusinessActivity", ctx.getInstanceId(), String.class).await();
            return null;
        } catch (TaskFailedException ex) {
            return ex.getErrorDetails();
        }
    }

    /**
     * Activity: BusinessActivity - throws an exception carrying custom properties.
     */
    @FunctionName("BusinessActivity")
    public String businessActivity(
            @DurableActivityTrigger(name = "instanceId") String instanceId,
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
