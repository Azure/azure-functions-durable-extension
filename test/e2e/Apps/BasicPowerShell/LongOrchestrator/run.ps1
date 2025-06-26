param($Context)

$output = @()

for ($i = 0; $i -lt 1000; $i++) {
    $output += Invoke-DurableActivity -FunctionName 'SimulatedWorkActivity' -Input 1000
}

$output
