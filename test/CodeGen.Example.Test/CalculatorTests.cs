// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.TypedInterfaces;
using Moq;
using System.Threading.Tasks;
using WebJobs.Extensions.DurableTask.CodeGen.Example;
using Xunit;

namespace DurableTask.Example.Tests.Backup
{
    public class CalculatorTests
    {
        [Fact]
        public async Task MultiplyTest()
        {
            // Arrange
            var num1 = 5;
            var num2 = 10;
            var answer = num1 * num2;

            var mockActivityCaller = new Mock<ITypedDurableActivityCaller>();
            mockActivityCaller.Setup(c => c.Add(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((int a, int b) => a + b);
            var activityCaller = mockActivityCaller.Object;

            var mockContext = new Mock<ITypedDurableOrchestrationContext>();
            mockContext.Setup(c => c.Activities).Returns(activityCaller);
            mockContext.Setup(c => c.GetInput<(int, int)>()).Returns((5, 10));
            var context = mockContext.Object;

            var calculator = new Calculator();

            // Act
            var result = await calculator.Multiply(context);

            // Assert
            Assert.Equal(answer, result);
            mockContext.Verify(c => c.GetInput<(int, int)>(), Times.Once);
            mockContext.Verify(c => c.Activities, Times.Exactly(num2));
            mockActivityCaller.Verify(c => c.Add(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(num2));
        }
    }
}
