// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using DurableFunctions.TypedInterfaces.SourceGenerator.Tests.Models;
using Xunit;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Tests
{
    public class DurableFunctionTests
    {
        private static CalculatorDocument Calculator => CalculatorDocument.Instance;

        [Fact]
        public void TryParseActivity()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.AddMethod;

            // Act
            var result = DurableFunction.TryParse(model, method, out DurableFunction function);

            // Assert
            Assert.True(result);
            Assert.Equal("WebJobs.Extensions.DurableTask.CodeGen.Example.Calculator.Add", function.FullTypeName);
            Assert.Equal(2, function.RequiredNamespaces.Count);
            var requiredNamespaceList = function.RequiredNamespaces.ToList();
            Assert.Equal("System.Threading.Tasks", requiredNamespaceList[0]);
            Assert.Equal("Microsoft.Azure.WebJobs.Extensions.DurableTask", requiredNamespaceList[1]);
            Assert.Equal("Add", function.Name);
            Assert.Equal(DurableFunctionKind.Activity, function.Kind);
            Assert.Equal(2, function.Parameters.Count);
            Assert.Equal("int", function.Parameters[0].Type.ToString());
            Assert.Equal("int", function.Parameters[1].Type.ToString());
            Assert.Equal("num1", function.Parameters[0].Name);
            Assert.Equal("num2", function.Parameters[1].Name);
            Assert.Equal("Task<int>", function.ReturnType);
            Assert.Equal("<int>", function.CallGenerics);
        }

        [Fact]
        public void TryParseOrchestration()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.MultiplyMethod;

            // Act
            var result = DurableFunction.TryParse(model, method, out DurableFunction function);

            // Assert
            Assert.True(result);
            Assert.Equal("WebJobs.Extensions.DurableTask.CodeGen.Example.Calculator.Multiply", function.FullTypeName);
            Assert.Equal(2, function.RequiredNamespaces.Count);
            var requiredNamespaceList = function.RequiredNamespaces.ToList();
            Assert.Equal("System.Threading.Tasks", requiredNamespaceList[0]);
            Assert.Equal("Microsoft.Azure.WebJobs.Extensions.DurableTask", requiredNamespaceList[1]);
            Assert.Equal("Multiply", function.Name);
            Assert.Equal(DurableFunctionKind.Orchestration, function.Kind);
            Assert.Equal(2, function.Parameters.Count);
            Assert.Equal("int", function.Parameters[0].Type.ToString());
            Assert.Equal("int", function.Parameters[1].Type.ToString());
            Assert.Equal("num1", function.Parameters[0].Name);
            Assert.Equal("num2", function.Parameters[1].Name);
            Assert.Equal("Task<int>", function.ReturnType);
            Assert.Equal("<int>", function.CallGenerics);
        }
    }
}
