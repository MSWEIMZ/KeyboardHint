Get-Process KeyboardHint -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) "publish\KeyboardHint.exe"
Start-Process $exe
Start-Sleep -Seconds 8
$p = Get-Process KeyboardHint -ErrorAction SilentlyContinue
if ($p) { Write-Output "started pid=$($p.Id)" } else { Write-Output "FAILED" }
