param([string]$Src, [string]$Dst, [int]$X, [int]$Y, [int]$W, [int]$H)
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile($Src)
$rect = New-Object System.Drawing.Rectangle($X, $Y, $W, $H)
$crop = $img.Clone($rect, $img.PixelFormat)
$crop.Save($Dst)
$img.Dispose(); $crop.Dispose()
Write-Output "cropped: $Dst ($W x $H)"
