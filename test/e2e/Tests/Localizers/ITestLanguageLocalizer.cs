// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

enum LanguageType
{
    DotnetIsolated,
    PowerShell,
    Python,
    Node,
    Java
}

internal interface ITestLanguageLocalizer
{
    string GetLocalizedStringValue(string key, params object[] args);

    LanguageType GetLanguageType();
}