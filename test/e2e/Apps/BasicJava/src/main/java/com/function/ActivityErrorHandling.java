package com.function;


import com.microsoft.azure.functions.annotation.*;
import com.function.exceptions.InvalidOperationException;
import com.function.exceptions.OverflowException;
import com.microsoft.azure.functions.*;

import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;


/**
 * Azure Functions with Durable Task for activity error handling.
 */
public class ActivityErrorHandling {
    // Equivalent to static ConcurrentDictionary<string, int>
    private static final ConcurrentHashMap<String, AtomicInteger> globalRetryCount = new ConcurrentHashMap<>();

    /**
     * Orchestrator: RethrowActivityException
     */
    @FunctionName("RethrowActivityException")
    public String rethrowActivityException(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        // Calls RaiseException and lets any exception propagate
        return ctx.callActivity("RaiseException", ctx.getInstanceId(), String.class).await();
    }

    /**
     * Orchestrator: CatchActivityException
     */
    @FunctionName("CatchActivityException")
    public String catchActivityException(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        try {
            return ctx.callActivity("RaiseException", ctx.getInstanceId(), String.class).await();
        } catch (TaskFailedException ex) {
            return ex.getMessage();
        }
    }

    /**
     * Orchestrator: CatchActivityExceptionFailureDetails
     */
    @FunctionName("CatchActivityExceptionFailureDetails")
    public FailureDetails catchActivityExceptionFailureDetails(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        try {
            ctx.callActivity("RaiseException", ctx.getInstanceId(), String.class).await();
            return null;
        } catch (TaskFailedException ex) {
            return ex.getErrorDetails();
        }
    }

    /**
     * Orchestrator: RetryActivityFunction
     */
    @FunctionName("RetryActivityFunction")
    public String retryActivityFunction(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        RetryPolicy retryPolicy = new RetryPolicy(3, java.time.Duration.ofSeconds(3));
        TaskOptions options = new TaskOptions(retryPolicy);
        return ctx.callActivity("RaiseException", ctx.getInstanceId(), options, String.class).await();
    }

    /**
     * Orchestrator: CustomRetryActivityFunction
     */
    @FunctionName("CustomRetryActivityFunction")
    public String customRetryActivityFunction(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        TaskOptions options = new TaskOptions(retryContext -> {
            FailureDetails lastFailure = retryContext.getLastFailure();
            if (lastFailure != null
                    // BUG: https://github.com/microsoft/durabletask-java/issues/238
                    // BUG: https://github.com/microsoft/durabletask-java/issues/239
                    && lastFailure.getErrorType().equals("OverflowException")
                    // && lastFailure.isCausedBy(InvalidOperationException.class)
                    // && lastFailure.getInnerFailure() != null
                    // && lastFailure.getInnerFailure().isCausedBy("java.lang.OverflowException")
                    && retryContext.getLastAttemptNumber() < 3) {
                return true;
            }
            return false;
        });
        return ctx.callActivity("RaiseComplexException", ctx.getInstanceId(), options, String.class).await();
    }

    /**
     * Activity: RaiseException
     * @throws InvalidOperationException 
     */
    @FunctionName("RaiseException")
    public String raiseException(
            @DurableActivityTrigger(name = "instanceId") String instanceId,
            final ExecutionContext context) throws InvalidOperationException {
        AtomicInteger count = globalRetryCount.computeIfAbsent(instanceId, k -> new AtomicInteger(0));
        int current = count.incrementAndGet();
        if (current == 1) {
            throw new InvalidOperationException("This activity failed");
        } else {
            return "Success";
        }
    }

    /**
     * Activity: RaiseComplexException
     * @throws InvalidOperationException 
     */
    @FunctionName("RaiseComplexException")
    public String raiseComplexException(
            @DurableActivityTrigger(name = "instanceId") String instanceId,
            final ExecutionContext context) throws InvalidOperationException {
        AtomicInteger count = globalRetryCount.computeIfAbsent(instanceId, k -> new AtomicInteger(0));
        int current = count.incrementAndGet();
        if (current == 1) {
            OverflowException inner = new OverflowException("Inner exception message");
            InvalidOperationException ex = new InvalidOperationException(
                "This activity failed\r\nMore information about the failure", inner);
            throw ex;
        } else {
            return "Success";
        }
    }
}