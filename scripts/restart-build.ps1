# kill any running instance, start the fresh win-x64 build, verify
Get-Process KeyboardHint -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) "src\KeyboardHint\bin\Release\net8.0-windows10.0.19041.0\win-x64\KeyboardHint.exe"
Start-Process $exe
Start-Sleep -Seconds 4
$p = Get-Process KeyboardHint -ErrorAction SilentlyContinue
if ($p) { Write-Output "started pid=$($p.Id) path=$($p.Path)" } else { Write-Output "FAILED to start" }
