#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($name)

# The duration of 5 seconds for this activity was chosen because
# it is long enough to demonstrate both the activity timeout and the 
# activity success case in the tests for activity timeout. 
Start-Sleep -Milliseconds 5000
"The activity function completed successfully"
