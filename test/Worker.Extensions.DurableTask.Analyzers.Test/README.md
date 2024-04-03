# Worker.Extensions.DurableTask.Analyzers.Test


## Using Analyzer Verifiers

This project contains utilities for testing analyzers and verifying their behavior.

- [`CSharpAnalyzerVerifier.cs`](./Verifiers/CSharpAnalyzerVerifier.cs): verifies (runs tests against) a CSharp [DiagnosticAnalyzer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticanalyzer?view=roslyn-dotnet-4.7.0)
- [`CSharpCodeFixVerifier.cs`](./Verifiers/CSharpCodeFixVerifier.cs): verifies a CSharp [DiagnosticAnalyzer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticanalyzer?view=roslyn-dotnet-4.7.0) + its related CSharp [CodeFixProvider](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.codefixes.codefixprovider?view=roslyn-dotnet-4.7.0)

Those are all utilities that extend classes of `Microsoft.CodeAnalysis.Testing` and `Microsoft.CodeAnalysis.CSharp.Testing` NuGet packages, providing an easier interface for tests.

For instance, here's how you can use them:

```cs
using SomeNamespace.Analyzer;
using VerifyCS = Worker.Extensions.DurableTask.Analyzers.Tests.CSharpAnalyzerVerifier<SomeNamespace.Analyzer.SomeAnalyzer>;

namespace SomeNamespace.Tests;

public class UnitTest1
{
    [Fact]
    public async Task DateTimeNowHasDiagnostic()
    {
        var test = @"
using System;

class Someclass
{   
    static void Write()
    {
        var now1 = {|#0:DateTime.Now|};
        Console.WriteLine(now1);
    }
}";

        var expected = VerifyCS
            .Diagnostic(SomeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("System.DateTime.Now");

        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}
```
