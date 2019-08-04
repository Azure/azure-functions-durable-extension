using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace DurableFunctionsAnalyzer.Models
{
    class FunctionCall
    {
        public string Name { get; set; }
        public SyntaxNode NameNode { get; set; }
        public SyntaxNode ParameterNode { get; set; }
        public String ParameterType { get; set; }
        public string ExpectedReturnType { get; set; }
        public SyntaxNode ExpectedReturnTypeNode { get; set; }
    }
}
