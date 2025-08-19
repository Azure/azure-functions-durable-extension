// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class ActivityInputType
{
    [Function(nameof(ActivityInputTypeOrchestrator))]
    public static async Task<List<string>> ActivityInputTypeOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var output = new List<string>();

        // Test byte array input
        byte[] byteArrayInput = new byte[] { 1, 2, 3, 4, 5 };
        output.Add(await context.CallActivityAsync<string>(nameof(ByteArrayInput), byteArrayInput));

        // Test empty byte array input
        byte[] emptyByteArray = new byte[0];
        output.Add(await context.CallActivityAsync<string>(nameof(ByteArrayInput), emptyByteArray));

        // Test single byte input
        byte singleByteInput = 42;
        output.Add(await context.CallActivityAsync<string>(nameof(SingleByteInput), singleByteInput));

        // Test custom class input
        var customClassInput = new CustomClass
        {
            Name = "Test",
            Age = 25,
            Data = new byte[] { 1, 2, 3 },
            Duration = TimeSpan.FromHours(1)
        };
        output.Add(await context.CallActivityAsync<string>(nameof(CustomClassInput), customClassInput));

        // Test int array input
        int[] intArrayInput = new int[] { 1, 2, 3, 4, 5 };
        output.Add(await context.CallActivityAsync<string>(nameof(IntArrayInput), intArrayInput));

        // Test string input
        string stringInput = "Test string input";
        output.Add(await context.CallActivityAsync<string>(nameof(StringInput), stringInput));

        // Test array of custom class input
        var complexInput = new CustomClass[]
        {
            new CustomClass { Name = "Test1", Age = 25, Data = new byte[] { 1, 2, 3 }, Duration = TimeSpan.FromMinutes(30) },
            new CustomClass { Name = "Test2", Age = 30, Data = new byte[0], Duration = TimeSpan.FromMinutes(45) }
        };
        output.Add(await context.CallActivityAsync<string>(nameof(CustomClassArrayInput), complexInput));

        return output;
    }

    [Function(nameof(ByteArrayInput))]
    public static string ByteArrayInput([ActivityTrigger] byte[] input, FunctionContext executionContext)
    {
        return $"Received byte[]: [{string.Join(", ", input)}]";
    }

    [Function(nameof(SingleByteInput))]
    public static string SingleByteInput([ActivityTrigger] byte input, FunctionContext executionContext)
    {
        return $"Received byte: {input}";
    }

    [Function(nameof(CustomClassInput))]
    public static string CustomClassInput([ActivityTrigger] CustomClass input, FunctionContext executionContext)
    {
        if (input.Data?.GetType() != typeof(byte[]))
        {
            return $"Error: Expected Data to be byte[] but got {input.Data!.GetType().Name}";
        }

        return $"Received CustomClass: {{Name: {input.Name}, Age: {input.Age}, Duration: {input.Duration}, Data: [{string.Join(", ", input.Data)}]}}";
    }

    [Function(nameof(IntArrayInput))]
    public static string IntArrayInput([ActivityTrigger] int[] input, FunctionContext executionContext)
    {
        return $"Received int[]: [{string.Join(", ", input)}]";
    }

    [Function(nameof(StringInput))]
    public static string StringInput([ActivityTrigger] string input, FunctionContext executionContext)
    {
        return $"Received string: {input}";
    }

    [Function(nameof(CustomClassArrayInput))]
    public static string CustomClassArrayInput([ActivityTrigger] CustomClass[] input, FunctionContext executionContext)
    {
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i].Data!.GetType() != typeof(byte[]))
            {
                return $"Error: Expected Data to be byte[] but got {input[i].Data!.GetType().Name}";
            }
        }

        var items = input.Select(item => 
            $"{{Name: {item.Name}, Age: {item.Age}, Duration: {item.Duration}, Data: [{string.Join(", ", item.Data!)}]}}");
        return $"Received CustomClass[]: [{string.Join(", ", items)}]";
    }
}

public class CustomClass
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public byte[]? Data { get; set; }
    public TimeSpan Duration { get; set; }
}
