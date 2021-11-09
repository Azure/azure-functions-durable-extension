// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.V2
{
    public class EntityMethodDiscoveryTests
    {
        private interface ICounter
        {
            void Add(int count);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CanFindMemberOnClassWithoutInterface()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("Method");

            var method = DurableEntityContext.FindMethodForContext<ClassWithoutInterface>(context.Object);

            Assert.NotNull(method);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void WillReceiveNullForMissingMember()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("NonExistingMethod");

            var method = DurableEntityContext.FindMethodForContext<ClassWithoutInterface>(context.Object);

            Assert.Null(method);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void WillFindMemberOnClassWithImplicitInterface()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("Add");

            var method = DurableEntityContext.FindMethodForContext<CounterImplicit>(context.Object);

            Assert.NotNull(method);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void WillFindMemberOnClassWithExplicitInterface()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("Add");

            var method = DurableEntityContext.FindMethodForContext<CounterExplicit>(context.Object);

            Assert.NotNull(method);
        }

        private class ClassWithoutInterface
        {
            public void Method()
            {
            }
        }

        private class CounterImplicit : ICounter
        {
            public void Add(int count)
            {
            }
        }

        private class CounterExplicit : ICounter
        {
            void ICounter.Add(int count)
            {
            }
        }
    }
}
