$ev = Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddHours(-4)} -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match 'KeyboardHint|UCEERR|RENDERTHREAD|PresentationFramework|milcore|d3d' }
foreach ($e in $ev) {
    Write-Output "=== $($e.TimeCreated) id=$($e.Id) level=$($e.LevelDisplayName) ==="
    Write-Output ($e.Message.Substring(0, [Math]::Min(400, $e.Message.Length)))
}
Write-Output "--- done, $($ev.Count) events ---"
