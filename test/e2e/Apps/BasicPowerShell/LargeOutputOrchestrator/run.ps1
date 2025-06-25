param($Context)

$sizeInKB = [int]$Context.Input

Write-Information "Saying hello."
$outputs = @()

$outputs += Invoke-DurableActivity -FunctionName 'SayHello' -Input "Tokyo"
function GenerateLargeString($sizeInKB) {
    $length = $sizeInKB * 1024
    return "A" * $length
}

$outputs += GenerateLargeString $sizeInKB

return $outputs