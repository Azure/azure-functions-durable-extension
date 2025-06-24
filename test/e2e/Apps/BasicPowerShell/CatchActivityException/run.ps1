param($Context)

Write-Host $Context.InstanceId

try {
    $output = Invoke-DurableActivity -FunctionName 'RaiseException' -Input $Context.InstanceId
    Write-Host "Activity completed successfully with output: $output"
    $output
}
catch {
    Write-Host "Activity failed with exception: $($_.Exception.Message)"
    $_.Exception.Message
}