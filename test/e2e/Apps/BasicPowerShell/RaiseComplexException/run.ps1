param($InstanceId)

Import-Module -Name "MyHelperModule"

if (GetExecutionCount($InstanceId) -gt 0) {
    IncrementExecutionCount($InstanceId)
    "Success"
}
else {
    IncrementExecutionCount($InstanceId)
    throw [System.InvalidOperationException]::new("This activity failed\r\nMore information about the failure", [System.OverflowException]::new("Inner exception message"))
}
