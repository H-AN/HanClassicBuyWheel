param([ValidateSet('Move','Left','Right')][string]$Action='Move',[int]$DeltaX=0,[int]$DeltaY=0)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$settings=Get-Content -LiteralPath (Join-Path $repo '.local\test-env.local.json') -Raw -Encoding UTF8 | ConvertFrom-Json
. (Join-Path $settings.GuideRoot 'tools\Common.ps1')
$matches=@(Get-CimInstance Win32_Process -Filter "Name='cs2.exe'" | Where-Object { $_.ExecutablePath -ieq $settings.ClientExe -and $_.CommandLine -notmatch '-dedicated' })
if ($matches.Count -ne 1) { throw '需要唯一的目标客户端' }
$client=Assert-TestProcess -TargetProcessId $matches[0].ProcessId -ExpectedExe $settings.ClientExe -Role Client
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WheelTestMouse {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,int x,int y,uint data,UIntPtr extra);
}
'@
$shell=New-Object -ComObject WScript.Shell
$shell.AppActivate($client.Id) | Out-Null
[WheelTestMouse]::SetForegroundWindow($client.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 150
[uint32]$foregroundProcess=0
[WheelTestMouse]::GetWindowThreadProcessId([WheelTestMouse]::GetForegroundWindow(),[ref]$foregroundProcess) | Out-Null
if ($foregroundProcess -ne $client.Id) { throw '客户端不在前台，未发送输入' }
if ($Action -eq 'Move') {
    if ([Math]::Abs($DeltaX) -gt 1000 -or [Math]::Abs($DeltaY) -gt 1000) { throw '鼠标位移越界' }
    [WheelTestMouse]::mouse_event(1,$DeltaX,$DeltaY,0,[UIntPtr]::Zero)
} else {
    $down=if ($Action -eq 'Left') { 2 } else { 8 }; $up=if ($Action -eq 'Left') { 4 } else { 16 }
    [WheelTestMouse]::mouse_event($down,0,0,0,[UIntPtr]::Zero)
    try { Start-Sleep -Milliseconds 80 }
    finally { [WheelTestMouse]::mouse_event($up,0,0,0,[UIntPtr]::Zero) }
}
