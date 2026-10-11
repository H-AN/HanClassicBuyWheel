param(
    [Parameter(Mandatory=$true)][string]$ServerRoot,
    [Parameter(Mandatory=$true)][string]$ClientRoot,
    [Parameter(Mandatory=$true)][string]$GuideRoot,
    [int]$Port = 27019
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$serverExe = Join-Path $ServerRoot 'game\bin\win64\cs2.exe'
$clientExe = Join-Path $ClientRoot 'game\bin\win64\cs2.exe'
if (Get-CimInstance Win32_Process -Filter "Name='cs2.exe'" | Where-Object { $_.ExecutablePath -ieq $serverExe -and $_.CommandLine -match '-dedicated' }) { throw '目标专服已运行，不能修改其启动配置' }
if (!(Test-Path -LiteralPath $serverExe) -or !(Test-Path -LiteralPath $clientExe)) { throw '缺少服务端或客户端' }
$session = Join-Path $repo ('.local\run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path (Join-Path $session 'backups') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $session 'evidence') -Force | Out-Null
$framework = Join-Path $ServerRoot 'game\csgo\addons\swiftlys2'
$corePath = Join-Path $framework 'configs\core.jsonc'
$coreBackup = Join-Path $session 'backups\core.jsonc'
Copy-Item -LiteralPath $corePath -Destination $coreBackup
$coreText = [IO.File]::ReadAllText($corePath)
if ($coreText -notmatch '"ManualLoadPlugins"\s*:\s*(true|false)' -or $coreText -notmatch '"AutoHotReload"\s*:\s*(true|false)') { throw '框架没有预期的隔离加载配置' }
$plugin = Join-Path $framework 'plugins\HanClassicBuyWheel'
$probe = Join-Path $framework 'plugins\ClassicWheelProbe'
foreach ($path in @($plugin,$probe)) { if (Test-Path -LiteralPath $path) { throw ('已有同名目录，先做版本备份：'+$path) } }
& dotnet publish (Join-Path $repo 'tests\ServerProbe\ServerProbe.csproj') -c Release -o (Join-Path $session 'probe') -v quiet
if ($LASTEXITCODE -ne 0) { throw '探针编译失败' }
$resourceFiles = @()
foreach ($root in @($ServerRoot,$ClientRoot)) {
    foreach ($relative in @('panorama\layout\custom_game\hanclassicbuywheel.vxml_c','panorama\styles\custom_game\hanclassicbuywheel.vcss_c')) {
        $target = Join-Path (Join-Path $root 'game\csgo') $relative
        $resourceFiles += [PSCustomObject]@{ Target=$target; Source=(Join-Path (Join-Path $repo 'artifacts\addon') $relative); Existed=(Test-Path -LiteralPath $target); Backup=(Join-Path $session ('backups\resource-'+$resourceFiles.Count)) }
    }
}
$configPath = Join-Path $framework 'configs\plugins\HanClassicBuyWheel\HanClassicBuyWheel.jsonc'
if (Test-Path -LiteralPath $configPath) { throw '已有轮盘配置，不能覆盖' }
$environment = [ordered]@{ RepoRoot=$repo; SessionRoot=$session; EvidenceRoot=(Join-Path $session 'evidence'); GuideRoot=$GuideRoot; ServerRoot=$ServerRoot; ServerExe=$serverExe; ClientRoot=$ClientRoot; ClientExe=$clientExe; FrameworkRoot=$framework; CorePath=$corePath; CoreBackup=$coreBackup; PluginRoot=$plugin; ProbeRoot=$probe; ConfigPath=$configPath; ResourceFiles=$resourceFiles; Port=$Port }
$envPath = Join-Path $repo '.local\test-env.local.json'
$environment | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $envPath -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repo 'artifacts\server\plugins\HanClassicBuyWheel') -Destination $plugin -Recurse
Copy-Item -LiteralPath (Join-Path $session 'probe') -Destination $probe -Recurse
foreach ($resource in $resourceFiles) {
    if ($resource.Existed) { Copy-Item -LiteralPath $resource.Target -Destination $resource.Backup }
    New-Item -ItemType Directory -Path (Split-Path -Parent $resource.Target) -Force | Out-Null
    Copy-Item -LiteralPath $resource.Source -Destination $resource.Target -Force
}
$coreText = $coreText -replace '"ManualLoadPlugins"\s*:\s*(true|false)', '"ManualLoadPlugins": true' -replace '"AutoHotReload"\s*:\s*(true|false)', '"AutoHotReload": false'
[IO.File]::WriteAllText($corePath,$coreText,[Text.UTF8Encoding]::new($false))
Write-Output ('测试环境配置：'+$envPath)
Write-Output ('备份/证据：'+$session)
