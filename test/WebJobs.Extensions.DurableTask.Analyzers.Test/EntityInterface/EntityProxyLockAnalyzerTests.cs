// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.EntityInterface
{
    [TestClass]
    public class EntityProxyLockAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = EntityProxyLockAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void SynchronousProxyCallToLockedEntity_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit(int amount);
            Task Credit(int amount);
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var from = new EntityId(""Account"", ""from"");
                var to = new EntityId(""Account"", ""to"");
                var fromProxy = context.CreateEntityProxy<IAccountEntity>(from);

                using (await context.LockAsync(from))
                {
                    fromProxy.Debit(10);
                    await fromProxy.Credit(10);

                    var toProxy = context.CreateEntityProxy<IAccountEntity>(to);
                    toProxy.Debit(10);
                }

                fromProxy.Debit(10);
            }
        }
    }";

            VerifyCSharpDiagnostic(
                test,
                ExpectedDiagnostic(26, 31, "Debit"));
        }

        [TestMethod]
        public void InlineAndAliasedProxyCallsWithParamsArray_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var first = new EntityId(""Account"", ""first"");
                var second = new EntityId(""Account"", ""second"");
                var locks = new[] { first, second };
                var firstProxy = context.CreateEntityProxy<IAccountEntity>(first);
                var alias = firstProxy;

                using (await context.LockAsync(locks))
                {
                    alias.Debit();
                    context.CreateEntityProxy<IAccountEntity>(second).Debit();
                    firstProxy?.Debit();
                    context.CreateEntityProxy<IAccountEntity>(second)?.Debit();
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(
                test,
                ExpectedDiagnostic(27, 27, "Debit"),
                ExpectedDiagnostic(28, 71, "Debit"),
                ExpectedDiagnostic(29, 33, "Debit"),
                ExpectedDiagnostic(30, 72, "Debit"));
        }

        [TestMethod]
        public void UsingDeclarationForLockedEntity_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var entity = new EntityId(""Account"", ""first"");
                var proxy = context.CreateEntityProxy<IAccountEntity>(entity);
                using var handle = await context.LockAsync(entity);

                proxy.Debit();
            }
        }
    }";

            VerifyCSharpDiagnostic(
                test,
                ExpectedDiagnostic(23, 23, "Debit"));
        }

        [TestMethod]
        public void StringKeyProxyCallToLockedEntity_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public class AccountEntity : IAccountEntity
        {
            public void Debit() { }
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                const string key = ""first"";
                var proxy = context.CreateEntityProxy<IAccountEntity>(key);

                using (await context.LockAsync(new EntityId(nameof(AccountEntity), key)))
                {
                    proxy.Debit();
                }

                using (await context.LockAsync(new EntityId(""OtherEntity"", key)))
                {
                    proxy.Debit();
                }

                using (await context.LockAsync(new EntityId(nameof(AccountEntity), ""second"")))
                {
                    proxy.Debit();
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(
                test,
                ExpectedDiagnostic(29, 27, "Debit"));
        }

        [TestMethod]
        public void EquivalentEntityIdExpressions_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                const string key = ""first"";
                var proxyTarget = new EntityId(""ACCOUNT"", key);
                var lockedEntity = new EntityId(""account"", key);
                var proxy = context.CreateEntityProxy<IAccountEntity>(proxyTarget);

                using (await context.LockAsync(lockedEntity))
                {
                    proxy.Debit();
                }

                EntityId targetTypedProxyId = new(""Account"", ""target"");
                EntityId targetTypedLockId = new(""account"", ""target"");
                var targetTypedProxy = context.CreateEntityProxy<IAccountEntity>(targetTypedProxyId);

                using (await context.LockAsync(targetTypedLockId))
                {
                    targetTypedProxy.Debit();
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(
                test,
                ExpectedDiagnostic(26, 27, "Debit"),
                ExpectedDiagnostic(35, 38, "Debit"));
        }

        [TestMethod]
        public void SafeProxyCalls_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
            Task Credit();
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var locked = new EntityId(""Account"", ""locked"");
                var unlocked = new EntityId(""Account"", ""unlocked"");
                var lockedProxy = context.CreateEntityProxy<IAccountEntity>(locked);
                var proxy = lockedProxy;
                proxy = context.CreateEntityProxy<IAccountEntity>(unlocked);
                var mutableId = locked;
                var mutableIdProxy = context.CreateEntityProxy<IAccountEntity>(mutableId);
                mutableId = unlocked;
                var locks = new[] { locked };
                locks = new[] { unlocked };
                var mutatedLocks = new[] { locked };
                mutatedLocks[0] = unlocked;
                var escapedLocks = new[] { locked };
                ReplaceFirst(escapedLocks, unlocked);
                var refMutableId = locked;
                var refMutableProxy = context.CreateEntityProxy<IAccountEntity>(refMutableId);
                Replace(ref refMutableId, unlocked);
                var capturedProxy = lockedProxy;
                capturedProxy = context.CreateEntityProxy<IAccountEntity>(unlocked);
                var closureMutableId = locked;
                void ChangeId()
                {
                    closureMutableId = unlocked;
                }

                var closureMutableProxy = context.CreateEntityProxy<IAccountEntity>(closureMutableId);
                ChangeId();

                lockedProxy.Debit();

                using (await context.LockAsync(locked))
                {
                    await lockedProxy.Credit();
                    proxy.Debit();
                }

                using (await context.LockAsync(mutableId))
                {
                    mutableIdProxy.Debit();
                }

                using (await context.LockAsync(locks))
                {
                    lockedProxy.Debit();
                }

                using (await context.LockAsync(mutatedLocks))
                {
                    lockedProxy.Debit();
                }

                using (await context.LockAsync(refMutableId))
                {
                    refMutableProxy.Debit();
                }

                using (await context.LockAsync(escapedLocks))
                {
                    lockedProxy.Debit();
                }

                using (await context.LockAsync(closureMutableId))
                {
                    closureMutableProxy.Debit();
                }

                var namedProxy = context.CreateEntityProxy<IAccountEntity>(
                    new EntityId(entityName: ""Account"", entityKey: ""locked""));
                using (await context.LockAsync(
                    new EntityId(entityKey: ""Account"", entityName: ""locked"")))
                {
                    namedProxy.Debit();
                }

                await InvokeCapturedProxy();

                async Task InvokeCapturedProxy()
                {
                    using (await context.LockAsync(locked))
                    {
                        capturedProxy.Debit();
                    }
                }
            }

            private static void Replace(ref EntityId target, EntityId replacement)
            {
                target = replacement;
            }

            private static void ReplaceFirst(EntityId[] targets, EntityId replacement)
            {
                targets[0] = replacement;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void PreAcquiredAndDisposedLockHandles_TracksLockLifetime()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var entity = new EntityId(""Account"", ""first"");
                var proxy = context.CreateEntityProxy<IAccountEntity>(entity);
                var handle = await context.LockAsync(entity);

                using (handle)
                {
                    proxy.Debit();
                }

                using (var disposedHandle = await context.LockAsync(entity))
                {
                    disposedHandle.Dispose();
                    proxy.Debit();
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(
                test,
                ExpectedDiagnostic(25, 27, "Debit"));
        }

        [TestMethod]
        public void StringKeyProxyWithAmbiguousImplementation_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public class FirstAccountEntity : IAccountEntity
        {
            public void Debit() { }
        }

        public class SecondAccountEntity : IAccountEntity
        {
            public void Debit() { }
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var proxy = context.CreateEntityProxy<IAccountEntity>(""first"");

                using (await context.LockAsync(new EntityId(nameof(FirstAccountEntity), ""first"")))
                {
                    proxy.Debit();
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DynamicEntityKeys_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public class AccountEntity : IAccountEntity
        {
            public void Debit() { }
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var entityIdProxy = context.CreateEntityProxy<IAccountEntity>(
                    new EntityId(nameof(AccountEntity), GetKey()));
                var stringKeyProxy = context.CreateEntityProxy<IAccountEntity>(GetKey());

                using (await context.LockAsync(
                    new EntityId(nameof(AccountEntity), GetKey())))
                {
                    entityIdProxy.Debit();
                    stringKeyProxy.Debit();
                }
            }

            private static string GetKey()
            {
                return ""key"";
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SelfReferentialEntityId_NoAnalyzerFailure()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public interface IAccountEntity
        {
            void Debit();
        }

        public static class Transfers
        {
            [FunctionName(""Transfer"")]
            public static async Task Run(
                [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                EntityId entity = entity;
                var proxy = context.CreateEntityProxy<IAccountEntity>(entity);

                using (await context.LockAsync(new EntityId(""Other"", ""key"")))
                {
                    proxy.Debit();
                }
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        private static DiagnosticResult ExpectedDiagnostic(int line, int column, string methodName)
        {
            return new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityProxyLockAnalyzerMessageFormat, methodName),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", line, column)
                        }
            };
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EntityProxyLockAnalyzer();
        }
    }
}
