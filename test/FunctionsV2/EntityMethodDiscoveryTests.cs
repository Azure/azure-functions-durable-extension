using Microsoft.Azure.WebJobs;
using Moq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class EntityMethodDiscoveryTests
    {
        private interface ICounter
        {
            void Add(int count);
        }

        [Fact]
        public void CanFindMemberOnClassWithoutInterface()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("Method");

            var method = TypedInvocationExtensions.FindMethodForContext<ClassWithoutInterface>(context.Object);

            Assert.NotNull(method);
        }

        [Fact]
        public void WillReceiveNullForMissingMember()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("NonExistingMethod");

            var method = TypedInvocationExtensions.FindMethodForContext<ClassWithoutInterface>(context.Object);

            Assert.Null(method);
        }

        [Fact]
        public void WillFindMemberOnClassWithImplicitInterface()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("Add");

            var method = TypedInvocationExtensions.FindMethodForContext<CounterImplicit>(context.Object);

            Assert.NotNull(method);
        }

        [Fact]
        public void WillFindMemberOnClassWithExplicitInterface()
        {
            var context = new Mock<IDurableEntityContext>();
            context.Setup(ctx => ctx.OperationName).Returns("Add");

            var method = TypedInvocationExtensions.FindMethodForContext<CounterExplicit>(context.Object);

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
