param(
    [Parameter(Mandatory=$true)][string]$Pbin,
    [string]$OutputRoot
)
$ErrorActionPreference = 'Stop'
if (!$OutputRoot) { $OutputRoot = Join-Path (Split-Path -Parent $PSScriptRoot) '.local\reference' }
$bytes = [IO.File]::ReadAllBytes($Pbin)
if ($bytes.Length -lt 68 -or [Text.Encoding]::ASCII.GetString($bytes, 0, 3) -ne 'PAN') { throw '不是 PAN 格式资源包' }
$wanted = @('panorama\layout\buymenu.xml', 'panorama\styles\buymenu.css', 'panorama\scripts\buymenu.js', 'panorama\csgo_keybinds.cfg')
$found = @()
for ($offset = 4; $offset -lt $bytes.Length - 30; $offset++) {
    if ([BitConverter]::ToUInt32($bytes, $offset) -ne 0x04034b50) { continue }
    $flags = [BitConverter]::ToUInt16($bytes, $offset + 6)
    $method = [BitConverter]::ToUInt16($bytes, $offset + 8)
    $size = [BitConverter]::ToUInt32($bytes, $offset + 18)
    $nameLength = [BitConverter]::ToUInt16($bytes, $offset + 26)
    $extraLength = [BitConverter]::ToUInt16($bytes, $offset + 28)
    $dataOffset = $offset + 30 + $nameLength + $extraLength
    if ($dataOffset + $size -gt $bytes.Length) { throw 'ZIP 条目越界' }
    $name = [Text.Encoding]::UTF8.GetString($bytes, $offset + 30, $nameLength)
    if ($wanted -contains $name) {
        if ($flags -band 9) { throw '不支持加密或数据描述符条目' }
        if ($method -ne 0) { throw '参考条目不是 ZIP Store 方法' }
        $target = Join-Path $OutputRoot $name
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        $content = New-Object byte[] $size
        [Array]::Copy($bytes, $dataOffset, $content, 0, $size)
        [IO.File]::WriteAllBytes($target, $content)
        $found += $name
        Write-Output "$name : $size bytes"
    }
    $offset = $dataOffset + $size - 1
}
if ($found.Count -ne $wanted.Count) { throw '未找到全部购买轮盘参考资源' }
Get-FileHash -LiteralPath $Pbin -Algorithm SHA256
