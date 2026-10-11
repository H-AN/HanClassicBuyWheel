param([switch]$Force)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repo 'config\HanClassicBuyWheel.jsonc'
if ((Test-Path -LiteralPath $target) -and !$Force) { throw '已有配置；重新生成默认值须明确传入 -Force' }
# CS2 默认价格；本地探针导出的价格表可用于核对。
$rows = @'
ak47|AK-47|primary.rifle|2700|2|ak47
galilar|Galil AR|primary.rifle|1800|2|galilar
famas|FAMAS|primary.rifle|1950|3|famas
m4a1|M4A4|primary.rifle|2900|3|m4a1
m4a1_silencer|M4A1-S|primary.rifle|2900|3|m4a1_silencer
sg556|SG 553|primary.rifle|3000|2|sg556
aug|AUG|primary.rifle|3300|3|aug
hkp2000|P2000|secondary|200|3|hkp2000
usp_silencer|USP-S|secondary|200|3|usp_silencer
glock|Glock-18|secondary|200|2|glock
p250|P250|secondary|300|2,3|p250
elite|Dual Berettas|secondary|300|2,3|elite
fiveseven|Five-SeveN|secondary|500|3|fiveseven
tec9|Tec-9|secondary|500|2|tec9
cz75a|CZ75-Auto|secondary|500|2,3|cz75a
deagle|Desert Eagle|secondary|700|2,3|deagle
revolver|R8 Revolver|secondary|600|2,3|revolver
mac10|MAC-10|primary.smg|1050|2|mac10
mp9|MP9|primary.smg|1250|3|mp9
ump45|UMP-45|primary.smg|1200|2,3|ump45
bizon|PP-Bizon|primary.smg|1300|2,3|bizon
mp7|MP7|primary.smg|1400|2,3|mp7
mp5sd|MP5-SD|primary.smg|1400|2,3|mp5sd
p90|P90|primary.smg|2350|2,3|p90
ssg08|SSG 08|primary.sniper|1700|2,3|ssg08
awp|AWP|primary.sniper|4750|2,3|awp
scar20|SCAR-20|primary.sniper|5000|3|scar20
g3sg1|G3SG1|primary.sniper|5000|2|g3sg1
nova|Nova|primary.shotgun|1050|2,3|nova
xm1014|XM1014|primary.shotgun|2000|2,3|xm1014
mag7|MAG-7|primary.shotgun|1300|3|mag7
sawedoff|Sawed-Off|primary.shotgun|1100|2|sawedoff
negev|Negev|primary.machinegun|1700|2,3|negev
m249|M249|primary.machinegun|5200|2,3|m249
hegrenade|高爆手雷|grenades|300|2,3|hegrenade
smokegrenade|烟雾弹|grenades|300|2,3|smokegrenade
molotov|燃烧瓶|grenades|400|2|molotov
incgrenade|燃烧弹|grenades|500|3|incgrenade
decoy|诱饵弹|grenades|50|2,3|decoy
flashbang|闪光弹|grenades|200|2,3|flashbang
taser|Zeus x27|equipment|200|2,3|taser
healthshot|回血针|equipment|0|2,3|healthshot
'@
$englishNames = @{ hegrenade='HE Grenade'; smokegrenade='Smoke Grenade'; molotov='Molotov'; incgrenade='Incendiary Grenade'; decoy='Decoy Grenade'; flashbang='Flashbang'; healthshot='Healthshot'; kevlar='Kevlar'; assaultsuit='Kevlar + Helmet'; defuser='Defuse Kit' }
$items = @(); $order = 0
foreach ($row in ($rows -split '\r?\n')) {
    $parts = $row.Split('|')
    $englishName=if ($englishNames.ContainsKey($parts[0])) { $englishNames[$parts[0]] } else { $parts[1] }
    $category = switch ($parts[2]) { 'primary.rifle' {'rifle'} 'primary.sniper' {'rifle'} 'primary.smg' {'smg'} 'primary.shotgun' {'heavy'} 'primary.machinegun' {'heavy'} default {$parts[2]} }
    $item = [ordered]@{ Id=$parts[0]; Name=$parts[1]; NameEnglish=$englishName; Classname=('weapon_'+$parts[0]); Category=$category; Price=[int]$parts[3]; Teams=@($parts[4].Split(',') | ForEach-Object { [int]$_ }); Kind='Weapon'; Enabled=($parts[0] -ne 'healthshot'); StrictTeam=$false; Order=$order; CarryLimit=1; Icon=$parts[5]; ClassicSlotCT=-1; ClassicSlotT=-1; DefinitionIndex=0 }
    if ($parts[0] -eq 'flashbang') { $item.CarryLimit = 2 }
    $items += $item; $order++
}
foreach ($equipment in @(@('kevlar','防弹衣','Kevlar',650),@('assaultsuit','防弹衣 + 头盔','AssaultSuit',1000),@('defuser','拆弹器','Defuser',400))) {
    $items += [ordered]@{ Id=$equipment[0]; Name=$equipment[1]; NameEnglish=$englishNames[$equipment[0]]; Classname=''; Category='equipment'; Price=[int]$equipment[3]; Teams=[int[]]$(if ($equipment[0] -eq 'defuser') { @(3) } else { @(2,3) }); Kind=$equipment[2]; Enabled=$true; StrictTeam=($equipment[0] -eq 'defuser'); Order=$order; CarryLimit=1; Icon=$equipment[0] }; $order++
}
# 槽位与提供的原版截图一致；-1 表示只在全武器模式显示。
$ct=@{secondary=@('hkp2000','elite','p250','fiveseven','deagle');heavy=@('nova','xm1014','mag7','m249','negev');smg=@('mp9','mp7','ump45','p90','bizon');rifle=@('famas','m4a1','ssg08','aug','awp','scar20');equipment=@('kevlar','assaultsuit','taser','defuser');grenades=@('incgrenade','decoy','flashbang','hegrenade','smokegrenade')}
$t=@{secondary=@('glock','elite','p250','tec9','deagle');heavy=@('nova','xm1014','sawedoff','m249','negev');smg=@('mac10','mp7','ump45','p90','bizon');rifle=@('galilar','ak47','ssg08','sg556','awp','g3sg1');equipment=@('kevlar','assaultsuit','taser','defuser');grenades=@('molotov','decoy','flashbang','hegrenade','smokegrenade')}
foreach($item in $items){$item.ClassicSlotCT=[Array]::IndexOf($ct[$item.Category],$item.Id);$item.ClassicSlotT=[Array]::IndexOf($t[$item.Category],$item.Id);$item.DefinitionIndex=0}
$knives=@(); $knifeRows=@'
42|默认 CT 刀|Default CT Knife|knife
59|默认 T 刀|Default T Knife|knife_t
500|刺刀|Bayonet|bayonet
503|海豹短刀|Classic Knife|knife_css
505|折叠刀|Flip Knife|knife_flip
506|穿肠刀|Gut Knife|knife_gut
507|爪子刀|Karambit|knife_karambit
508|M9 刺刀|M9 Bayonet|knife_m9_bayonet
509|猎杀者匕首|Huntsman Knife|knife_tactical
512|弯刀|Falchion Knife|knife_falchion
514|鲍伊猎刀|Bowie Knife|knife_survival_bowie
515|蝴蝶刀|Butterfly Knife|knife_butterfly
516|暗影双匕|Shadow Daggers|knife_push
517|系绳匕首|Paracord Knife|knife_cord
518|求生匕首|Survival Knife|knife_canis
519|熊刀|Ursus Knife|knife_ursus
520|折刀|Navaja Knife|knife_gypsy_jackknife
521|流浪者匕首|Nomad Knife|knife_outdoor
522|短剑|Stiletto Knife|knife_stiletto
523|锯齿爪刀|Talon Knife|knife_widowmaker
525|骷髅匕首|Skeleton Knife|knife_skeleton
526|廓尔喀刀|Kukri Knife|knife_kukri
'@
foreach($row in ($knifeRows -split '\r?\n')){$p=$row.Split('|');$knives+=[ordered]@{Id=('knife_'+$p[0]);Name=$p[1];NameEnglish=$p[2];Classname='weapon_knife';Category='knives';Price=0;Teams=@(2,3);Kind='Knife';Enabled=$true;StrictTeam=$false;Order=[int]$p[0];CarryLimit=1;Icon=$p[3];DefinitionIndex=[int]$p[0];ClassicSlotCT=-1;ClassicSlotT=-1}}
$config=[ordered]@{Enabled=$true;AllWeapons=$false;AllowedTeam='any';AllowKnives=$false;OpenCommand='buywheel';UseNativeBuyKey=$false;ClassicStyle=$true;CloseOnDuck=$true;LanguageMode='Auto';DefaultLanguage='en';RequireBuyZone=$true;RequireBuyTime=$true;BlockNativePurchases=$true;FreezeMovement=$true;SoundsEnabled=$true;SoundVolume=0.75;ClickCooldownMs=220;MaxGrenades=3;MaxFlashbangs=2;RoundStartMoneyEnabled=$false;RoundStartMoneyAdd=$true;RoundStartMoney=1000;BannedWeapons=@()}
$utf8=New-Object Text.UTF8Encoding($false)
function WriteConfig($name,$body,$comment){$output=Join-Path $repo ('config\'+$name);New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force|Out-Null;[IO.File]::WriteAllText($output,('// '+$comment+"`n"+($body|ConvertTo-Json -Depth 8)),$utf8);$template=Join-Path $repo ('src\HanClassicBuyWheel\resources\templates\'+$name);Copy-Item -LiteralPath $output -Destination $template -Force}
WriteConfig 'HanClassicBuyWheel.jsonc' @{HanClassicBuyWheel=$config} ("主配置。AllWeapons=false 经典六分类；AllowedTeam=ct/t/any；OpenCommand 不含 sw_。修改后 sw_buywheel_reload。`n// RequireBuyZone=false：任何地点可购买，原生 B 同步放开。`n// RequireBuyTime=false：无限购买时间；true 则沿用服务器原买时。")
WriteConfig 'Weapons.jsonc' @{Weapons=@{Items=$items}} '独立武器配置。Price 自定义价格；ClassicSlotCT/T=0~5，装备0~3；-1仅全武器显示。换用 USP-S 等时先将原武器槽改为-1。'
WriteConfig 'Knives.jsonc' @{Knives=@{Items=$knives}} '独立刀具配置。主配置 AllowKnives=true 启用；DefinitionIndex 刀型编号；Price=0 免费。'
$config.AllWeapons=$true
[IO.File]::WriteAllText((Join-Path $repo 'config\all.jsonc'),(@{HanClassicBuyWheel=$config}|ConvertTo-Json -Depth 8),$utf8)
$config.AllowedTeam='ct'
[IO.File]::WriteAllText((Join-Path $repo 'config\only-ct.jsonc'),(@{HanClassicBuyWheel=$config}|ConvertTo-Json -Depth 8),$utf8)
Write-Output '已生成主配置、武器配置、刀具配置及全武器/CT 示例。'
