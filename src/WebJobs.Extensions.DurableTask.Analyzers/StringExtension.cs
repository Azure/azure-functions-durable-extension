// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public static class StringExtensions
    {
        public static int LevenshteinDistance(this string baseString, string comparisonString)
        {
            int baseLength = baseString.Length;
            int comparisonLength = comparisonString.Length;
            int[,] d = new int[baseLength + 1, comparisonLength + 1];
            if (baseLength == 0)
            {
                return comparisonLength;
            }
            if (comparisonLength == 0)
            {
                return baseLength;
            }
            for (int i = 0; i <= baseLength; d[i, 0] = i++)
                ;
            for (int j = 0; j <= comparisonLength; d[0, j] = j++)
                ;
            for (int i = 1; i <= baseLength; i++)
            {
                for (int j = 1; j <= comparisonLength; j++)
                {
                    int cost = (comparisonString[j - 1] == baseString[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[baseLength, comparisonLength];
        }
    }
}
