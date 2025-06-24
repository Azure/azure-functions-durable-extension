param($Context)

$retryOptions = New-DurableRetryOptions `
    -FirstRetryInterval (New-TimeSpan -Seconds 1) `
    -MaxNumberOfAttempts 3 

$output += Invoke-DurableActivity -FunctionName 'RaiseException' -Input $Context.InstanceId -RetryOptions $retryOptions

$output
