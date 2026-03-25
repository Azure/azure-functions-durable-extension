// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Moq.Language;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class MoqExtensions
    {
        public static ISetupSequentialResult<TResult> ReturnsSequence<TResult>(this ISetupSequentialResult<TResult> sequentialResult, IEnumerable<TResult> results)
        {
            ISetupSequentialResult<TResult> result = sequentialResult;
            foreach (TResult r in results)
            {
                result = result.Returns(r);
            }

            return result;
        }

        public static ISetupSequentialResult<Task<TResult>> ReturnsSequenceAsync<TResult>(this ISetupSequentialResult<Task<TResult>> sequentialResult, IEnumerable<TResult> results)
        {
            ISetupSequentialResult<Task<TResult>> result = sequentialResult;
            foreach (TResult r in results)
            {
                result = result.ReturnsAsync(r);
            }

            return result;
        }

        public static ISetupSequentialResult<ValueTask<TResult>> ReturnsSequenceAsync<TResult>(this ISetupSequentialResult<ValueTask<TResult>> sequentialResult, IEnumerable<TResult> results)
        {
            ISetupSequentialResult<ValueTask<TResult>> result = sequentialResult;
            foreach (TResult r in results)
            {
                result = result.Returns(new ValueTask<TResult>(r));
            }

            return result;
        }
    }
}