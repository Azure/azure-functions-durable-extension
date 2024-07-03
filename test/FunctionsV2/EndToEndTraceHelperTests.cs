// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class EndToEndTraceHelperTests
    {
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void StringSanitizerTest(bool shouldTraceRawData, bool testNullInput)
        {
            // set up trace helper
            var nullLogger = new NullLogger<EndToEndTraceHelper>();
            var traceHelper = new EndToEndTraceHelper(
                logger: nullLogger,
                traceReplayEvents: false, // has not effect on sanitizer
                shouldTraceRawData: shouldTraceRawData);

            // prepare sensitive data to sanitize
            string possibleSensitiveData = "DO NOT LOG ME";

            // run sanitizer
            traceHelper.SanitizeString(
                rawPayload: testNullInput ? null : possibleSensitiveData,
                out string iLoggerString,
                out string kustoTableString);

            // expected: sanitized string should not contain the sensitive data
            Assert.DoesNotContain(possibleSensitiveData, kustoTableString);

            if (shouldTraceRawData)
            {
                if (testNullInput)
                {
                    // If provided input is null, it is logged as "(null)"
                    Assert.Equal("(null)", iLoggerString);
                }
                else
                {
                    // Otherwise, we expect to see the data as-is
                    Assert.Equal(possibleSensitiveData, iLoggerString);
                }
            }
            else
            {
                // If raw data is not being traced,
                // kusto and the ilogger should get the same data
                Assert.Equal(iLoggerString, kustoTableString);
            }
        }


        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void ExceptionSanitizerTest(bool shouldTraceRawData, bool testNullInput)
        {
            // set up trace helper
            var nullLogger = new NullLogger<EndToEndTraceHelper>();
            var traceHelper = new EndToEndTraceHelper(
                logger: nullLogger,
                traceReplayEvents: false, // has not effect on sanitizer
                shouldTraceRawData: shouldTraceRawData);

            // exception to sanitize
            var possiblySensitiveData = "DO NOT LOG ME";
            var exception = new Exception(possiblySensitiveData);

            // run sanitizer
            traceHelper.SanitizeException(
                exception: testNullInput ? null : exception,
                out string iLoggerString,
                out string kustoTableString);

            // exception message should not be part of the sanitized strings
            Assert.DoesNotContain(possiblySensitiveData, kustoTableString);

            if (shouldTraceRawData)
            {
                var expectedString = testNullInput ? string.Empty : exception.ToString();
                Assert.Equal(expectedString, iLoggerString);
            }
            else
            {
                Assert.Equal(iLoggerString, kustoTableString);
            }
        }
    }
}
