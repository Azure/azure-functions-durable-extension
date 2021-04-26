// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Models;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Models;
using Xunit;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests
{
    public class DurableTaskCodeGeneratorTests
    {
        private CalculatorDocument Calculator => CalculatorDocument.Instance;

        [Fact]
        public void GenerateCallerClass()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.AddMethod;

            DurableFunction.TryParse(model, method, out DurableFunction function);

            var functions = new List<DurableFunction>() { function };

            var expected = Resource.GenerateCallerClass;

            // Act
            //var result = DurableTaskCodeGenerator.GenerateCallerClassSourceText(functions, new CSharpParseOptions());

            //var result = DurableTaskCodeGenerator.GenerateDurableOrchestrationCallerSourceText(functions, new CSharpParseOptions());

            //// Assert
            //Assert.Equal(expected.Replace("\r\n", "\n"), result.ToString().Replace("\r\n", "\n"));
        }

        //[Fact]
        //public void GenerateCallerInterface()
        //{
        //    // Arrange
        //    var model = Calculator.Semantic;
        //    var method = Calculator.AddMethod;

        //    DurableFunction.TryParse(model, method, out DurableFunction function);

        //    var functions = new List<DurableFunction>() { function };

        //    var expected = Resource.GenerateCallerInterface;

        //    // Act
        //    var result = DurableTaskCodeGenerator.GenerateCallerInterfaceSourceText(functions, new CSharpParseOptions());

        //    // Assert
        //    Assert.Equal(expected.Replace("\r\n", "\n"), result.ToString().Replace("\r\n", "\n"));
        //}

        //[Fact]
        //public void Generate()
        //{
        //    // Arrange
        //    var model = Calculator.Semantic;
        //    var method = Calculator.AddMethod;

        //    var orchestrationContext = Calculator.Compilation.GetTypeByMetadataName("Microsoft.Azure.WebJobs.Extensions.DurableTask.IDurableOrchestrationContext");

        //    var methods = orchestrationContext.GetMembers().OfType<IMethodSymbol>().ToList();

        //    DurableFunction.TryParse(model, method, out DurableFunction function);

        //    var functions = new List<DurableFunction>() { function };

        //    var expected = Resource.GenerateCallerInterface;

        //    // Act
        //    var result = new CompleteGenerator().Generate(model, new CSharpParseOptions()); DurableTaskCodeGenerator.GenerateWrapperImplementation(new CSharpParseOptions());

        //    // Assert
        //    Assert.Equal(expected.Replace("\r\n", "\n"), result.ToString().Replace("\r\n", "\n"));
        //}
    }
}
