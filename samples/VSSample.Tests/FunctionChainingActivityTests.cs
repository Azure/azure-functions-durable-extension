// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample.Tests
{
    using Xunit;

    public class FunctionChainingActivityTests
    {
        [Fact]
        public void SayHello_returns_greeting()
        {
            var result = Orchestrator_Function_Chaining.SayHello("John");
            Assert.Equal("Hello John!", result);
        }
    }
}
