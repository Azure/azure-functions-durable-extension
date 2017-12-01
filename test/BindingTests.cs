﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class BindingTests
    {
        private readonly ITestOutputHelper output;

        private readonly ILoggerFactory loggerFactory;
        private readonly TestLoggerProvider loggerProvider;

        public BindingTests(ITestOutputHelper output)
        {
            this.output = output;
            loggerProvider = new TestLoggerProvider();
            loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);
        }

        [Fact]
        public async Task ActivityTriggerAsJObject()
        {
            using (JobHost host = TestHelpers.GetJobHost(loggerFactory, nameof(ActivityTriggerAsJObject)))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToJObject);
                startArgs.Input = new { Foo = "Bar" };

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                // The function checks to see if there is a property called "Foo" which is set to a value
                // called "Bar" and returns true if this is the case. Otherwise returns false.
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(true, status?.Output);

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task ActivityTriggerAsPOCO()
        {
            using (JobHost host = TestHelpers.GetJobHost(loggerFactory, nameof(ActivityTriggerAsPOCO)))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToPOCO);

                var input = new { Foo = "Bar" };
                startArgs.Input = input;

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                // The function echos back the 'Foo' input property value
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(input.Foo, status?.Output);

                await host.StopAsync();
            }
        }


        [Fact]
        public async Task ActivityTriggerAsNumber()
        {
            using (JobHost host = TestHelpers.GetJobHost(loggerFactory, nameof(ActivityTriggerAsNumber)))
            {
                await host.StartAsync();

                // Using StartOrchestrationArgs to start an activity function because it's easier than creating a new type.
                var startArgs = new StartOrchestrationArgs();
                startArgs.FunctionName = nameof(TestActivities.BindToDouble);
                startArgs.Input = 3.14;

                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.CallActivity), startArgs, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                // The function echos back the input value
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal((double)startArgs.Input, status?.Output);

                await host.StopAsync();
            }
        }
    }
}
