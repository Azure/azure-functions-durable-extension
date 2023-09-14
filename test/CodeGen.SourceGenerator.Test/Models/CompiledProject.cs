// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Tests.Models
{
    public class CompiledProject
    {
        public CompiledProject(string projectFilePath)
        {
            if (!File.Exists(projectFilePath))
            {
                throw new ArgumentException($"No project file exists at '{Path.GetFullPath(projectFilePath)}'.");
            }

            // https://stackoverflow.com/a/64766792/7176908
            MSBuildLocator.RegisterDefaults();

            this.Workspace = MSBuildWorkspace.Create();
            this.Workspace.LoadMetadataForReferencedProjects = true;
            this.Project = this.Workspace.OpenProjectAsync(projectFilePath).Result;

            this.Compilation = this.Project.GetCompilationAsync().Result;
        }

        public MSBuildWorkspace Workspace { get; }

        public Project Project { get; }

        public Compilation Compilation { get; }
    }
}
