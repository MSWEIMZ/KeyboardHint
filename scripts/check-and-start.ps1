# check process / start new build (ASCII-only: PS5.1 reads files as ANSI without BOM)
$procs = Get-Process KeyboardHint -ErrorAction SilentlyContinue
if ($procs) {
    Write-Output "RUNNING:"
    $procs | Select-Object Id, Path | Format-Table -AutoSize | Out-String | Write-Output
} else {
    Write-Output "not running, starting new build..."
    $exe = Join-Path (Split-Path $PSScriptRoot -Parent) "src\KeyboardHint\bin\Release\net8.0-windows10.0.19041.0\win-x64\KeyboardHint.exe"
    Write-Output "exe: $exe"
    Start-Process $exe
    Start-Sleep -Seconds 4
    $p = Get-Process KeyboardHint -ErrorAction SilentlyContinue
    if ($p) { Write-Output "started pid=$($p.Id)" } else { Write-Output "FAILED to start" }
}
