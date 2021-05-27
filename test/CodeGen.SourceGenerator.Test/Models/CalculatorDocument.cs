// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Tests.Models
{
    using System;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using WebJobs.Extensions.DurableTask.CodeGen.SourceGenerator.Test.Utils;

    public class CalculatorDocument
    {
        private const string CalculatorDocumentName = "Calculator.cs";

        private static CalculatorDocument instance;

        private CalculatorDocument(CompiledProject compiledProject)
        {
            this.Project = compiledProject.Project;
            this.Compilation = compiledProject.Compilation;

            this.Document = this.Project.Documents.FirstOrDefault(d => d.Name == CalculatorDocumentName);

            if (this.Document == null)
            {
                throw new InvalidOperationException($"Failed to find document with name '{CalculatorDocumentName}'.");
            }

            if (!this.Document.TryGetSyntaxTree(out var tree))
            {
                throw new InvalidOperationException($"Failed to get syntax tree for document with name '{CalculatorDocumentName}'.");
            }

            this.Semantic = this.Compilation.GetSemanticModel(tree);
            this.Root = tree.GetRoot();

            this.IdentityMethod = this.GetMethod("Identity");
            this.AddListsMethod = this.GetMethod("AddLists");
            this.AddListMethod = this.GetMethod("AddList");
            this.AddListComplexMethod = this.GetMethod("AddListComplex");
            this.AddComplexMethod = this.GetMethod("AddComplex");
            this.AddMethod = this.GetMethod("Add");
            this.SubtractMethod = this.GetMethod("Subtract");
            this.DivideMethod = this.GetMethod("Divide");
            this.MultiplyMethod = this.GetMethod("Multiply");
            this.LogMethod = this.GetMethod("Log");
        }

        public static CalculatorDocument Instance => instance ??= new CalculatorDocument(ProjectUtility.CompiledProject);

        public Project Project { get; }

        public Compilation Compilation { get; }

        public Document Document { get; }

        public SemanticModel Semantic { get; }

        public SyntaxNode Root { get; }

        public MethodDeclarationSyntax IdentityMethod { get; }

        public MethodDeclarationSyntax AddListsMethod { get; }

        public MethodDeclarationSyntax AddListMethod { get; }

        public MethodDeclarationSyntax AddListComplexMethod { get; }

        public MethodDeclarationSyntax AddComplexMethod { get; }

        public MethodDeclarationSyntax AddMethod { get; }

        public MethodDeclarationSyntax SubtractMethod { get; }

        public MethodDeclarationSyntax DivideMethod { get; }

        public MethodDeclarationSyntax MultiplyMethod { get; }

        public MethodDeclarationSyntax LogMethod { get; }

        private MethodDeclarationSyntax GetMethod(string methodName)
        {
            var method = this.Root.DescendantNodes()
                       .OfType<MethodDeclarationSyntax>()
                       .FirstOrDefault(m => m.Identifier.ToFullString() == methodName);

            if (method == null)
            {
                throw new ArgumentException($"Failed to find method with name '{methodName}' in document '{CalculatorDocumentName}.cs'");
            }

            return method;
        }
    }
}
