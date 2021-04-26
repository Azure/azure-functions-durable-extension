// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Utils;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Tests.Models
{
    public class CalculatorDocument
    {
        private const string CalculatorDocumentName = "Calculator.cs";

        private static CalculatorDocument instance;
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

        private CalculatorDocument(CompiledProject compiledProject)
        {
            Project = compiledProject.Project;
            Compilation = compiledProject.Compilation;

            Document = Project.Documents.FirstOrDefault(d => d.Name == CalculatorDocumentName);

            if (Document == null)
                throw new InvalidOperationException($"Failed to find document with name '{CalculatorDocumentName}'.");

            if (!Document.TryGetSyntaxTree(out var tree))
                throw new InvalidOperationException($"Failed to get syntax tree for document with name '{CalculatorDocumentName}'.");

            Semantic = Compilation.GetSemanticModel(tree);
            Root = tree.GetRoot();

            IdentityMethod = GetMethod("Identity");
            AddListsMethod = GetMethod("AddLists");
            AddListMethod = GetMethod("AddList");
            AddListComplexMethod = GetMethod("AddListComplex");
            AddComplexMethod = GetMethod("AddComplex");
            AddMethod = GetMethod("Add");
            SubtractMethod = GetMethod("Subtract");
            DivideMethod = GetMethod("Divide");
            MultiplyMethod = GetMethod("Multiply");
            LogMethod = GetMethod("Log");
        }

        private MethodDeclarationSyntax GetMethod(string methodName)
        {
            var method = Root.DescendantNodes()
                       .OfType<MethodDeclarationSyntax>()
                       .FirstOrDefault(m => m.Identifier.ToFullString() == methodName);

            if (method == null)
                throw new ArgumentException($"Failed to find method with name '{methodName}' in document '{CalculatorDocumentName}.cs'");

            return method;
        }
    }
}
