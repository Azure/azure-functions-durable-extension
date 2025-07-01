#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($Context)

Start-DurableExternalEventListener -EventName "Approval" 

return "Orchestrator Finished!"