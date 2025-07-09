#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param([CustomClass]$in)

if (!($in.Data -is [byte[]]))
{
    "Error: Expected Data to be byte[] but got $($in.Data.GetType().Name)";
}

"Received CustomClass: {Name: $($in.Name), Age: $($in.Age), Duration: $($in.Duration), Data: [$($in.Data -join ", ")]}";