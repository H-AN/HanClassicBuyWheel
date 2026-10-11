param([Parameter(Mandatory=$true)][string]$ToolsRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$addonName = 'hanclassicbuywheel_panorama'
$content = Join-Path $ToolsRoot "content\csgo_addons\$addonName"
$game = Join-Path $ToolsRoot "game\csgo_addons\$addonName"
$compiler = Join-Path $ToolsRoot 'game\bin\win64\resourcecompiler.exe'
if (!(Test-Path -LiteralPath $compiler -PathType Leaf)) { throw '缺少 CS2 Workshop Tools 资源编译器' }
[xml]$layout = Get-Content -LiteralPath (Join-Path $repo 'panorama\layout\custom_game\hanclassicbuywheel.vxml') -Raw -Encoding UTF8
$ids = @{}
foreach ($node in $layout.SelectNodes('//*')) {
    if ($node.Name -notin @('root','styles','include','Panel','Label','Button','Image')) { throw ('布局节点不受支持：'+$node.Name) }
    if ($node.Name -in @('Button','Image') -and $node.HasAttribute('hittest')) { throw ('Custom HUD 禁止此属性：'+$node.Name+'.hittest') }
    if ($node.Name -eq 'Image' -and $node.HasAttribute('scaling')) { throw 'Custom HUD 禁止 Image.scaling；使用 CSS background-size:contain 保持比例' }
    if ($node.id) { if ($ids.ContainsKey($node.id)) { throw ('重复 ID：'+$node.id) }; $ids[$node.id]=$true }
}
$backup = Join-Path $repo ('artifacts\backups\panorama-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
foreach ($entry in @(@('content',$content),@('game',$game))) {
    if (Test-Path -LiteralPath $entry[1]) {
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Copy-Item -LiteralPath $entry[1] -Destination (Join-Path $backup $entry[0]) -Recurse
    }
}
foreach ($kind in @('layout','styles')) {
    $extension = if ($kind -eq 'layout') { 'vxml' } else { 'vcss' }
    $relative = "panorama\$kind\custom_game\hanclassicbuywheel.$extension"
    $destination = Join-Path $content $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $destination -Force
}
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $content 'panorama\preprocessor_config.txt'), '"PanzipCfg" { "BlockDefs" { } }', $utf8)
$addonInfo = @'
<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
{ IsPlayable = false }
'@
[IO.File]::WriteAllText((Join-Path $content 'addoninfo.txt'), $addonInfo, $utf8)
New-Item -ItemType Directory -Path $game -Force | Out-Null
$compiledAt = [DateTime]::UtcNow
& $compiler -game (Join-Path $ToolsRoot 'game\csgo') -i (Join-Path $content 'panorama\styles\custom_game\hanclassicbuywheel.vcss') -i (Join-Path $content 'panorama\layout\custom_game\hanclassicbuywheel.vxml') -f -nop4
if ($LASTEXITCODE -ne 0) { throw ('资源编译失败：'+$LASTEXITCODE) }
foreach ($relative in @('panorama\styles\custom_game\hanclassicbuywheel.vcss_c','panorama\layout\custom_game\hanclassicbuywheel.vxml_c')) {
    $source = Join-Path $game $relative
    if (!(Test-Path -LiteralPath $source) -or (Get-Item -LiteralPath $source).LastWriteTimeUtc -lt $compiledAt.AddSeconds(-2)) { throw ('本次编译没有生成资源：'+$relative) }
    $target = Join-Path (Join-Path $repo 'artifacts\addon') $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    Get-FileHash -LiteralPath $target -Algorithm SHA256
}
Copy-Item -LiteralPath (Join-Path $content 'addoninfo.txt') -Destination (Join-Path $game 'addoninfo.txt') -Force
Copy-Item -LiteralPath (Join-Path $content 'addoninfo.txt') -Destination (Join-Path $repo 'artifacts\addon\addoninfo.txt') -Force
Write-Output '资源已编译到 artifacts/addon；尚未发布 Workshop。'
