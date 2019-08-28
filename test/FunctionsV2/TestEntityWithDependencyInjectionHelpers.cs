// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public static class TestEntityWithDependencyInjectionHelpers
    {
        public const string DummyEnvironmentVariable = "DUMMY_ENVIRONMENT_VARIABLE";
        public const string DummyEnvironmentVariableValue = "DUMMY_VALUE";

        public const string BlobContainerPath = "durable-entities-di-test-blob-environment";
        public const string BlobStoredEnvironmentVariableValue = "blobs are great";
        public const string BlobStoredEnvironmentVariableName = "blob_storage_environment_variable";

        public interface IEnvironment
        {
            Task<string> GetEnvironmentVariable(string variableName);
        }

        public interface IWritableEnvironment
        {
            Task<string> GetEnvironmentVariable(string variableName);

            Task<bool> SetEnvironmentVariable(KeyValuePair<string, string> environmentVariable);
        }

        public static async Task<string> EnvironmentOrchestration([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            var environment = ctx.GetInput<EntityId>();

            var entityProxy = ctx.CreateEntityProxy<IEnvironment>(environment);

            // get current value
            return await entityProxy.GetEnvironmentVariable(DummyEnvironmentVariable);
        }

        public static async Task<List<string>> BlobEnvironmentOrchestration([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            List<string> environmentValues = new List<string>();
            var environment = ctx.GetInput<EntityId>();

            var entityProxy = ctx.CreateEntityProxy<IWritableEnvironment>(environment);

            // get current value
            environmentValues.Add(await entityProxy.GetEnvironmentVariable(DummyEnvironmentVariable));

            await entityProxy.SetEnvironmentVariable(new KeyValuePair<string, string>(BlobStoredEnvironmentVariableName, BlobStoredEnvironmentVariableValue));
            environmentValues.Add(await entityProxy.GetEnvironmentVariable(BlobStoredEnvironmentVariableName));
            return environmentValues;
        }

        [FunctionName(nameof(Environment))]
        public static Task EnvironmentFunction([EntityTrigger] IDurableEntityContext context)
        {
            return context.DispatchAsync<Environment>();
        }

        [FunctionName(nameof(BlobBackedEnvironment))]
        public static Task BlobEnvironmentFunction([EntityTrigger] IDurableEntityContext context, [Blob(BlobContainerPath, System.IO.FileAccess.Read)] CloudBlobContainer blob)
        {
            return context.DispatchAsync<BlobBackedEnvironment>(blob);
        }

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class Environment : IEnvironment
        {
            private readonly INameResolver nameResolver;

            public Environment(INameResolver nameResolver)
            {
                this.nameResolver = nameResolver;
            }

            public Task<string> GetEnvironmentVariable(string variableName)
            {
                return Task.FromResult(this.nameResolver.Resolve(variableName));
            }

            public void Delete()
            {
                Entity.Current.DestructOnExit();
            }
        }

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class BlobBackedEnvironment : IWritableEnvironment
        {
            private readonly INameResolver nameResolver;
            private readonly CloudBlobContainer blobContainer;

            public BlobBackedEnvironment(INameResolver nameResolver, CloudBlobContainer blobContainer)
            {
                this.nameResolver = nameResolver;
                this.blobContainer = blobContainer;
            }

            public async Task<string> GetEnvironmentVariable(string variableName)
            {
                CloudBlockBlob environmentVariableBlob = this.blobContainer.GetBlockBlobReference(variableName);
                if (await environmentVariableBlob.ExistsAsync())
                {
                    var readStream = await environmentVariableBlob.OpenReadAsync();
                    using (var reader = new StreamReader(readStream))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    return this.nameResolver.Resolve(variableName);
                }
            }

            public async Task<bool> SetEnvironmentVariable(KeyValuePair<string, string> variable)
            {
                try
                {
                    CloudBlockBlob environmentVariableBlob = this.blobContainer.GetBlockBlobReference(variable.Key);
                    await environmentVariableBlob.UploadTextAsync(variable.Value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public void Delete()
            {
                Entity.Current.DestructOnExit();
            }
        }

        public class DummyEnvironmentVariableResolver : INameResolver
        {
            public string Resolve(string name)
            {
                if (string.Equals(name, DummyEnvironmentVariable))
                {
                    return DummyEnvironmentVariableValue;
                }

                return null;
            }
        }
    }
}
