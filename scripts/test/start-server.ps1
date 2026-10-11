param()
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$envPath=Join-Path $repo '.local\test-env.local.json'
$settings=Get-Content -LiteralPath $envPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (Get-CimInstance Win32_Process -Filter "Name='cs2.exe'" | Where-Object { $_.ExecutablePath -ieq $settings.ServerExe -and $_.CommandLine -match '-dedicated' }) { throw '目标服务器已运行' }
$argsForServer=@('-dedicated','-insecure','-console','-usercon','-port',([string]$settings.Port),'+sv_lan','1','+game_type','0','+game_mode','0','+map','de_dust2')
$started=Start-Process -FilePath $settings.ServerExe -WorkingDirectory (Split-Path -Parent $settings.ServerExe) -ArgumentList $argsForServer -WindowStyle Hidden -PassThru
$settings | Add-Member -NotePropertyName ServerProcessId -NotePropertyValue $started.Id -Force
$settings | Add-Member -NotePropertyName ServerStartTime -NotePropertyValue $started.StartTime.ToUniversalTime().ToString('o') -Force
$settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $envPath -Encoding UTF8
Write-Output ('ServerProcessId='+$started.Id)
