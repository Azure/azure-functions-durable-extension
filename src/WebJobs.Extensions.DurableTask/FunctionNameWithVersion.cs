// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Utility class for handling function names with optional version information.
    /// </summary>
    internal static class FunctionNameWithVersion
    {
        /// <summary>
        /// Delimiter used to separate function name from version in serialized format.
        /// </summary>
        internal const char Delimiter = '\n';

        /// <summary>
        /// Combines a function name and optional version into a single string.
        /// </summary>
        /// <param name="functionName">The name of the function.</param>
        /// <param name="version">The optional version string. If null, only the function name is returned.</param>
        /// <returns>The combined function name and version string, or just the function name if version is null.</returns>
        internal static string Combine(string functionName, string version)
        {
            return version == null ? functionName : functionName + Delimiter + version;
        }

        /// <summary>
        /// Parses a combined function name and version string into separate components.
        /// </summary>
        /// <param name="functionNameAndVersion">The combined function name and version string.</param>
        /// <returns>A tuple containing the function name and version. Version will be null if no delimiter is found.</returns>
        internal static (string functionName, string version) Parse(string functionNameAndVersion)
        {
            int delimiterIndex = functionNameAndVersion.IndexOf(Delimiter);
            if (delimiterIndex < 0)
            {
                // No version specified
                return (functionNameAndVersion, null);
            }

            // Function name and version are separated by delimiter
            var functionName = functionNameAndVersion.Substring(0, delimiterIndex);
            var version = functionNameAndVersion.Substring(delimiterIndex + 1);
            return (functionName, version);
        }
    }
}
