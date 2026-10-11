param([string]$Command,[string]$Label)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$settings=Get-Content -LiteralPath (Join-Path $repo '.local\test-env.local.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($Command) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $settings.GuideRoot 'tools\Send-TestServerConsole.ps1') -ServerProcessId $settings.ServerProcessId -ExpectedExe $settings.ServerExe -ExpectedStartTime $settings.ServerStartTime -Command $Command
    if ($LASTEXITCODE -ne 0) { throw '服务端输入失败' }
}
if ($Label) {
    if ($Label -notmatch '^[A-Za-z0-9_-]+$') { throw '证据标签无效' }
    $output=Join-Path $settings.EvidenceRoot ($Label+'.txt')
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $settings.GuideRoot 'tools\Read-TestServerConsole.ps1') -ServerProcessId $settings.ServerProcessId -ExpectedExe $settings.ServerExe -ExpectedStartTime $settings.ServerStartTime -OutputPath $output -TailLines 180
    if ($LASTEXITCODE -ne 0) { throw '服务端读取失败' }
    Get-Content -LiteralPath $output -Encoding UTF8 | Select-Object -Last 70
}
