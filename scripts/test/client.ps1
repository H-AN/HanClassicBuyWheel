param([string]$Action='Screenshot',[string]$Command,[string]$Label,[int]$X=0,[int]$Y=0,[byte]$KeyCode=27)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$settings=Get-Content -LiteralPath (Join-Path $repo '.local\test-env.local.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$clients=@(Get-CimInstance Win32_Process -Filter "Name='cs2.exe'" | Where-Object { $_.ExecutablePath -ieq $settings.ClientExe -and $_.CommandLine -notmatch '-dedicated' })
if ($clients.Count -ne 1) { throw '需要唯一的目标客户端' }
$client=Get-Process -Id $clients[0].ProcessId
$focusHelper=New-Object -ComObject WScript.Shell
$focusHelper.AppActivate($client.Id) | Out-Null
$arguments=@{ ClientProcessId=$client.Id; ExpectedExe=$settings.ClientExe; ExpectedStartTime=$client.StartTime.ToUniversalTime().ToString('o'); Action=$Action; X=$X; Y=$Y; KeyCode=$KeyCode }
if ($Command) { $arguments.Command=$Command }
if ($Label) { if ($Label -notmatch '^[A-Za-z0-9_-]+$') { throw '证据标签无效' }; $arguments.OutputPath=Join-Path $settings.EvidenceRoot ($Label+'.png') }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $settings.GuideRoot 'tools\Invoke-TestClient.ps1') @arguments
if ($LASTEXITCODE -ne 0) { throw '客户端动作失败' }
