# Start the win-x64 Release build from its own directory.
$exe = Join-Path $PSScriptRoot "..\src\KeyboardHint\bin\Release\net8.0-windows10.0.19041.0\win-x64\KeyboardHint.exe"
Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
Start-Sleep -Seconds 2
$p = Get-Process KeyboardHint -ErrorAction SilentlyContinue
Write-Output "started: $($p -ne $null) pid=$($p.Id -join ',')"
