Get-Process KeyboardHint -ErrorAction SilentlyContinue | ForEach-Object {
  [PSCustomObject]@{
    Id = $_.Id
    WS_MB = [math]::Round($_.WorkingSet64 / 1MB, 1)
    Private_MB = [math]::Round($_.PrivateMemorySize64 / 1MB, 1)
  }
} | Format-Table -AutoSize
