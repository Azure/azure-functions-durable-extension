package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

/**
 * Disabled-but-still-deployed activity plus an orchestrator that schedules it. Used by
 * DisabledOrchestrationTests to validate that dispatching a disabled activity fails the orchestration
 * deterministically instead of poison-looping forever.
 * See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
 */
public class DisabledActivity {
    /**
     * Orchestrator that schedules the disabled-but-still-deployed DisabledActivity. Because the
     * activity has no active listener, the dispatch must fail the orchestration deterministically.
     */
    @FunctionName("CallDisabledActivity")
    public String callDisabledActivity(
            @DurableOrchestrationTrigger(name = "ctx") TaskOrchestrationContext ctx) {
        return ctx.callActivity("DisabledActivity", "hello", String.class).await();
    }

    /**
     * Activity function DisabledActivity. Disabled at runtime via the
     * AzureWebJobs.DisabledActivity.Disabled app setting in local.settings.json, so no listener is
     * started for it even though it is still deployed/indexed.
     */
    @FunctionName("DisabledActivity")
    public String disabledActivity(
            @DurableActivityTrigger(name = "input") String input) {
        return input;
    }
}
