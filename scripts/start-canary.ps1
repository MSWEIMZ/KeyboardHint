# Start the debug build with KBH_CANARY=1 (shows a plain red WPF window).
$exe = Join-Path $PSScriptRoot "..\src\KeyboardHint\bin\Release\net8.0-windows10.0.19041.0\win-x64\KeyboardHint.exe"
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $exe
$psi.UseShellExecute = $false
$psi.EnvironmentVariables.Add("KBH_CANARY", "1")
if ($args.Count -gt 0 -and $args[0] -eq 'sw') { $psi.EnvironmentVariables.Add("KBH_SW", "1") }
$p = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 2
Write-Output "started pid=$($p.Id) exited=$($p.HasExited)"
