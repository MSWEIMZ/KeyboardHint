$kh = Get-Process KeyboardHint -ErrorAction SilentlyContinue
foreach ($p in $kh) { Write-Output "kh pid=$($p.Id) session=$($p.SessionId) started=$($p.StartTime)" }
Write-Output "powershell session: $([Diagnostics.Process]::GetCurrentProcess().SessionId)"
$ex = Get-Process explorer -ErrorAction SilentlyContinue | Select-Object -First 1
if ($ex) { Write-Output "explorer session: $($ex.SessionId)" }
