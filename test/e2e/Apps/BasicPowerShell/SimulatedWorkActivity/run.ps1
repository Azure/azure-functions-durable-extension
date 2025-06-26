param($sleepMs)

Write-Information ("Sleeping for " + "$sleepMs" + "ms.")
Start-Sleep -Milliseconds $sleepMs
("Slept for " + "$sleepMs" + "ms.")
