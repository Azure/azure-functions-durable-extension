param($Context)

$output = @()

Write-Host $Context.InstanceId

$output += Invoke-DurableActivity -FunctionName 'RaiseException' -Input ($Context.InstanceId)

$output
