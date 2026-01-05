// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests for ErrorSerializerSettingsFactory.
    /// </summary>
    public class ErrorSerializerSettingsFactoryTests
    {
        /// <summary>
        /// Verifies that the error serializer settings have MaxDepth configured
        /// to prevent StackOverflowException when serializing complex exceptions.
        /// </summary>
        [Fact]
        public void CreateJsonSerializerSettings_HasMaxDepthConfigured()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();

            // Act
            var settings = factory.CreateJsonSerializerSettings();

            // Assert
            Assert.NotNull(settings.MaxDepth);
            Assert.True(settings.MaxDepth > 0, "MaxDepth should be a positive value");
        }

        /// <summary>
        /// Verifies that simple exceptions can still be serialized successfully.
        /// </summary>
        [Fact]
        public void Serialize_SimpleException_Succeeds()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();
            var settings = factory.CreateJsonSerializerSettings();
            var simpleException = new InvalidOperationException("Test exception message");

            // Act
            var serialized = JsonConvert.SerializeObject(simpleException, settings);

            // Assert
            Assert.NotNull(serialized);
            Assert.Contains("Test exception message", serialized);
        }

        /// <summary>
        /// Verifies that exceptions with inner exceptions at a reasonable depth can be serialized.
        /// </summary>
        [Fact]
        public void Serialize_ExceptionWithInnerExceptions_Succeeds()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();
            var settings = factory.CreateJsonSerializerSettings();

            // Create an exception with a few levels of inner exceptions
            var innerMost = new ArgumentException("Inner most exception");
            var middle = new InvalidOperationException("Middle exception", innerMost);
            var outer = new Exception("Outer exception", middle);

            // Act
            var serialized = JsonConvert.SerializeObject(outer, settings);

            // Assert
            Assert.NotNull(serialized);
            Assert.Contains("Outer exception", serialized);
            Assert.Contains("Middle exception", serialized);
            Assert.Contains("Inner most exception", serialized);
        }

        /// <summary>
        /// Verifies that the TargetSite property is excluded from serialization.
        /// </summary>
        [Fact]
        public void Serialize_Exception_ExcludesTargetSite()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();
            var settings = factory.CreateJsonSerializerSettings();
            var exceptionWithTargetSite = CreateThrownException("Test exception");

            // Verify that the exception has a TargetSite before serialization
            Assert.NotNull(exceptionWithTargetSite.TargetSite);

            // Act
            var serialized = JsonConvert.SerializeObject(exceptionWithTargetSite, settings);

            // Assert - TargetSite should not appear as a property key in the JSON
            Assert.NotNull(serialized);

            // The serialized JSON shouldn't have "TargetSite" followed by a colon (as a property)
            Assert.DoesNotContain("\"TargetSite\":", serialized);
        }

        /// <summary>
        /// Verifies that ReferenceLoopHandling is set to Ignore.
        /// </summary>
        [Fact]
        public void CreateJsonSerializerSettings_HasReferenceLoopHandlingIgnore()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();

            // Act
            var settings = factory.CreateJsonSerializerSettings();

            // Assert
            Assert.Equal(ReferenceLoopHandling.Ignore, settings.ReferenceLoopHandling);
        }

        /// <summary>
        /// Verifies that TypeNameHandling is set to Objects for proper exception type deserialization.
        /// </summary>
        [Fact]
        public void CreateJsonSerializerSettings_HasTypeNameHandlingObjects()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();

            // Act
            var settings = factory.CreateJsonSerializerSettings();

            // Assert
            Assert.Equal(TypeNameHandling.Objects, settings.TypeNameHandling);
        }

        /// <summary>
        /// Verifies that core exception properties are serialized.
        /// </summary>
        [Fact]
        public void Serialize_Exception_IncludesAllowedProperties()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();
            var settings = factory.CreateJsonSerializerSettings();
            var exceptionWithStackTrace = CreateThrownException("Test message");

            // Act
            var serialized = JsonConvert.SerializeObject(exceptionWithStackTrace, settings);

            // Assert - core properties should be present
            Assert.Contains("Message", serialized);
            Assert.Contains("Test message", serialized);
        }

        /// <summary>
        /// Verifies that exceptions with custom properties that could cause
        /// serialization issues are safely serialized.
        /// </summary>
        [Fact]
        public void Serialize_ExceptionWithDangerousProperty_SucceedsWithoutIncludingDangerousProperty()
        {
            // Arrange
            var factory = new ErrorSerializerSettingsFactory();
            var settings = factory.CreateJsonSerializerSettings();

            // Create an exception with a property that could cause serialization issues
            var dangerousException = new ExceptionWithProblematicProperty("Test exception");

            // Act - this should succeed without StackOverflowException
            var serialized = JsonConvert.SerializeObject(dangerousException, settings);

            // Assert
            Assert.NotNull(serialized);
            Assert.Contains("Test exception", serialized);

            // The dangerous property should NOT be serialized as a JSON property (look for the colon pattern)
            Assert.DoesNotContain("\"SelfReference\":", serialized);
            Assert.DoesNotContain("\"ProblematicProperty\":", serialized);
        }

        /// <summary>
        /// Creates an exception that has been thrown and caught, ensuring it has
        /// populated TargetSite and StackTrace properties.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <returns>An exception with populated stack trace information.</returns>
        private static Exception CreateThrownException(string message)
        {
            try
            {
                throw new InvalidOperationException(message);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// A test exception with a property that would cause serialization issues.
        /// </summary>
        private class ExceptionWithProblematicProperty : Exception
        {
            public ExceptionWithProblematicProperty(string message)
                : base(message)
            {
                // Self-referential property that would cause infinite recursion
                this.SelfReference = this;
            }

            public Exception SelfReference { get; set; }

            // A property with a complex object graph
            public object ProblematicProperty => new { Nested = new { More = new { AndMore = this } } };
        }
    }
}
