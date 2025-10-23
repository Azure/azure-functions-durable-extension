// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Microsoft.Azure.Durable.Tests.E2E;

public class CustomExceptionPropertiesOrchestration
{
    [Function(nameof(OrchestrationWithCustomException))]
    public async Task<TaskFailureDetails?> OrchestrationWithCustomException([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Call the activity that will throw an exception
        try
        {
#pragma warning disable DURABLE2001 // Incorrect input type
            await context.CallActivityAsync(nameof(BusinessActivity));
#pragma warning restore DURABLE2001
        }
        catch (TaskFailedException ex)
        {
            return ex.FailureDetails;
        }

        // Should never reach here.
        return null;

    }

    [Function(nameof(BusinessActivity))]
    public void BusinessActivity([ActivityTrigger] TaskActivityContext context)
    {
        // Throw an exception with custom properties that should be captured
        throw new BusinessValidationException(
                message: "Business logic validation failed",
                stringProperty: "validation-error-123",
                intProperty: 100,
                longProperty: 999999999L,
                dateTimeProperty: new DateTime(2025, 10, 15, 14, 30, 0, DateTimeKind.Utc),
                dictionaryProperty: new Dictionary<string, object?>
                {
                    ["error_code"] = "VALIDATION_FAILED",
                    ["retry_count"] = 3,
                    ["is_critical"] = true
                },
                listProperty: new List<object?> { "error1", "error2", 500, null },
                nullProperty: null);
    }
}

[Serializable]
public class BusinessValidationException : Exception
{
    public BusinessValidationException(string message,
        string stringProperty,
        int intProperty,
        long longProperty,
        DateTime dateTimeProperty,
        IDictionary<string, object?> dictionaryProperty,
        IList<object?> listProperty,
        object? nullProperty) : base(message)
    {
        this.StringProperty = stringProperty;
        this.IntProperty = intProperty;
        this.LongProperty = longProperty;
        this.DateTimeProperty = dateTimeProperty;
        this.DictionaryProperty = dictionaryProperty;
        this.ListProperty = listProperty;
        this.NullProperty = nullProperty;
    }

    public string? StringProperty { get; }
    public int? IntProperty { get; }
    public long? LongProperty { get; }
    public DateTime? DateTimeProperty { get; }
    public IDictionary<string, object?>? DictionaryProperty { get; }
    public IList<object?>? ListProperty { get; }
    public object? NullProperty { get; }

    public BusinessValidationException(string message) : base(message) { }
}

