param()
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$plugin=Join-Path $repo 'artifacts\server\plugins\HanClassicBuyWheel'
$addon=Join-Path $repo 'artifacts\addon'
foreach($file in @((Join-Path $plugin 'HanClassicBuyWheel.dll'),(Join-Path $addon 'panorama\layout\custom_game\hanclassicbuywheel.vxml_c'),(Join-Path $addon 'panorama\styles\custom_game\hanclassicbuywheel.vcss_c'))){if(!(Test-Path -LiteralPath $file)){throw ('Missing build artifact: '+$file)}}
[xml]$project=Get-Content -LiteralPath (Join-Path $repo 'src\HanClassicBuyWheel\HanClassicBuyWheel.csproj') -Raw
$name='HanClassicBuyWheel-'+$project.Project.PropertyGroup.Version+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$stage=Join-Path $repo ('artifacts\packages\'+$name)
New-Item -ItemType Directory -Path (Join-Path $stage 'server\plugins') -Force|Out-Null
Copy-Item -LiteralPath $plugin -Destination (Join-Path $stage 'server\plugins\HanClassicBuyWheel') -Recurse
Copy-Item -LiteralPath $addon -Destination (Join-Path $stage 'addon') -Recurse
foreach($name in @('config','docs','panorama')){Copy-Item -LiteralPath (Join-Path $repo $name) -Destination (Join-Path $stage $name) -Recurse}
Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination $stage
$files=@(Get-ChildItem -LiteralPath $stage -File -Recurse)
if($files.Name -match '^SwiftlyS2.*\.dll$|ClassicWheelProbe|ServerProbe|code\.pbin'){throw 'Excluded runtime/reference/test file in package'}
$hashes=foreach($file in $files){[ordered]@{Path=$file.FullName.Substring($stage.Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}}
[IO.File]::WriteAllText((Join-Path $stage 'manifest.json'),($hashes|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$zip=$stage+'.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Output $zip
