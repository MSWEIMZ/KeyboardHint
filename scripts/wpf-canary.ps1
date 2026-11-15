# Canary: minimal WPF window on a fresh STA thread; screenshot its region.
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Drawing, System.Windows.Forms

$sync = [hashtable]::Synchronized(@{ ready = $false; hwnd = [IntPtr]::Zero })

$ts = [System.Threading.ThreadStart]{
    $s = $sync
    Add-Type -AssemblyName PresentationFramework
    $w = New-Object System.Windows.Window
    $w.Title = "canary"
    $w.Width = 420; $w.Height = 220
    $w.Left = 60; $w.Top = 60
    $w.WindowStartupLocation = 'Manual'
    $w.Topmost = $true
    $b = New-Object System.Windows.Controls.Border
    $b.Background = [System.Windows.Media.Brushes]::Red
    $t = New-Object System.Windows.Controls.TextBlock
    $t.Text = "WPF CANARY"
    $t.Foreground = [System.Windows.Media.Brushes]::White
    $t.FontSize = 40
    $t.HorizontalAlignment = 'Center'; $t.VerticalAlignment = 'Center'
    $b.Child = $t; $w.Content = $b
    $w.Show()
    $s.hwnd = (New-Object System.Windows.Interop.WindowInteropHelper($w)).Handle
    $s.ready = $true
    [System.Windows.Threading.Dispatcher]::Run()
}
$th = New-Object System.Threading.Thread($ts)
$th.SetApartmentState('STA'); $th.Start()
for ($i=0; $i -lt 50 -and -not $sync.ready; $i++) { Start-Sleep -Milliseconds 100 }
Start-Sleep -Milliseconds 1200
$bmp = New-Object System.Drawing.Bitmap 460, 260
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(40, 40, 0, 0, $bmp.Size)
$bmp.Save("$env:LOCALAPPDATA\KeyboardHint\canary.png")
$g.Dispose(); $bmp.Dispose()
Write-Output "canary hwnd=$($sync.hwnd) shot saved"
