// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.TypedInterfaces;
using WebJobs.Extensions.DurableTask.CodeGen.Example.Models;

namespace WebJobs.Extensions.DurableTask.CodeGen.Example
{
    // Used by DurableTask.CodeGeneration.Tests
    // DO NOT MODIFY - unless updating the tests
    public class Calculator
    {
        [FunctionName("Identity")]
        public Task<int> Identity(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            var num1 = context.GetInput<int>();

            return Task.FromResult(num1);
        }

        [FunctionName("AddLists")]
        public Task<int> AddLists(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            var (nums1, nums2) = context.GetInput<(List<int>, List<int>)>();

            return Task.FromResult(nums1.Sum() + nums2.Sum());
        }

        [FunctionName("AddList")]
        public Task<int> AddList(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            var nums = context.GetInput<List<int>>();

            return Task.FromResult(nums.Sum());
        }

        [FunctionName("AddListComplex")]
        public Task<ComplexNumber> AddListComplex(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            var (nums1, nums2) = context.GetInput<(List<ComplexNumber>, List<ComplexNumber>)>();

            return Task.FromResult(new ComplexNumber());
        }

        [FunctionName("AddComplex")]
        public Task<ComplexNumber> AddComplex(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            var (num1, num2) = context.GetInput<(ComplexNumber, ComplexNumber)>();

            var result = new ComplexNumber()
            {
                Img = num1.Img + num2.Img,
                Real = num1.Real + num2.Real
            };

            return Task.FromResult(result);
        }

        // Don't add function name attribute
        public Task<int> Subtract(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            var (num1, num2) = context.GetInput<(int, int)>();

            return Task.FromResult(num1 - num2);
        }

        // Don't add trigger attribute
        [FunctionName("Divide")]
        public Task Divide(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            return Task.FromResult(0);
        }

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
        // This project intentionally violates this analyzer
# pragma warning disable DF0201
            [OrchestrationTrigger] ITypedDurableOrchestrationContext context
# pragma warning restore DF0201
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

        [FunctionName("Log")]
        public Task<int> Log(
            [ActivityTrigger] IDurableActivityContext context
        )
        {
            var num = context.GetInput<int>();

            return Task.FromResult((int)MathF.Log(num));
        }
    }
}
