using DurableFunctionsAnalyzer.Models;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DurableFunctionsAnalyzer.Analyzers
{
    interface IFunctionAnalyzer
    {
        void ReportProblems(CompilationAnalysisContext cac, IEnumerable<FunctionDefinition> availableFunctions, IEnumerable<FunctionCall> calledFunctions);
    }
}
