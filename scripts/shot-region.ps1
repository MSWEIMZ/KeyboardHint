# Screenshot a screen region: shot-region.ps1 <x> <y> <w> <h> <outfile>
param([int]$x=30,[int]$y=30,[int]$w=500,[int]$h=280,[string]$out="canary2.png")
Add-Type -AssemblyName System.Drawing
$b = New-Object Drawing.Bitmap $w, $h
$g = [Drawing.Graphics]::FromImage($b)
$g.CopyFromScreen($x, $y, 0, 0, $b.Size)
$b.Save("$env:LOCALAPPDATA\KeyboardHint\$out")
$g.Dispose(); $b.Dispose()
Write-Output "saved $out"
