param([Parameter(Mandatory=$true)][string]$BackupPath)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$backup=(Resolve-Path -LiteralPath $BackupPath).Path
$source=Join-Path $backup 'source'
if(!(Test-Path -LiteralPath (Join-Path $source 'src\HanClassicBuyWheel\HanClassicBuyWheel.csproj')) -or !(Test-Path -LiteralPath (Join-Path $backup 'source-files.txt'))){throw 'Invalid baseline backup'}
$archive=Join-Path $repo ('artifacts\backups\before-restore-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $archive -Force | Out-Null
# Move exact known directories first: leaving newer source files would break the older build.
foreach($name in @('src','scripts','tests','panorama','config','docs','README.md')){
    $target=[IO.Path]::GetFullPath((Join-Path $repo $name))
    if(!$target.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Target outside repository'}
    if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination (Join-Path $archive $name)}
    $saved=Join-Path $source $name
    if(Test-Path -LiteralPath $saved){Copy-Item -LiteralPath $saved -Destination $target -Recurse}
}
foreach($name in @('server','addon')){
    $target=[IO.Path]::GetFullPath((Join-Path $repo ('artifacts\'+$name)))
    if(!$target.StartsWith((Join-Path $repo 'artifacts')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Artifact target outside repository'}
    if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination (Join-Path $archive $name)}
    if(Test-Path -LiteralPath (Join-Path $backup $name)){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination $target -Recurse}
}
Write-Output ('Restored baseline. Current version archived at: '+$archive)
Write-Output 'Server/client were not changed. Rebuild and redeploy the restored version when ready.'
