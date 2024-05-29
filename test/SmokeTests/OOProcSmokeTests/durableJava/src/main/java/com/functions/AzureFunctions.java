package com.functions;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;
import java.util.*;

import com.microsoft.durabletask.DurableTaskClient;
import com.microsoft.durabletask.OrchestrationRunner;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableClientContext;
import com.microsoft.durabletask.azurefunctions.DurableClientInput;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

/**
 * Azure Durable Functions with HTTP trigger.
 */
public class AzureFunctions {
    /**
     * This HTTP-triggered function starts the orchestration.
     */
    @FunctionName("StartOrchestration")
    public HttpResponseMessage startOrchestration(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            @DurableClientInput(name = "durableContext") DurableClientContext durableContext,
            final ExecutionContext context) {
        context.getLogger().info("Java HTTP trigger processed a request.");

        DurableTaskClient client = durableContext.getClient();
        String instanceId = client.scheduleNewOrchestrationInstance("Cities");
        context.getLogger().info("Created new Java orchestration with instance ID = " + instanceId);
        return durableContext.createCheckStatusResponse(request, instanceId);
    }

    /**
     * This is the orchestrator function. The OrchestrationRunner.loadAndRun() static
     * method is used to take the function input and execute the orchestrator logic.
     */
    @FunctionName("Cities")
    public String citiesOrchestrator(
            @DurableOrchestrationTrigger(name = "orchestratorRequestProtoBytes") String orchestratorRequestProtoBytes) {
        return OrchestrationRunner.loadAndRun(orchestratorRequestProtoBytes, ctx -> {
            String result = "";
            String[] cities = {"Dubai", "New York", "Vancouver"};
            City paris = new City("France", "Paris");
            
            result += ctx.callActivity("Capitalize", "Tokyo", String.class).await() + ", ";
            result += ctx.callActivity("Capitalize", "London", String.class).await() + ", ";
            result += ctx.callActivity("Capitalize", "Seattle", String.class).await() + ", ";
            result += ctx.callActivity("Capitalize", "Austin", String.class).await()+ ", ";

            result += ctx.callActivity("Print", 123, String.class).await()+ ", ";
            result += ctx.callActivity("PrintArray", cities, String.class).await()+ ", ";
            result += ctx.callActivity("PrintObject", paris, String.class).await()+ ", ";

            return result;
        });
    }

    /**
     * This is the activity function that gets invoked by the orchestration.
     */
    @FunctionName("Capitalize")
    public String capitalize(
            @DurableActivityTrigger(name = "name") String name,
            final ExecutionContext context) {
        context.getLogger().info("Capitalizing: " + name);
        return name.toUpperCase();
    }

    @FunctionName("Print")
    public String print(
            @DurableActivityTrigger(name = "input") String input,
            final ExecutionContext context) {
        context.getLogger().info("Printing input: " + input);
        return input.toString();
    }

    @FunctionName("PrintArray")
    public String printArray(
            @DurableActivityTrigger(name = "array") String[] array,
            final ExecutionContext context) {
        context.getLogger().info(Arrays.toString(array));
        return Arrays.toString(array);
    }

    @FunctionName("PrintObject")
    public String printObject(
            @DurableActivityTrigger(name = "city") City city,
            final ExecutionContext context) {
        context.getLogger().info("Printing object" + city.toString());
        return city.toString();
    }

    public class City {
        private String country;
        private String name;

        public City(String country, String name){
            this.country = country;
            this.name = name;
        }

        public String getCountry() {
            return country;
        }

        public void setCountry(String country) {
            this.country = country;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        @Override
        public String toString() {
            return "City [Country=" + country + ", name=" + name + "]";
        }
    }
}
