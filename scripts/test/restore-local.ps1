param([switch]$RestoreUi)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$settings=Get-Content -LiteralPath (Join-Path $repo '.local\test-env.local.json') -Raw -Encoding UTF8|ConvertFrom-Json
if(Get-CimInstance Win32_Process -Filter "Name='cs2.exe'"|Where-Object{$_.ExecutablePath -ieq $settings.ServerExe -and $_.CommandLine -match '-dedicated'}){throw 'Stop the verified test server before cleanup'}
$archive=Join-Path $settings.SessionRoot ('cleanup-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $archive -Force|Out-Null
Copy-Item -LiteralPath $settings.CorePath -Destination (Join-Path $archive 'core-test.jsonc')
Copy-Item -LiteralPath $settings.CoreBackup -Destination $settings.CorePath -Force
$plugins=[IO.Path]::GetFullPath((Join-Path $settings.FrameworkRoot 'plugins'))
$probe=[IO.Path]::GetFullPath($settings.ProbeRoot)
if($probe -ine (Join-Path $plugins 'ClassicWheelProbe')){throw 'Unexpected probe directory'}
if(Test-Path -LiteralPath $probe){Move-Item -LiteralPath $probe -Destination (Join-Path $archive 'ClassicWheelProbe')}
if($RestoreUi){
    foreach($resource in $settings.ResourceFiles){
        $target=[IO.Path]::GetFullPath($resource.Target)
        $valid=$false
        foreach($gameRoot in @($settings.ServerRoot,$settings.ClientRoot)){
            $allowed=Join-Path $gameRoot 'game\csgo\panorama'
            if($target.StartsWith([IO.Path]::GetFullPath($allowed)+'\',[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($target) -in @('hanclassicbuywheel.vxml_c','hanclassicbuywheel.vcss_c')){$valid=$true}
        }
        if(!$valid){throw 'Unexpected UI override target'}
        if($resource.Existed){Copy-Item -LiteralPath $resource.Backup -Destination $target -Force}
        elseif(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination (Join-Path $archive ('resource-'+[guid]::NewGuid().ToString('N')))}
    }
}
Write-Output ('Restored original framework config, archived probe: '+$archive)
Write-Output 'HanClassicBuyWheel and its editable configuration remain installed. UI remains unless RestoreUi was selected.'
