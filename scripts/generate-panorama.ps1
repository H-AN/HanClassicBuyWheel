param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$weaponText = [IO.File]::ReadAllText((Join-Path $repo 'config\Weapons.jsonc')) -replace '(?m)^\s*//[^\r\n]*', ''
$knifeText = [IO.File]::ReadAllText((Join-Path $repo 'config\Knives.jsonc')) -replace '(?m)^\s*//[^\r\n]*', ''
$icons = @(@(($weaponText | ConvertFrom-Json).Weapons.Items) + @(($knifeText | ConvertFrom-Json).Knives.Items) | ForEach-Object { $_.Icon } | Where-Object { $_ } | Sort-Object -Unique)
$xml = New-Object Text.StringBuilder
[void]$xml.AppendLine('<root><styles><include src="s2r://panorama/styles/custom_game/hanclassicbuywheel.vcss_c" /></styles>')
[void]$xml.AppendLine('<Panel class="Screen" hittest="false"><Panel id="hcw_screen" class="TeamScope" hittest="false"><Panel class="Window" hittest="false">')
[void]$xml.AppendLine('<Panel class="Header"><Label id="hcw_team" class="Team" text="{s:value}" /><Label id="hcw_title" class="Title" text="{s:value}" /><Label id="hcw_money" class="Money" text="{s:value}" /></Panel>')
[void]$xml.AppendLine('<Panel class="Body"><Panel id="hcw_wheel" class="Wheel" hittest="false">')
for ($i=0; $i -lt 12; $i++) {
    [void]$xml.AppendLine(('<Panel id="hcw_slot_{0}" class="Slot slot_{0}" hittest="false"><Panel class="Wedge" hittest="false" /><Label class="Number" text="{1}" /><Panel id="hcw_card_{0}" class="Card" hittest="false"><Panel class="Icons" hittest="false">' -f $i,($i+1)))
    foreach ($icon in $icons) {
        if ($icon -notmatch '^[a-z0-9_]+$') { throw '图标名称无效' }
        [void]$xml.AppendLine(('<Panel class="Icon img_{0}" hittest="false" />' -f $icon))
    }
    [void]$xml.AppendLine(('</Panel><Label id="hcw_name_{0}" class="ItemName" text="{{s:value}}" /><Label id="hcw_price_{0}" class="Price" text="{{s:value}}" /><Button id="hcw_choice_{0}" class="HitTarget" /></Panel></Panel>' -f $i))
}
for ($i=0; $i -lt 12; $i++) { [void]$xml.AppendLine(('<Panel class="Seam seam_{0}" hittest="false" />' -f $i)) }
[void]$xml.AppendLine('<Button id="hcw_back" class="Center" hittest="true"><Label id="hcw_back_text" class="CenterHint" text="{s:value}" /></Button><Panel class="Rim" hittest="false" /></Panel>')
[void]$xml.AppendLine('<Panel class="Details" hittest="false"><Label id="hcw_mode" class="Mode" text="{s:value}" /><Label id="hcw_path" class="Path" text="{s:value}" /><Label id="hcw_selected" class="SelectedName" text="{s:value}" /><Label id="hcw_details" class="DetailText" text="{s:value}" /><Label id="hcw_status" class="Status" text="{s:value}" /></Panel></Panel>')
[void]$xml.AppendLine('<Panel class="Footer"><Label id="hcw_hint" class="Hint" text="{s:value}" /><Label id="hcw_page" class="Page" text="{s:value}" /><Button id="hcw_prev" class="FooterButton"><Label id="hcw_prev_text" text="{s:value}" /></Button><Button id="hcw_next" class="FooterButton"><Label id="hcw_next_text" text="{s:value}" /></Button><Button id="hcw_home" class="FooterButton"><Label id="hcw_home_text" text="{s:value}" /></Button><Button id="hcw_close" class="FooterButton"><Label id="hcw_close_text" text="{s:value}" /></Button><Label id="hcw_credit" class="Credit" text="{s:value}" /><Button id="hcw_language" class="FooterButton"><Label id="hcw_language_text" text="{s:value}" /></Button></Panel></Panel></Panel></Panel></root>')
$css = New-Object Text.StringBuilder
[void]$css.AppendLine(@'
.Screen { width: 100%; height: 100%; background-color: #080a0cc9; }
.TeamScope { width: 100%; height: 100%; }
.Window { width: 1130px; height: 865px; horizontal-align: center; vertical-align: center; flow-children: down; }
Label { color: #eee8dc; font-family: Stratum2, "Noto Sans CJK SC", "Microsoft YaHei", Arial; text-overflow: shrink; }
.Header { width: 100%; height: 90px; flow-children: none; border-bottom: 1px solid #ffffff18; }
.Team { color: #d9b76c; font-size: 17px; margin-top: 12px; letter-spacing: 3px; }
.ct .Team { color: #82b8df; }
.Title { font-size: 36px; margin-top: 40px; }
.Money { horizontal-align: right; vertical-align: center; color: #b7d79e; font-size: 44px; }
.Body { width: 100%; height: 700px; flow-children: right; }
.Wheel { width: 680px; height: 680px; margin-top: 10px; flow-children: none; border-radius: 50%; background-color: #25292be6; border: 0px solid transparent; }
.Rim { width: 100%; height: 100%; border-radius: 50%; border: 2px solid #d9b76c55; }
.ct .Rim { border-color: #82b8df55; }
.Seam { width: 100%; height: 100%; border-radius: 50%; background-color: #d9b76c; visibility: collapse; }
.ct .Seam { background-color: #82b8df; }
.Slot { width: 100%; height: 100%; flow-children: none; }
.Wedge { width: 100%; height: 100%; background-color: #ffffff04; border-radius: 50%; }
.HitTarget { horizontal-align: center; vertical-align: center; border-radius: 0px; background-color: transparent; border: 0px solid transparent; }
.HitTarget:hover { background-color: transparent; border: 0px solid transparent; }
.ct .HitTarget:hover { background-color: transparent; border-color: transparent; }
.Slot:hover .Wedge { background-color: #d9b76c38; }
.ct .Slot:hover .Wedge { background-color: #82b8df38; }
.selected .Wedge { background-color: #d9b76c48; }
.ct .selected .Wedge { background-color: #82b8df48; }
.disabled .Card { opacity: 0.35; }
.disabled .Price { color: #e68b81; }
.Card { width: 144px; height: 120px; flow-children: none; horizontal-align: left; vertical-align: top; }
.Icons { width: 100%; height: 55px; margin-top: 8px; }
.Icon { width: 115px; height: 48px; horizontal-align: center; vertical-align: center; wash-color: #eee8dc; visibility: collapse; }
.ItemName { width: 100%; height: 28px; margin-top: 63px; text-align: center; font-size: 18px; }
.Price { width: 100%; height: 24px; margin-top: 91px; text-align: center; font-size: 17px; color: #b7d79e; }
.Center { width: 150px; height: 150px; horizontal-align: center; vertical-align: center; border-radius: 50%; background-color: #15191a; border: 1px solid #ffffff18; flow-children: down; }
.CenterMark { horizontal-align: center; margin-top: 43px; font-size: 30px; color: #d9b76c; letter-spacing: 3px; }
.ct .CenterMark { color: #82b8df; }
.CenterHint { horizontal-align: center; vertical-align: center; font-size: 18px; color: #d9b76c; }
.ct .CenterHint { color: #82b8df; }
.Details { width: 400px; height: 620px; margin-left: 44px; margin-top: 64px; flow-children: down; }
.Mode { font-size: 16px; color: #d9b76c; letter-spacing: 2px; }
.Path { font-size: 17px; color: #929996; margin-top: 12px; }
.SelectedName { font-size: 44px; margin-top: 72px; width: 100%; height: 60px; }
.DetailText { font-size: 24px; color: #b8b9b0; white-space: normal; margin-top: 20px; line-height: 1.7; }
.Status { font-size: 20px; color: #b7d79e; margin-top: 36px; width: 100%; white-space: normal; }
.Footer { width: 100%; height: 70px; border-top: 1px solid #ffffff18; flow-children: right; padding-top: 14px; }
.Hint { width: 350px; font-size: 16px; vertical-align: center; color: #a3aaa5; }
.Page { width: 55px; font-size: 15px; vertical-align: center; }
.FooterButton { width: 90px; height: 40px; margin-left: 5px; border: 1px solid #ffffff22; background-color: #20292b; }
.Credit { width: 230px; margin-left: 8px; font-size: 13px; vertical-align: center; color: #d9b76c; }
.ct .Credit { color: #82b8df; }
.FooterButton Label { horizontal-align: center; vertical-align: center; font-size: 17px; }
.FooterButton:hover { background-color: #3f4a4b; }
.empty { visibility: collapse; }
.root .Card { width: 190px; height: 160px; }
.root .ItemName { margin-top: 66px; font-size: 24px; }
.root .Price { margin-top: 94px; font-size: 18px; }
.blank .Card { visibility: collapse; }
.Number { width: 30px; height: 24px; text-align: center; font-size: 20px; color: #c5c5c5; visibility: collapse; }
.classic .Number { visibility: visible; }
.classic .Wheel { background-color: #101010e8; }
.classic .Rim { border: 7px solid #484848; }
.classic .Seam { background-color: #484848; }
.classic .Wedge { background-color: gradient(radial, 50% 50%, 0% 0%, 70% 70%, from(#262626), to(#131313)); }
.classic .Slot:hover .Wedge { background-color: gradient(radial, 50% 50%, 0% 0%, 70% 70%, from(#595959), to(#363636)); }
.classic .HitTarget { border-radius: 2px; }
.classic .HitTarget:hover { background-color: transparent; border: 0px solid transparent; }
.classic .ItemName { margin-top: 0px; font-size: 19px; color: #eeeeee; }
.classic .Icons { margin-top: 29px; height: 56px; }
.classic .Price { margin-top: 92px; color: #9acd72; }
.classic .disabled .Price { color: #b67a7a; }
.classic .disabled .Card { opacity: 0.7; }
.classic .Center { background-color: #171717; border: 2px solid #505050; }
.classic .root .ItemName { margin-top: 65px; font-size: 26px; }
.classic .root .Icons { visibility: collapse; }
.classic .root .Price { visibility: collapse; }
'@)
foreach ($icon in $icons) {
    [void]$css.AppendLine(('.img_{0} {{ background-image: url("s2r://panorama/images/icons/equipment/{0}.vsvg"); background-size: contain; background-position: center; background-repeat: no-repeat; }}' -f $icon))
    [void]$css.AppendLine(('.icon_{0} .img_{0} {{ visibility: visible; }}' -f $icon))
}
# 资源只预编译几何，不硬编码武器槽位。服务端按数据数量切 count_N 类。
$culture = [Globalization.CultureInfo]::InvariantCulture
$hitRegions = @()
function FitHitRegion([double]$centerX,[double]$centerY,[double]$angle,[int]$count,[double]$width,[double]$height) {
    $halfAngle=(180.0/$count-2.0)*[Math]::PI/180
    $low=0.0; $high=1.0
    for($iteration=0;$iteration -lt 28;$iteration++) {
        $scale=($low+$high)/2; $fits=$true
        foreach($signX in @(-1,1)){foreach($signY in @(-1,1)){
            $dx=$centerX-340+$signX*$width*$scale/2; $dy=$centerY-340+$signY*$height*$scale/2
            $dot=$dx*[Math]::Cos($angle)+$dy*[Math]::Sin($angle)
            $cross=$dy*[Math]::Cos($angle)-$dx*[Math]::Sin($angle)
            if($dx*$dx+$dy*$dy -gt 328*328 -or $dot -lt 90 -or ($count -gt 1 -and [Math]::Abs([Math]::Atan2($cross,$dot)) -gt $halfAngle)){$fits=$false}
        }}
        if($fits){$low=$scale}else{$high=$scale}
    }
    return @(($width*$low),($height*$low))
}
for ($n=1; $n -le 12; $n++) {
    for ($i=0; $i -lt $n; $i++) {
        $step = 360.0/$n; $offset=if($n -eq 6){-30.0}else{0.0}; $angle=(-90+$offset+$step*$i)*[Math]::PI/180
        $x=340+[Math]::Cos($angle)*235-72; $y=340+[Math]::Sin($angle)*235-60
        $hit=FitHitRegion ($x+72) ($y+60) $angle $n 144 120
        [void]$css.AppendLine(('.count_{0} .slot_{1} .HitTarget {{ width: {2}px; height: {3}px; }}' -f $n,$i,$hit[0].ToString('F2',$culture),$hit[1].ToString('F2',$culture)))
        $hitRegions+=[ordered]@{Root=$false;Count=$n;Index=$i;CenterX=($x+72);CenterY=($y+60);Angle=$angle;Width=$hit[0];Height=$hit[1]}
        # Panorama radial 的 0 度在正上方，卡片的三角函数角度才以右侧为 0。
        $start=($offset+$step*$i-$step/2+360)%360
        $span=$step
        [void]$css.AppendLine(('.count_{0} .slot_{1} .Card {{ position: {2}px {3}px 0px; }}' -f $n,$i,$x.ToString('F2',$culture),$y.ToString('F2',$culture)))
        [void]$css.AppendLine(('.count_{0} .slot_{1} .Wedge {{ clip: radial(50% 50%, {2}deg, {3}deg); }}' -f $n,$i,$start.ToString('F2',$culture),$span.ToString('F2',$culture)))
        if ($n -gt 1) {
            $seamStart=($start-0.6+360)%360
            [void]$css.AppendLine(('.count_{0} .seam_{1} {{ visibility: visible; clip: radial(50% 50%, {2}deg, 1.20deg); }}' -f $n,$i,$seamStart.ToString('F2',$culture)))
        }
        $numberX=340+[Math]::Cos($angle)*110-15; $numberY=340+[Math]::Sin($angle)*110-12
        [void]$css.AppendLine(('.count_{0} .slot_{1} .Number {{ position: {2}px {3}px 0px; }}' -f $n,$i,$numberX.ToString('F2',$culture),$numberY.ToString('F2',$culture)))
    }
}
$rootWidth=190.0; $rootHeight=160.0; $rootRadius=225.0
foreach($rootCount in @(6,7)) { for ($i=0; $i -lt $rootCount; $i++) {
    $offset=if($rootCount -eq 6){-30.0}else{0.0}; $angle=(-90+$offset+(360.0/$rootCount)*$i)*[Math]::PI/180
    $centerX=340+[Math]::Cos($angle)*$rootRadius; $centerY=340+[Math]::Sin($angle)*$rootRadius
    $left=$centerX-$rootWidth/2; $top=$centerY-$rootHeight/2
    $hit=FitHitRegion $centerX $centerY $angle $rootCount $rootWidth $rootHeight
    [void]$css.AppendLine(('.root.count_{0} .slot_{1} .HitTarget {{ width: {2}px; height: {3}px; }}' -f $rootCount,$i,$hit[0].ToString('F2',$culture),$hit[1].ToString('F2',$culture)))
    $hitRegions+=[ordered]@{Root=$true;Count=$rootCount;Index=$i;CenterX=$centerX;CenterY=$centerY;Angle=$angle;Width=$hit[0];Height=$hit[1]}
    [void]$css.AppendLine(('.root.count_{0} .slot_{1} .Card {{ position: {2}px {3}px 0px; }}' -f $rootCount,$i,$left.ToString('F2',$culture),$top.ToString('F2',$culture)))
} }
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($kind in @('layout','styles')) { New-Item -ItemType Directory -Path (Join-Path $repo "panorama\$kind\custom_game") -Force | Out-Null }
# CS2 Custom HUD 的属性白名单与普通 Panorama 不同：Button/Image 禁止 hittest。
$safeXml = $xml.ToString() -replace '(<(?:Button|Image)\b[^>]*?) hittest="(?:true|false)"', '$1'
[IO.File]::WriteAllText((Join-Path $repo 'panorama\layout\custom_game\hanclassicbuywheel.vxml'),$safeXml,$utf8)
[IO.File]::WriteAllText((Join-Path $repo 'panorama\styles\custom_game\hanclassicbuywheel.vcss'),$css.ToString(),$utf8)
[IO.File]::WriteAllText((Join-Path $repo 'panorama\hit-regions.json'),($hitRegions|ConvertTo-Json -Depth 3),$utf8)
Write-Output '已生成 1~12 扇区的原生轮盘布局与样式。'
