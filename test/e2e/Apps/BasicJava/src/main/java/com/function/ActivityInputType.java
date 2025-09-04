package com.function;

import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;
import com.microsoft.durabletask.*;
import com.microsoft.durabletask.azurefunctions.DurableActivityTrigger;
import com.microsoft.durabletask.azurefunctions.DurableOrchestrationTrigger;

import java.util.*;
import java.time.Duration;


/**
 * Durable Functions E2E test for activity input types.
 */
public class ActivityInputType {

    /**
     * Orchestrator function that tests various activity input types.
     */
    @FunctionName("ActivityInputTypeOrchestrator")
    public List<String> activityInputTypeOrchestrator(
            @DurableOrchestrationTrigger(name = "context") TaskOrchestrationContext context) {
        List<String> output = new ArrayList<>();

        // Test byte array input
        List<Byte> byteArrayInput = Arrays.asList((byte) 1, (byte) 2, (byte) 3, (byte) 4, (byte) 5);
        output.add(context.callActivity("ByteArrayInput", byteArrayInput, String.class).await());

        // Test empty byte array input
        List<Byte> emptyByteArray = new ArrayList<>();
        output.add(context.callActivity("ByteArrayInput", emptyByteArray, String.class).await());

        // Test single byte input
        byte singleByteInput = 42;
        output.add(context.callActivity("SingleByteInput", singleByteInput, String.class).await());

        // Test custom class input
        CustomClass customClassInput = new CustomClass();
        customClassInput.setName("Test");
        customClassInput.setAge(25);
        customClassInput.setData(Arrays.asList((byte) 1, (byte) 2, (byte) 3));
        customClassInput.setDuration(Duration.ofHours(1).toMillis());
        output.add(context.callActivity("CustomClassInput", customClassInput, String.class).await());

        // Test int array input
        int[] intArrayInput = new int[] { 1, 2, 3, 4, 5 };
        output.add(context.callActivity("IntArrayInput", intArrayInput, String.class).await());

        // Test string input
        String stringInput = "Test string input";
        output.add(context.callActivity("StringInput", stringInput, String.class).await());

        // Test array of custom class input
        List<CustomClass> complexInput = Arrays.asList(
            new CustomClass("Test1", 25, Arrays.asList((byte) 1, (byte) 2, (byte) 3), Duration.ofMinutes(30)),
            new CustomClass("Test2", 30, new ArrayList<>(), Duration.ofMinutes(45))
        );
        output.add(context.callActivity("CustomClassArrayInput", complexInput, String.class).await());

        return output;
    }

    /**
     * Activity: Receives a byte array.
     */
    @FunctionName("ByteArrayInput")
    public String byteArrayInput(
            @DurableActivityTrigger(name = "input") List<Byte> input,
            final ExecutionContext context) {
        return "Received byte[]: " + input;
    }

    /**
     * Activity: Receives a single byte.
     */
    @FunctionName("SingleByteInput")
    public String singleByteInput(
            @DurableActivityTrigger(name = "input") byte input,
            final ExecutionContext context) {
        return "Received byte: " + input;
    }

    /**
     * Activity: Receives a custom class.
     */
    @FunctionName("CustomClassInput")
    public String customClassInput(
            @DurableActivityTrigger(name = "input") CustomClass input,
            final ExecutionContext context) {
        if (input.getData() == null || !(input.getData() instanceof List<Byte>)) {
            return "Error: Expected Data to be byte[] but got " + (input.getData() == null ? "null" : input.getData().getClass().getSimpleName());
        }
        return "Received CustomClass: " + input.toString();
    }

    /**
     * Activity: Receives an int array.
     */
    @FunctionName("IntArrayInput")
    public String intArrayInput(
            @DurableActivityTrigger(name = "input") int[] input,
            final ExecutionContext context) {
        return "Received int[]: [" + joinInts(input) + "]";
    }

    /**
     * Activity: Receives a string.
     */
    @FunctionName("StringInput")
    public String stringInput(
            @DurableActivityTrigger(name = "input") String input,
            final ExecutionContext context) {
        input = input.substring(1, input.length() - 1); // Bug: Double serialization issue
        return "Received string: " + input;
    }

    /**
     * Activity: Receives an array of custom class.
     */
    @FunctionName("CustomClassArrayInput")
    public String customClassArrayInput(
            @DurableActivityTrigger(name = "input") List<CustomClass> input,
            final ExecutionContext context) {
        for (CustomClass item : input) {
            if (item.getData() == null || !(item.getData() instanceof List<Byte>)) {
                return "Error: Expected Data to be byte[] but got " + (item.getData() == null ? "null" : item.getData().getClass().getSimpleName());
            }
        }
        return "Received CustomClass[]: " + input.toString();
    }

    // Helper to join int arrays as comma-separated string
    private static String joinInts(int[] arr) {
        if (arr == null || arr.length == 0) return "";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < arr.length; i++) {
            if (i > 0) sb.append(", ");
            sb.append(arr[i]);
        }
        return sb.toString();
    }
}

/**
 * Custom class for activity input.
 */
class CustomClass {
    private String name;
    private int age;
    private List<Byte> data;
    // As of Java 16, reflective access to Duration (and all Java base modules) was restricted,
    // so serialization of objects with a Duration field is not possible directly. Due to this, we
    // store the duration as milliseconds for seralization/deserialization.
    private long duration;

    public CustomClass() {}

    public CustomClass(String name, int age, List<Byte> data, Duration duration) {
        this.name = name;
        this.age = age;
        this.data = data;
        this.duration = duration == null ? 0 : duration.toMillis();
    }

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }

    public int getAge() { return age; }
    public void setAge(int age) { this.age = age; }

    public List<Byte> getData() { return data; }
    public void setData(List<Byte> data) { this.data = data; }

    public long getDuration() { return duration; }
    public void setDuration(long duration) { this.duration = duration; }
    
    public String toString() {
        return String.format("{Name: %s, Age: %d, Duration: %s, Data: %s}",
            name, age, formatDuration(), data);
    }

    private String formatDuration() {
        Duration duration = Duration.ofMillis(this.duration);

        long hours = duration.toHours();
        long minutes = duration.toMinutesPart();
        long seconds = duration.toSecondsPart();

        String formatted = String.format("%02d:%02d:%02d", hours, minutes, seconds);
        return formatted;
    }
}