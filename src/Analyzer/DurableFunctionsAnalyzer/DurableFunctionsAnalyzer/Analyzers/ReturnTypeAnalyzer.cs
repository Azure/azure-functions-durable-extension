using DurableFunctionsAnalyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;


namespace DurableFunctionsAnalyzer.Analyzers
{
    class ReturnTypeAnalyzer : IFunctionAnalyzer
    {
        public const string DiagnosticId = "DurableFunctionsReturnTypeAnalyzer";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ReturnTypeAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ReturnTypeAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ReturnTypeAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);


        public void ReportProblems(CompilationAnalysisContext cac, IEnumerable<FunctionDefinition> availableFunctions, IEnumerable<FunctionCall> calledFunctions)
        {
            foreach (var node in calledFunctions)
            {
                var functionDefinition = availableFunctions.Where(x => x.Name == node.Name).SingleOrDefault();
                if (functionDefinition != null)
                {

                    if (functionDefinition.ReturnType != node.ExpectedReturnType &&
                        !(functionDefinition.ReturnType == "System.Void" && node.ExpectedReturnType == "System.Threading.Tasks.Task"))//handle when the return type is void
                    {
                        if ($"System.Threading.Tasks.Task<{functionDefinition.ReturnType}>" != node.ExpectedReturnType)//Handle cases where the activity isn't async
                            cac.ReportDiagnostic(Diagnostic.Create(Rule, node.ExpectedReturnTypeNode.GetLocation(), node.Name, functionDefinition.ReturnType, node.ExpectedReturnType));
                    }
                }
            }
        }



    }
}
