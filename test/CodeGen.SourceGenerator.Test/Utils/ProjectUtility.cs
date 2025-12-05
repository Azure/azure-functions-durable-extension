// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Reflection;
using DurableFunctions.TypedInterfaces.SourceGenerator.Tests.Models;

namespace WebJobs.Extensions.DurableTask.CodeGen.SourceGenerator.Test.Utils
{
    internal class ProjectUtility
    {
        private const string RelativeProjectFilePath = @"../../../../../src/DurableFunctions.TypedInterfaces/Example/DurableFunctions.TypedInterfaces.Example.csproj";

        private static CompiledProject instance;

        public static CompiledProject CompiledProject => instance ??= new CompiledProject(GetProjectFilePath());

        private static string GetProjectFilePath()
        {
            // Get the directory where the test assembly is located
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            return Path.GetFullPath(Path.Combine(assemblyDirectory!, RelativeProjectFilePath));
        }
    }
}
