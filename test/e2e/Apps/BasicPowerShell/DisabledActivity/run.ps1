#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Disabled at runtime via the AzureWebJobs.DisabledActivity.Disabled app setting in
# local.settings.json. Used by DisabledOrchestrationTests to validate that scheduling a
# disabled-but-still-deployed activity fails the orchestration gracefully instead of poison-looping.
param($name)

$name
