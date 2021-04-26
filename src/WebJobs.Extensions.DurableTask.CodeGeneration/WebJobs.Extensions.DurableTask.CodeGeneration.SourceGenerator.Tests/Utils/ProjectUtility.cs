// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Models;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Utils
{
    internal class ProjectUtility
    {
        private const string ProjectFilePath = @"..\..\..\..\WebJobs.Extensions.DurableTask.CodeGeneration.Example\WebJobs.Extensions.DurableTask.CodeGeneration.Example";

        private static CompiledProject compiledProject;

        public static CompiledProject CompiledProject => compiledProject ??= new CompiledProject(ProjectFilePath);
    }
}
