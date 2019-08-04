using System;
using System.Collections.Generic;
using System.Text;

namespace DurableFunctionsAnalyzer.Models
{
    class FunctionDefinition
    {
        public string Name { get; set; }
        public string ActivityTriggerType { get; set; }
        public string ReturnType { get; set; }
    }
}
