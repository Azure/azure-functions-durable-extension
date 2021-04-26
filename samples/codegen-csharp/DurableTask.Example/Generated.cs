using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Example.Math;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations
{
    public partial interface IDurableOrchestrationCaller
    {
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Identity"/>
        /// </summary>
        Task<int> Identity(IDurableOrchestrationContext context, int num1);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Identity"/>
        /// </summary>
        Task<int> IdentityWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num1);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddLists"/>
        /// </summary>
        Task<int> AddLists(IDurableOrchestrationContext context, List<int> nums1, List<int> nums2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddLists"/>
        /// </summary>
        Task<int> AddListsWithRetry(IDurableOrchestrationContext context, RetryOptions options, List<int> nums1, List<int> nums2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddList"/>
        /// </summary>
        Task<int> AddList(IDurableOrchestrationContext context, List<int> nums);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddList"/>
        /// </summary>
        Task<int> AddListWithRetry(IDurableOrchestrationContext context, RetryOptions options, List<int> nums);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddListComplex"/>
        /// </summary>
        Task<ComplexNumber> AddListComplex(IDurableOrchestrationContext context, List<ComplexNumber> nums1, List<ComplexNumber> nums2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddListComplex"/>
        /// </summary>
        Task<ComplexNumber> AddListComplexWithRetry(IDurableOrchestrationContext context, RetryOptions options, List<ComplexNumber> nums1, List<ComplexNumber> nums2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddComplex"/>
        /// </summary>
        Task<ComplexNumber> AddComplex(IDurableOrchestrationContext context, ComplexNumber num1, ComplexNumber num2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddComplex"/>
        /// </summary>
        Task<ComplexNumber> AddComplexWithRetry(IDurableOrchestrationContext context, RetryOptions options, ComplexNumber num1, ComplexNumber num2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Add"/>
        /// </summary>
        Task<int> Add(IDurableOrchestrationContext context, int num1, int num2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Add"/>
        /// </summary>
        Task<int> AddWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num1, int num2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        Task<int> Multiply(IDurableOrchestrationContext context, int num1, int num2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        Task<int> MultiplyWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num1, int num2);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        string StartMultiply(IDurableOrchestrationContext context, int num1, int num2, string instanceId = null);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        Task<string> StartMultiply(IDurableClient client, int num1, int num2, string instanceId = null);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Log"/>
        /// </summary>
        Task<int> Log(IDurableOrchestrationContext context, int num);
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Log"/>
        /// </summary>
        Task<int> LogWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num);
    }

    public partial class DurableOrchestrationCaller : IDurableOrchestrationCaller
    {
        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Identity"/>
        /// </summary>
        public Task<int> Identity(IDurableOrchestrationContext context, int num1)
        {
            return context.CallActivityAsync<int>("Identity", num1);
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Identity"/>
        /// </summary>
        public Task<int> IdentityWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num1)
        {
            return context.CallActivityWithRetryAsync<int>("Identity", options, num1);
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddLists"/>
        /// </summary>
        public Task<int> AddLists(IDurableOrchestrationContext context, List<int> nums1, List<int> nums2)
        {
            return context.CallActivityAsync<int>("AddLists", (nums1, nums2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddLists"/>
        /// </summary>
        public Task<int> AddListsWithRetry(IDurableOrchestrationContext context, RetryOptions options, List<int> nums1, List<int> nums2)
        {
            return context.CallActivityWithRetryAsync<int>("AddLists", options, (nums1, nums2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddList"/>
        /// </summary>
        public Task<int> AddList(IDurableOrchestrationContext context, List<int> nums)
        {
            return context.CallActivityAsync<int>("AddList", nums);
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddList"/>
        /// </summary>
        public Task<int> AddListWithRetry(IDurableOrchestrationContext context, RetryOptions options, List<int> nums)
        {
            return context.CallActivityWithRetryAsync<int>("AddList", options, nums);
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddListComplex"/>
        /// </summary>
        public Task<ComplexNumber> AddListComplex(IDurableOrchestrationContext context, List<ComplexNumber> nums1, List<ComplexNumber> nums2)
        {
            return context.CallActivityAsync<ComplexNumber>("AddListComplex", (nums1, nums2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddListComplex"/>
        /// </summary>
        public Task<ComplexNumber> AddListComplexWithRetry(IDurableOrchestrationContext context, RetryOptions options, List<ComplexNumber> nums1, List<ComplexNumber> nums2)
        {
            return context.CallActivityWithRetryAsync<ComplexNumber>("AddListComplex", options, (nums1, nums2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddComplex"/>
        /// </summary>
        public Task<ComplexNumber> AddComplex(IDurableOrchestrationContext context, ComplexNumber num1, ComplexNumber num2)
        {
            return context.CallActivityAsync<ComplexNumber>("AddComplex", (num1, num2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.AddComplex"/>
        /// </summary>
        public Task<ComplexNumber> AddComplexWithRetry(IDurableOrchestrationContext context, RetryOptions options, ComplexNumber num1, ComplexNumber num2)
        {
            return context.CallActivityWithRetryAsync<ComplexNumber>("AddComplex", options, (num1, num2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Add"/>
        /// </summary>
        public Task<int> Add(IDurableOrchestrationContext context, int num1, int num2)
        {
            return context.CallActivityAsync<int>("Add", (num1, num2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Add"/>
        /// </summary>
        public Task<int> AddWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num1, int num2)
        {
            return context.CallActivityWithRetryAsync<int>("Add", options, (num1, num2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        public Task<int> Multiply(IDurableOrchestrationContext context, int num1, int num2)
        {
            return context.CallSubOrchestratorAsync<int>("Multiply", (num1, num2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        public Task<int> MultiplyWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num1, int num2)
        {
            return context.CallSubOrchestratorWithRetryAsync<int>("Multiply", options, (num1, num2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        public string StartMultiply(IDurableOrchestrationContext context, int num1, int num2, string instanceId = null)
        {
            return context.StartNewOrchestration("Multiply", (num1, num2), instanceId);
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Multiply"/>
        /// </summary>
        public Task<string> StartMultiply(IDurableClient client, int num1, int num2, string instanceId = null)
        {
            return client.StartNewAsync("Multiply", instanceId, (num1, num2));
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Log"/>
        /// </summary>
        public Task<int> Log(IDurableOrchestrationContext context, int num)
        {
            return context.CallActivityAsync<int>("Log", num);
        }

        /// <summary>
        /// See <see cref = "DurableTask.Example.Calculator.Log"/>
        /// </summary>
        public Task<int> LogWithRetry(IDurableOrchestrationContext context, RetryOptions options, int num)
        {
            return context.CallActivityWithRetryAsync<int>("Log", options, num);
        }
    }
}
