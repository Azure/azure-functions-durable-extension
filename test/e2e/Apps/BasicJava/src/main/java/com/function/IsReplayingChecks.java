// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

import java.util.*;

import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

public class IsReplayingChecks {

    @FunctionName("IsReplayingEcho")
    public String isReplayingEcho(
            @DurableActivityTrigger(name = "value") String value,
            final ExecutionContext context) {
        // Bug: https://github.com/microsoft/durabletask-java/issues/235
        if (value != null && value.length() >= 2 && value.startsWith("\"") && value.endsWith("\"")) {
            value = value.substring(1, value.length() - 1);
        }
        return value;
    }

    @FunctionName("IsReplayingBasic")
    public Map<String, Object> isReplayingBasic(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext ctx) {
        boolean before = ctx.getIsReplaying();
        String result = ctx.callActivity("IsReplayingEcho", "hello", String.class).await();
        boolean after = ctx.getIsReplaying();

        Map<String, Object> output = new LinkedHashMap<>();
        output.put("before_activity", before);
        output.put("after_activity", after);
        output.put("activity_result", result);
        return output;
    }

    @FunctionName("IsReplayingMultiActivity")
    public Map<String, Object> isReplayingMultiActivity(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext ctx) {
        List<Map<String, Object>> snapshots = new ArrayList<>();

        Map<String, Object> s0 = new LinkedHashMap<>();
        s0.put("step", 0);
        s0.put("label", "start");
        s0.put("is_replaying", ctx.getIsReplaying());
        snapshots.add(s0);

        String r1 = ctx.callActivity("IsReplayingEcho", "one", String.class).await();

        Map<String, Object> s1 = new LinkedHashMap<>();
        s1.put("step", 1);
        s1.put("label", "after_first");
        s1.put("is_replaying", ctx.getIsReplaying());
        snapshots.add(s1);

        String r2 = ctx.callActivity("IsReplayingEcho", "two", String.class).await();

        Map<String, Object> s2 = new LinkedHashMap<>();
        s2.put("step", 2);
        s2.put("label", "after_second");
        s2.put("is_replaying", ctx.getIsReplaying());
        snapshots.add(s2);

        String r3 = ctx.callActivity("IsReplayingEcho", "three", String.class).await();

        Map<String, Object> s3 = new LinkedHashMap<>();
        s3.put("step", 3);
        s3.put("label", "after_third");
        s3.put("is_replaying", ctx.getIsReplaying());
        snapshots.add(s3);

        Map<String, Object> output = new LinkedHashMap<>();
        output.put("snapshots", snapshots);
        output.put("activities", Arrays.asList(r1, r2, r3));
        return output;
    }

    @FunctionName("IsReplayingConditionalLog")
    public Map<String, Object> isReplayingConditionalLog(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext ctx,
            final ExecutionContext executionContext) {
        int liveLogCount = 0;

        if (!ctx.getIsReplaying()) {
            executionContext.getLogger().info("IsReplayingConditionalLog: LIVE before activity");
            liveLogCount++;
        } else {
            executionContext.getLogger().info("IsReplayingConditionalLog: REPLAY before activity");
        }

        String result = ctx.callActivity("IsReplayingEcho", "logged", String.class).await();

        if (!ctx.getIsReplaying()) {
            executionContext.getLogger().info("IsReplayingConditionalLog: LIVE after activity");
            liveLogCount++;
        } else {
            executionContext.getLogger().info("IsReplayingConditionalLog: REPLAY after activity");
        }

        Map<String, Object> output = new LinkedHashMap<>();
        output.put("live_log_count", liveLogCount);
        output.put("activity_result", result);
        return output;
    }

    @FunctionName("IsReplayingCounter")
    public Map<String, Object> isReplayingCounter(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext ctx) {
        int nonReplayCount = 0;
        int replayCount = 0;

        if (ctx.getIsReplaying()) { replayCount++; } else { nonReplayCount++; }

        String r1 = ctx.callActivity("IsReplayingEcho", "a", String.class).await();
        if (ctx.getIsReplaying()) { replayCount++; } else { nonReplayCount++; }

        String r2 = ctx.callActivity("IsReplayingEcho", "b", String.class).await();
        if (ctx.getIsReplaying()) { replayCount++; } else { nonReplayCount++; }

        String r3 = ctx.callActivity("IsReplayingEcho", "c", String.class).await();
        if (ctx.getIsReplaying()) { replayCount++; } else { nonReplayCount++; }

        Map<String, Object> output = new LinkedHashMap<>();
        output.put("non_replay_count", nonReplayCount);
        output.put("replay_count", replayCount);
        output.put("total_checkpoints", nonReplayCount + replayCount);
        output.put("activities", Arrays.asList(r1, r2, r3));
        return output;
    }

    @FunctionName("IsReplayingFanOutFanIn")
    public Map<String, Object> isReplayingFanOutFanIn(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext ctx) {
        boolean before = ctx.getIsReplaying();

        Task<String> t1 = ctx.callActivity("IsReplayingEcho", "alpha", String.class);
        Task<String> t2 = ctx.callActivity("IsReplayingEcho", "beta", String.class);
        Task<String> t3 = ctx.callActivity("IsReplayingEcho", "gamma", String.class);
        ctx.allOf(Arrays.asList(t1, t2, t3)).await();

        boolean after = ctx.getIsReplaying();

        Map<String, Object> output = new LinkedHashMap<>();
        output.put("before_fan_out", before);
        output.put("after_fan_in", after);
        output.put("activities", Arrays.asList(t1.await(), t2.await(), t3.await()));
        return output;
    }
}
