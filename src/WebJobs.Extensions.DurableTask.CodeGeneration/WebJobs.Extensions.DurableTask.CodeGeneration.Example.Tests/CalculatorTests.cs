// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

//using System.Threading.Tasks;
//using Microsoft.Azure.WebJobs.Extensions.DurableTask;
//using Moq;
//using Microsoft.Azure.WebJobs.Generated;
//using Xunit;
//using System;

//namespace DurableTask.Example.Tests.Backup
//{
//    public class CalculatorTests
//    {
//        [Fact]
//        //public async Task MultiplyTest()
//        //{
//        //    // Arrange
//        //    var num1 = 5;
//        //    var num2 = 10;
//        //    var answer = num1 * num2;

//        //    var mockOrchestrationContext = new Mock<IDurableOrchestrationContext>(MockBehavior.Strict);
//        //    mockOrchestrationContext.Setup(c => c.GetInput<(int, int)>()).Returns((5, 10));
//        //    var context = mockOrchestrationContext.Object;

//        //    var callerContext = new Mock<IGeneratedDurableFunctionCaller>();
//        //    callerContext.Setup(c => c.Add(context, It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((IDurableOrchestrationContext context, int a, int b) => a + b);
//        //    var caller = callerContext.Object;

//        //    var calculator = new Calculator();

//        //    // Act
//        //    var result = await calculator.Multiply(context, caller);

//        //    // Assert
//        //    Assert.Equal(answer, result);
//        //    mockOrchestrationContext.Verify(c => c.GetInput<(int, int)>(), Times.Once);
//        //    callerContext.Verify(c => c.Add(context, It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(num2));
//        //}
//    }
//}
