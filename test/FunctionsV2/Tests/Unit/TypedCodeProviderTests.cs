// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TypedCodeProviderTests
    {
        private const string TypedDurableOrchestrationContextFullName =
            TypedCodeProvider.Namespace + "." + TypedCodeProvider.TypedDurableOrchestrationContext;

        private const string TypedDurableClientFullName =
            TypedCodeProvider.Namespace + "." + TypedCodeProvider.TypedDurableClient;

        private const string TypedDurableOrchestrationCallerFullName =
            TypedCodeProvider.Namespace + "." + TypedCodeProvider.TypedDurableOrchestrationCaller;

        private const string TypedDurableActivityCallerFullName =
            TypedCodeProvider.Namespace + "." + TypedCodeProvider.TypedDurableActivityCaller;

        private const string TypedDurableOrchestrationStarterFullName =
            TypedCodeProvider.Namespace + "." + TypedCodeProvider.TypedDurableOrchestrationStarter;

        private static readonly string[] RequiredTypeNames =
        {
            TypedDurableOrchestrationContextFullName,
            TypedDurableClientFullName,
            TypedDurableOrchestrationCallerFullName,
            TypedDurableActivityCallerFullName,
            TypedDurableOrchestrationStarterFullName,
        };

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_NoGeneratedTypes_IsNotInitialized()
        {
            var provider = new TypedCodeProvider();

            provider.Initialize(new[] { CreateAssembly() });

            Assert.False(provider.IsInitialized);
            Assert.Null(provider.TypedDurableOrchestrationContextType);
            Assert.Null(provider.TypedDurableClientType);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_PartialGeneratedTypes_IsNotInitialized()
        {
            Assembly assembly = CreateAssembly(
                TypedDurableOrchestrationContextFullName,
                TypedDurableClientFullName,
                TypedDurableOrchestrationCallerFullName,
                TypedDurableActivityCallerFullName);
            var provider = new TypedCodeProvider();

            provider.Initialize(new[] { assembly });

            Assert.False(provider.IsInitialized);
            Assert.Same(
                assembly.GetType(TypedDurableOrchestrationContextFullName, throwOnError: false, ignoreCase: false),
                provider.TypedDurableOrchestrationContextType);
            Assert.Same(
                assembly.GetType(TypedDurableClientFullName, throwOnError: false, ignoreCase: false),
                provider.TypedDurableClientType);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_AllGeneratedTypesAcrossAssemblies_IsInitialized()
        {
            Assembly contextAssembly = CreateAssembly(
                TypedDurableOrchestrationContextFullName,
                TypedDurableOrchestrationCallerFullName);
            Assembly clientAssembly = CreateAssembly(
                TypedDurableClientFullName,
                TypedDurableOrchestrationStarterFullName);
            Assembly activityAssembly = CreateAssembly(TypedDurableActivityCallerFullName);
            var provider = new TypedCodeProvider();

            provider.Initialize(new[] { contextAssembly, clientAssembly, activityAssembly });

            Assert.True(provider.IsInitialized);
            Assert.Same(
                contextAssembly.GetType(TypedDurableOrchestrationContextFullName, throwOnError: false, ignoreCase: false),
                provider.TypedDurableOrchestrationContextType);
            Assert.Same(
                clientAssembly.GetType(TypedDurableClientFullName, throwOnError: false, ignoreCase: false),
                provider.TypedDurableClientType);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_TypeNamesDifferOnlyByCase_IsNotInitialized()
        {
            string[] typeNames = Array.ConvertAll(RequiredTypeNames, name => name.ToLowerInvariant());
            var provider = new TypedCodeProvider();

            provider.Initialize(new[] { CreateAssembly(typeNames) });

            Assert.False(provider.IsInitialized);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_DoesNotEnumerateDefinedTypes()
        {
            var assembly = new Mock<Assembly>(MockBehavior.Strict);
            assembly
                .Setup(a => a.GetType(It.IsAny<string>(), false, false))
                .Returns((string _, bool _, bool _) => null);
            assembly
                .SetupGet(a => a.DefinedTypes)
                .Throws(new InvalidOperationException("DefinedTypes must not be enumerated."));
            var provider = new TypedCodeProvider();

            provider.Initialize(new[] { assembly.Object });

            Assert.False(provider.IsInitialized);
            assembly.VerifyGet(a => a.DefinedTypes, Times.Never);
            assembly.Verify(
                a => a.GetType(It.IsAny<string>(), false, false),
                Times.Exactly(RequiredTypeNames.Length));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_AllTypesFound_DoesNotInspectRemainingAssemblies()
        {
            Assembly completeAssembly = CreateAssembly(RequiredTypeNames);
            var uninspectedAssembly = new Mock<Assembly>(MockBehavior.Strict);
            var provider = new TypedCodeProvider();

            provider.Initialize(new[] { completeAssembly, uninspectedAssembly.Object });

            Assert.True(provider.IsInitialized);
            uninspectedAssembly.VerifyNoOtherCalls();
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_AssemblyTypeLookupFails_ContinuesWithRemainingAssemblies()
        {
            var uninspectableAssembly = new Mock<Assembly>(MockBehavior.Strict);
            uninspectableAssembly
                .Setup(a => a.GetType(It.IsAny<string>(), false, false))
                .Throws(new FileLoadException("Assembly cannot be inspected."));
            Assembly completeAssembly = CreateAssembly(RequiredTypeNames);
            var provider = new TypedCodeProvider();

            provider.Initialize(new[] { uninspectableAssembly.Object, completeAssembly });

            Assert.True(provider.IsInitialized);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_UnexpectedTypeLookupFailure_IsNotSwallowed()
        {
            var assembly = new Mock<Assembly>(MockBehavior.Strict);
            assembly
                .Setup(a => a.GetType(It.IsAny<string>(), false, false))
                .Throws(new ApplicationException("Unexpected lookup failure."));
            var provider = new TypedCodeProvider();

            Assert.Throws<ApplicationException>(() => provider.Initialize(new[] { assembly.Object }));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Initialize_AllGeneratedTypes_PreservesTypedInstantiation()
        {
            var types = new Dictionary<string, Type>
            {
                [TypedDurableOrchestrationContextFullName] = typeof(TestTypedDurableOrchestrationContext),
                [TypedDurableClientFullName] = typeof(TestTypedDurableClient),
                [TypedDurableOrchestrationCallerFullName] = typeof(TestTypedDurableOrchestrationCaller),
                [TypedDurableActivityCallerFullName] = typeof(TestTypedDurableActivityCaller),
                [TypedDurableOrchestrationStarterFullName] = typeof(TestTypedDurableOrchestrationStarter),
            };
            var assembly = new Mock<Assembly>(MockBehavior.Strict);
            assembly
                .Setup(a => a.GetType(It.IsAny<string>(), false, false))
                .Returns((string name, bool _, bool _) => types.TryGetValue(name, out Type type) ? type : null);
            var provider = new TypedCodeProvider();
            IDurableOrchestrationContext context = Mock.Of<IDurableOrchestrationContext>();
            IDurableClient client = Mock.Of<IDurableClient>();

            provider.Initialize(new[] { assembly.Object });

            var typedContext = Assert.IsType<TestTypedDurableOrchestrationContext>(
                provider.InstantiateTypedDurableOrchestrationContext(context));
            var typedClient = Assert.IsType<TestTypedDurableClient>(
                provider.InstantiateTypedDurableClient(client));
            Assert.Same(context, typedContext.Context);
            Assert.Same(context, typedContext.OrchestrationCaller.Context);
            Assert.Same(context, typedContext.ActivityCaller.Context);
            Assert.Same(client, typedClient.Client);
            Assert.Same(client, typedClient.OrchestrationStarter.Client);
        }

        private static Assembly CreateAssembly(params string[] typeNames)
        {
            var assemblyName = new AssemblyName($"TypedCodeProviderTests_{Guid.NewGuid():N}");
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
                assemblyName,
                AssemblyBuilderAccess.RunAndCollect);
            ModuleBuilder module = assembly.DefineDynamicModule("Main");

            foreach (string typeName in typeNames)
            {
                module.DefineType(typeName, TypeAttributes.Public).CreateTypeInfo();
            }

            return assembly;
        }

        private sealed class TestTypedDurableOrchestrationContext
        {
            public TestTypedDurableOrchestrationContext(
                IDurableOrchestrationContext context,
                TestTypedDurableOrchestrationCaller orchestrationCaller,
                TestTypedDurableActivityCaller activityCaller)
            {
                this.Context = context;
                this.OrchestrationCaller = orchestrationCaller;
                this.ActivityCaller = activityCaller;
            }

            public IDurableOrchestrationContext Context { get; }

            public TestTypedDurableOrchestrationCaller OrchestrationCaller { get; }

            public TestTypedDurableActivityCaller ActivityCaller { get; }
        }

        private sealed class TestTypedDurableClient
        {
            public TestTypedDurableClient(
                IDurableClient client,
                TestTypedDurableOrchestrationStarter orchestrationStarter)
            {
                this.Client = client;
                this.OrchestrationStarter = orchestrationStarter;
            }

            public IDurableClient Client { get; }

            public TestTypedDurableOrchestrationStarter OrchestrationStarter { get; }
        }

        private sealed class TestTypedDurableOrchestrationCaller
        {
            public TestTypedDurableOrchestrationCaller(IDurableOrchestrationContext context)
            {
                this.Context = context;
            }

            public IDurableOrchestrationContext Context { get; }
        }

        private sealed class TestTypedDurableActivityCaller
        {
            public TestTypedDurableActivityCaller(IDurableOrchestrationContext context)
            {
                this.Context = context;
            }

            public IDurableOrchestrationContext Context { get; }
        }

        private sealed class TestTypedDurableOrchestrationStarter
        {
            public TestTypedDurableOrchestrationStarter(IDurableClient client)
            {
                this.Client = client;
            }

            public IDurableClient Client { get; }
        }
    }
}
