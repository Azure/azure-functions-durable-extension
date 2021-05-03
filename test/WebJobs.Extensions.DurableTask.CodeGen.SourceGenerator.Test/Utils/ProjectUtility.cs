﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Models;

namespace WebJobs.Extensions.DurableTask.CodeGen.SourceGenerator.Test.Utils
{
    internal class ProjectUtility
    {
        private const string ProjectFilePath = @"..\..\..\..\..\src\codegen\WebJobs.Extensions.DurableTask.CodeGen.Example\WebJobs.Extensions.DurableTask.CodeGen.Example.csproj";

        private static CompiledProject instance;

        public static CompiledProject CompiledProject => instance ??= new CompiledProject(ProjectFilePath);
    }
}
