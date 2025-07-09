#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($sleepMs)

Write-Information "Sleeping for ${$sleepMs}ms."
Start-Sleep -Milliseconds $sleepMs
# This is the actual result of the activity
"Slept for ${$sleepMs}ms."
