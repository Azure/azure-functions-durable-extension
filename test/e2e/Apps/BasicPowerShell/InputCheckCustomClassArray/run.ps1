param([CustomClass[]]$in)

foreach ($item in $in) {
    if ($null -eq $item.Data -or $item.Data.GetType().Name -ne 'Byte[]') {
        "Error: Expected Data to be byte[] but got $($item.Data.GetType().Name)"
    }
}

$items = $in | ForEach-Object {
    "{Name: $($_.Name), Age: $($_.Age), Duration: $($_.Duration), Data: [$((($_.Data) -join ', '))]}"
}
"Received CustomClass[]: [$($items -join ', ')]"
