# 截取全部屏幕到 %LOCALAPPDATA%\KeyboardHint\shot.png（联调用）
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
$dir = Join-Path $env:LOCALAPPDATA 'KeyboardHint'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$out = Join-Path $dir 'shot.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved: $out  ($($vs.Width)x$($vs.Height) at $($vs.Left),$($vs.Top))"
