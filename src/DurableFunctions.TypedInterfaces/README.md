# CodeGeneration for Durable Functions

Assists in performing Roslyn-based code generation for apps using the [Durable Function](https://github.com/Azure/azure-functions-durable-extension) extension for the [Durable Task](https://github.com/Azure/durabletask) framework. Automatically keeps dependencies between orchestration/actvity functions up to date as your code evolves.

Supports design-time code generation that responds to changes made in hand-authored files by generating new code that shows up to Intellisense as soon as you type.

## What It Does

Automatically generates method stubs that correspond to the contracts of Orchestration/Activity functions it finds within the project. Ensures that the return type, functionName, and parameters all stay up to date as your project grows.

## How to use

1. Add the project's nuget package as a dependency.

For example: 

```xml
<PackageReference Include="DurableFunctions.TypedInterfaces" Version="0.1.0-preview" />
```

*The project should now automatically generate code for Orchestration/Activity functions.*

2. *Optionally*, add the following statements to a property group in your project's csproj in order to see the generated files.

```xml
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
<CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
```

3. Add an Orchestration/Activity function to your project.

```csharp
[FunctionName("SayHello")]
public static string SayHello([ActivityTrigger] IDurableActivityContext context, ILogger log)
{
    var name = context.GetInput<string>();

    log.LogInformation($"Saying hello to {name}.");
    return $"Hello {name}!";
}
```

4. Manually add using statement to reference the generated code.

```
using Microsoft.Azure.WebJobs.Extensions.DurableTask.TypedInterfaces;
```

5. Replace ```IDurableClient``` / ```IDurableOrchestrationContext``` usage for their generated counterparts ```ITypedDurableClient``` / ```ITypedDurableOrchestrationContext```. The generated interfaces can perform all operations exposed by standard interfaces, in additional to performing typed calls to Orchestration/Activity Function.

Some example Activity calls through the generated interface:

```csharp
[FunctionName("SimpleOrchestration")]
public static async Task<List<string>> SimpleOrchestrator(
    [OrchestrationTrigger] ITypedDurableOrchestrationContext context)
{
    var outputs = new List<string>();

    // Replace "hello" with the name of your Durable Activity Function.
    outputs.Add(await context.Activity.SayHello("Tokyo"));
    outputs.Add(await context.Activity.SayHello("Seattle"));
    outputs.Add(await context.Activity.SayHello("London"));

    return outputs;
}
```

## The Problem

As the complexity of your app leveraging the [Durable Function](https://github.com/Azure/azure-functions-durable-extension) extension grows, it becomes increasingly difficult to track and update calls between dependent orchestrations/activities. It would be ideal to have compile time checks that ensure the expected contract still holds between the two durable functions.

In general, there are three points of failure for updating dependencies every time a Orchestration/Activity function is updated:
    
* Return Type mismatch
* Function Name changed
* Parameters mismatch

While Function Name can be abstracted to shared constant values, it becomes tricky to effectively validate return types and parameters for all calls.

Problems generally crop up in two places in Orchestrator functions that manage sub Orchestrator/Activity functions. It manifests in the Orchestrator function itself, and also when writing unit tests that attempt to mock the calls an Orchestration makes.

In the example below, the ```Multiply``` Orchestration depends on the ```Add``` Activity.

```csharp
[FunctionName("Add")]
public Task<int> Add(
    [ActivityTrigger] IDurableActivityContext context
)
{
    var (num1, num2) = context.GetInput<(int, int)>();

    return Task.FromResult(num1 + num2);
}

[FunctionName("Multiply")]
public async Task<int> Multiply(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var (num1, num2) = context.GetInput<(int, int)>();

    var result = 0;

    for (var i = 0; i < num2; i++)
    {
        // Depends on Add
        result += await context.CallActivityAsync<int>("Add", (result, num1));
    }

    return result;
}
```

In another example, the test for the ```Multiply``` Orchestration indirectly depends on the ```Add``` Activity.

```csharp
[Fact]
public async Task MultiplyTest()
{
    // Arrange
    var num1 = 5;
    var num2 = 10;
    var answer = num1 * num2;

    var mockOrchestrationContext = new Mock<IDurableOrchestrationContext>(MockBehavior.Strict);
    mockOrchestrationContext.Setup(s => s.GetInput<(int, int)>()).Returns((5, 10));
    // Depends on Add
    mockOrchestrationContext.Setup(s => s.CallActivityAsync<int>("Add", new Tuple<int, int>(num1, num2))).ReturnsAsync(answer);
    var context = mockOrchestrationContext.Object;

    var calculator = new Calculator();

    // Act
    var result = await calculator.Multiply(context);

    // Assert
    Assert.Equal(answer, result);
    mockOrchestrationContext.Verify(c => c.GetInput<(int, int)>(), Times.Once);
    mockOrchestrationContext.Verify(c => c.CallActivityAsync<int>("Add", new Tuple<int, int>(num1, num2)), Times.Once);
}
```

## The Solution

Using the recently released resource Roslyn-based [Source Generators](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/) we can automatically generate code to abstract the orchestration/activity call. 

The function name, duable function type (orchestration/activity), parameters (names/types) and return type call can all be discovered by statically analyzing the code. 

An ```ITypedDurableOrchestrationCaller``` is generated for representing all Orchestration function calls in the project.

An ```ITypedDurableActivityCaller``` is generated for representing all Activity function calls in the project.

An ```ITypedDurableOrchestrationStarter``` is generated for representing all Orchestration function starts in the project.

An ```ITypedDurableOrchestrationContext``` interface is generated representing an interface for wrapping all operations against previous usage of ```IDurableOrchestrationContext``` and supporting calling Orchestration/Activity functions in the project - exposing an ```ITypedDurableOrchestrationCaller``` / ```ITypedDurableActivityCaller```.

An ```ITypedDurableClient``` interface is generated representing an interface for wrapping all operations against previous usage of ```IDurableClient``` and supporting starting Orchestration functions in the project - exposing ```ITypedDurableOrchestrationStarter```.

Paired concrete implementations of the preceding interfaces are also generated to automatically handle forwarding all the calls appropriate through the pre-existing operations exposed by the DurableTask framework.

```csharp
public partial interface ITypedDurableActivityCaller
{
    /// <summary>
    /// See <see cref = "WebJobs.Extensions.DurableTask.CodeGen.Example.Calculator.Add"/>
    /// </summary>
    Task<int> Add(int num1, int num2);
}
```

```csharp
public partial interface ITypedDurableActivityCaller
{
    /// <summary>
    /// See <see cref = "WebJobs.Extensions.DurableTask.CodeGen.Example.Calculator.Add"/>
    /// </summary>
    Task<int> Add(int num1, int num2) 
    {
        return context.CallActivityAsync<int>("Add", (num1, num2));
    }
}
```

```csharp
[FunctionName("Multiply")]
public async Task<int> Multiply(
    [OrchestrationTrigger] ITypedDurableOrchestrationContext context
)
{
    var (num1, num2) = context.GetInput<(int, int)>();

    var result = 0;

    for (var i = 0; i < num2; i++)
    {
        result = await context.Activities.Add(result, num1);
    }

    return result;
}
```

## Limitations

### 1. Manually Adding Namespace

For the moment, generated code will not show up intellisense unless the namespace containing the code is added to the file you are trying to use them in. Generated code is placed in the namespace ```Microsoft.Azure.Webjobs.Extensions.DurableTask.TypedInterfaces```. 

In order to use the new typed interfaces, you must manually include the using statement:
```csharp
using Microsoft.Azure.WebJobs.Extensions.DurableTask.TypedInterfaces;
```

### 2. Scoped Code Generation

Ideally, we would only update the generated code scoped to what the user is actively making changes to. Currently, SourceGenerators take an all or nothing approach - all code must be regenerated for all functions every update. However, the Roslyn team plans to add more granular support for creating code that can be scoped/udpated to have increased performance.
