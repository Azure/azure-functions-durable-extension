// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Models;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Models;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Utils;
using Xunit;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests
{
    public class SyntaxNodeUtilityTests
    {
        private CalculatorDocument Calculator => CalculatorDocument.Instance;

        [Fact]
        public void GetExistingFunctionName()
        {
            // Arrange
            var expectedFunctionName = "Add";
            var model = Calculator.Semantic;
            var method = Calculator.AddMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetFunctionName(model, method, out string functionName);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedFunctionName, functionName);
        }

        [Fact]
        public void GetMissingFunctionName()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.SubtractMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetFunctionName(model, method, out string functionName);

            // Assert
            Assert.False(result);
            Assert.Null(functionName);
        }


        [Fact]
        public void GetActivityFunctionKind()
        {
            // Arrange
            var expectedFunctionKind = DurableFunctionKind.Activity;
            var method = Calculator.SubtractMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetFunctionKind(method, out DurableFunctionKind kind);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedFunctionKind, kind);
        }

        [Fact]
        public void GetOrchestrationFunctionKind()
        {
            // Arrange
            var expectedFunctionKind = DurableFunctionKind.Orchestration;
            var method = Calculator.MultiplyMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetFunctionKind(method, out DurableFunctionKind kind);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedFunctionKind, kind);
        }

        [Fact]
        public void GetMissingFunctionKind()
        {
            // Arrange
            var expectedFunctionKind = DurableFunctionKind.Unknonwn;
            var method = Calculator.DivideMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetFunctionKind(method, out DurableFunctionKind kind);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedFunctionKind, kind);
        }

        [Fact]
        public void GetReturnType()
        {
            // Arrange
            var method = Calculator.MultiplyMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetReturnType(method, out TypeSyntax returnType);

            // Assert
            Assert.True(result);
            Assert.NotNull(returnType);
        }

        [Fact]
        public void GetParametersFromAssignment()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.AddMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> parameters);

            // Assert
            Assert.True(result);
            Assert.Equal(2, parameters.Count);
            Assert.Equal("int", parameters[0].Type.ToString());
            Assert.Equal("int", parameters[1].Type.ToString());
            Assert.Equal("num1", parameters[0].Name.ToString());
            Assert.Equal("num2", parameters[1].Name.ToString());
        }

        [Fact]
        public void GetGenericParametersFromAssignment()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.AddListsMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> parameters);

            // Assert
            Assert.True(result);
            Assert.Equal(2, parameters.Count);
            Assert.Equal("List<int>", parameters[0].Type.ToString());
            Assert.Equal("List<int>", parameters[1].Type.ToString());
            Assert.Equal("nums1", parameters[0].Name.ToString());
            Assert.Equal("nums2", parameters[1].Name.ToString());
        }

        [Fact]
        public void GetParameterFromLocalDeclaration()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.IdentityMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> parameters);

            // Assert
            Assert.True(result);
            Assert.Single(parameters);
            Assert.Equal("int", parameters[0].Type.ToString());
            Assert.Equal("num1", parameters[0].Name.ToString());
        }

        [Fact]
        public void GetGenericParameterFromLocalDeclaration()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.AddListMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> parameters);

            // Assert
            Assert.True(result);
            Assert.Single(parameters);
            Assert.Equal("List<int>", parameters[0].Type.ToString());
            Assert.Equal("nums", parameters[0].Name.ToString());
        }


        [Fact]
        public void GetQualifiedName()
        {
            // Arrange
            var expectedQualifiedName = "DurableTask.Example.Calculator.Add";
            var model = Calculator.Semantic;
            var method = Calculator.AddMethod;

            // Act
            var result = SyntaxNodeUtility.TryGetQualifiedTypeName(model, method, out string qualifiedName);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedQualifiedName, qualifiedName);
        }

        [Fact]
        public void GetRequiredNamespaces()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.AddComplexMethod;

            var parameterTypes = SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> typedParameters);
            var usedTypes = typedParameters.Select(p => p.Type).ToList();

            // Act
            var result = SyntaxNodeUtility.TryGetRequiredNamespaces(model, usedTypes, out HashSet<string> requiredNamespaces);

            // Assert
            Assert.True(result);
            Assert.Single(requiredNamespaces);
            Assert.Equal("DurableTask.Example.Math", requiredNamespaces.First());
        }

        [Fact]
        public void GetRequiredNamespacesWithGenerics()
        {
            // Arrange
            var model = Calculator.Semantic;
            var method = Calculator.AddListComplexMethod;

            SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> typedParameters);
            var usedTypes = typedParameters.Select(p => p.Type).ToList();

            // Act
            var result = SyntaxNodeUtility.TryGetRequiredNamespaces(model, usedTypes, out HashSet<string> requiredNamespaces);

            // Assert
            Assert.True(result);
            Assert.Equal(2, requiredNamespaces.Count);
            var requiredNamespaceList = requiredNamespaces.ToList();
            Assert.Equal("System.Collections.Generic", requiredNamespaceList[0]);
            Assert.Equal("DurableTask.Example.Math", requiredNamespaceList[1]);
        }
    }
}
