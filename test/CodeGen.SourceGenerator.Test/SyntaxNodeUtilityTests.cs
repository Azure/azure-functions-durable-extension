// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using DurableFunctions.TypedInterfaces.SourceGenerator.Tests.Models;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;
using Xunit;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Tests
{
    public class SyntaxNodeUtilityTests
    {
        private CalculatorDocument Calculator => CalculatorDocument.Instance;

        [Fact]
        public void GetExistingFunctionName()
        {
            // Arrange
            var expectedFunctionName = "Add";
            var model = this.Calculator.Semantic;
            var method = this.Calculator.AddMethod;

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
            var model = this.Calculator.Semantic;
            var method = this.Calculator.SubtractMethod;

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
            var method = this.Calculator.SubtractMethod;

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
            var method = this.Calculator.MultiplyMethod;

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
            var method = this.Calculator.DivideMethod;

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
            var method = this.Calculator.MultiplyMethod;

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
            var model = this.Calculator.Semantic;
            var method = this.Calculator.AddMethod;

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
            var model = this.Calculator.Semantic;
            var method = this.Calculator.AddListsMethod;

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
            var model = this.Calculator.Semantic;
            var method = this.Calculator.IdentityMethod;

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
            var model = this.Calculator.Semantic;
            var method = this.Calculator.AddListMethod;

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
            var expectedQualifiedName = "WebJobs.Extensions.DurableTask.CodeGen.Example.Calculator.Add";
            var model = this.Calculator.Semantic;
            var method = this.Calculator.AddMethod;

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
            var model = this.Calculator.Semantic;
            var method = this.Calculator.AddComplexMethod;

            var parameterTypes = SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> typedParameters);
            var usedTypes = typedParameters.Select(p => p.Type).ToList();

            // Act
            var result = SyntaxNodeUtility.TryGetRequiredNamespaces(model, usedTypes, out HashSet<string> requiredNamespaces);

            // Assert
            Assert.True(result);
            Assert.Single(requiredNamespaces);
            Assert.Equal("WebJobs.Extensions.DurableTask.CodeGen.Example.Models", requiredNamespaces.First());
        }

        [Fact]
        public void GetRequiredNamespacesWithGenerics()
        {
            // Arrange
            var model = this.Calculator.Semantic;
            var method = this.Calculator.AddListComplexMethod;

            SyntaxNodeUtility.TryGetParameters(model, method, out List<TypedParameter> typedParameters);
            var usedTypes = typedParameters.Select(p => p.Type).ToList();

            // Act
            var result = SyntaxNodeUtility.TryGetRequiredNamespaces(model, usedTypes, out HashSet<string> requiredNamespaces);

            // Assert
            Assert.True(result);
            Assert.Equal(2, requiredNamespaces.Count);
            var requiredNamespaceList = requiredNamespaces.ToList();
            Assert.Equal("System.Collections.Generic", requiredNamespaceList[0]);
            Assert.Equal("WebJobs.Extensions.DurableTask.CodeGen.Example.Models", requiredNamespaceList[1]);
        }
    }
}
