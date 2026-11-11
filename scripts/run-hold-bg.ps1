# 后台启动 hold-ctrl.ps1（参数：按住毫秒数，默认 5000）
$ms = if ($args.Count -gt 0) { $args[0] } else { 5000 }
Start-Process powershell -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',"$PSScriptRoot\hold-ctrl.ps1",$ms
Write-Output "hold-ctrl launched in background ($ms ms)"
