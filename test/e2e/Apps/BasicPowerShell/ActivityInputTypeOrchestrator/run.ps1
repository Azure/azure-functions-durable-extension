#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($Context)

# Initialize output array
$output = @()

# Test byte array input
$byteArrayInput = [byte[]]@(1,2,3,4,5)
$output += Invoke-DurableActivity -FunctionName 'InputCheckByteArray' -Input $byteArrayInput

# Test empty byte array input
$emptyByteArray = @()
$output += Invoke-DurableActivity -FunctionName 'InputCheckByteArray' -Input $emptyByteArray

# Test single byte input
$singleByteInput = 42
$output += Invoke-DurableActivity -FunctionName 'InputCheckSingleByte' -Input $singleByteInput

# Test custom class input
$customClassInput = [CustomClass]::new()
$customClassInput.Name = "Test"
$customClassInput.Age = 25
$customClassInput.Data = [byte[]]@(1,2,3)
# $customClassInput.Duration = [TimeSpan]::FromHours(1)
$customClassInput.Duration = "01:00:00"
$output += Invoke-DurableActivity -FunctionName 'InputCheckCustomClass' -Input $customClassInput

# Test int array input
$intArrayInput = @(1,2,3,4,5)
$output += Invoke-DurableActivity -FunctionName 'InputCheckIntArray' -Input $intArrayInput

# Test string input
$stringInput = "Test string input"
$output += Invoke-DurableActivity -FunctionName 'InputCheckString' -Input $stringInput

# Test array of custom class input
$customClass1 = [CustomClass]::new()
$customClass1.Name = "Test1"
$customClass1.Age = 25
$customClass1.Data = [byte[]]@(1,2,3)
# $customClass1.Duration = [TimeSpan]::FromMinutes(30)
$customClass1.Duration = "00:30:00"

$customClass2 = [CustomClass]::new()
$customClass2.Name = "Test2"
$customClass2.Age = 30
$customClass2.Data = [byte[]]@()
# $customClass2.Duration = [TimeSpan]::FromMinutes(45)
$customClass2.Duration = "00:45:00"

$complexInput = @($customClass1, $customClass2)
$output += Invoke-DurableActivity -FunctionName 'InputCheckCustomClassArray' -Input $complexInput

return $output
