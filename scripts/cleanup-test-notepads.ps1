Get-Process notepad -ErrorAction SilentlyContinue |
  Where-Object { $_.StartTime -gt (Get-Date).AddMinutes(-30) } |
  ForEach-Object { Stop-Process -Id $_.Id -Force; Write-Output "killed notepad $($_.Id)" }
