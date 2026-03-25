// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Xunit;
using Xunit.Extensions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class TraceParentTest
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FromString_NormalCase()
        {
            var expectedVersion = "00";
            var expectedTraceId = "0af7651916cd43dd8448eb211c80319c";
            var expectedSpanId = "00f067aa0ba902b7";
            var expectedTraceFlags = "01";
            var actual = TraceParent.FromString($"{expectedVersion}-{expectedTraceId}-{expectedSpanId}-{expectedTraceFlags}");
            Assert.Equal(expectedVersion, actual.Version);
            Assert.Equal(expectedTraceId, actual.TraceId);
            Assert.Equal(expectedSpanId, actual.SpanId);
            Assert.Equal(expectedTraceFlags, actual.TraceFlags);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FromString_Exception()
        {
            var ex = Assert.Throws<ArgumentException>(() => { TraceParent.FromString("foobar"); });
            Assert.Contains("foobar", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => { TraceParent.FromString(""); });
            Assert.Contains("Traceparent", ex.Message);
        }
    }
}
