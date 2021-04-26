// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Models
{
    public class CompiledProject
    {
        #region Properties

        public MSBuildWorkspace Workspace { get; }
        public Project Project { get; }
        public Compilation Compilation { get; }

        #endregion

        #region Constructors

        public CompiledProject(string projectFilePath)
        {
            if (!File.Exists(projectFilePath))
                throw new ArgumentException($"No project file exists at '{projectFilePath}'.");

            // https://stackoverflow.com/a/64766792/7176908
            MSBuildLocator.RegisterDefaults();

            Workspace = MSBuildWorkspace.Create();
            Workspace.LoadMetadataForReferencedProjects = true;
            Project = Workspace.OpenProjectAsync(projectFilePath).Result;


            Compilation = Project.GetCompilationAsync().Result;
        }

        #endregion
    }
}
