param([string]$Configuration = 'Release', [string]$ToolsRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
& dotnet restore (Join-Path $repo 'src\HanClassicBuyWheel\HanClassicBuyWheel.csproj') --locked-mode
if ($LASTEXITCODE -ne 0) { throw '依赖还原失败' }
& dotnet publish (Join-Path $repo 'src\HanClassicBuyWheel\HanClassicBuyWheel.csproj') -c $Configuration --no-restore -o (Join-Path $repo 'artifacts\server\plugins\HanClassicBuyWheel')
if ($LASTEXITCODE -ne 0) { throw '插件构建失败' }
if (Test-Path -LiteralPath (Join-Path $repo 'artifacts\server\plugins\HanClassicBuyWheel\SwiftlyS2.CS2.dll')) { throw '正式包误包含框架 SDK' }
& dotnet run --project (Join-Path $repo 'tests\Checks\Checks.csproj') -c Release -- $repo
if ($LASTEXITCODE -ne 0) { throw '购买规则回归失败' }
if ($ToolsRoot) { & (Join-Path $PSScriptRoot 'build-panorama.ps1') -ToolsRoot $ToolsRoot }
