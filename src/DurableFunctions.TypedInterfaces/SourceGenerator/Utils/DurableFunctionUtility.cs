// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Utils
{
    public class DurableFunctionUtility
    {
        public static bool TryFindDurableOrchestrationContextType(SemanticModel model, out INamedTypeSymbol namedTypeSymbol)
        {
            namedTypeSymbol = model.Compilation.GetTypeByMetadataName($"{Names.DurableTaskNamespace}.{Names.IDurableOrchestrationContext}");
            return namedTypeSymbol != null;
        }

        public static bool TryFindDurableClientType(SemanticModel model, out INamedTypeSymbol namedTypeSymbol)
        {
            namedTypeSymbol = model.Compilation.GetTypeByMetadataName($"{Names.DurableTaskNamespace}.{Names.IDurableClient}");
            return namedTypeSymbol != null;
        }
    }
}
