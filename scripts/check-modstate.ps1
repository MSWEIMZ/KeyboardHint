Add-Type -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern short GetAsyncKeyState(int k);' -Name KS -Namespace X
$names = @{ 91="LWin"; 92="RWin"; 160="LShift"; 161="RShift"; 162="LCtrl"; 163="RCtrl"; 164="LAlt"; 165="RAlt" }
foreach ($k in $names.Keys) {
    $s = [X.KS]::GetAsyncKeyState($k)
    $down = ($s -band 0x8000) -ne 0
    Write-Output "$($names[$k]) vk=$k down=$down raw=$s"
}
