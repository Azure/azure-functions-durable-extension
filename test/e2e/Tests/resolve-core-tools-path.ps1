#!/usr/bin/env pwsh
#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#
# Resolves the Core Tools version and directory path used by the E2E tests.
# When dot-sourced, sets two variables in the caller's scope: $CORE_TOOLS_VERSION and $FUNC_CLI_DIRECTORY.
# The version is read from the CORE_TOOLS_VERSION environment variable when set,
# otherwise it falls back to the default defined here.

$CORE_TOOLS_VERSION = if ($env:CORE_TOOLS_VERSION) {
  Write-Host "Using CORE_TOOLS_VERSION from environment: $env:CORE_TOOLS_VERSION"
  $env:CORE_TOOLS_VERSION
} else {
  '4.8.0'
}

$ProjectTemporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) 'DurableTaskExtensionE2ETests'
$FUNC_CLI_DIRECTORY = Join-Path $ProjectTemporaryPath 'Azure.Functions.Cli'
