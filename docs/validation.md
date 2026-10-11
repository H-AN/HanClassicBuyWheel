# 验证记录

日期：2026-10-12，Windows，本地 CS2 专服与真实 Steam 客户端，SwiftlyS2.CS2 1.4.13、.NET 10。测试使用临时隔离框架配置，只加载新插件和临时 ClassicWheelProbe；旧插件源码与目录未修改。证据在本机忽略目录 `.local/run-20261011-154650-330/evidence`，实体/库存快照归档于同目录；不随发布包公开私人服务器日志。

## 构建与配置

- Release 构建成功、零警告；43 项检查通过：队伍/全武器/禁用规则、原版槽位留空、分页、购买与失败退款、免费交易、中英翻译、三配置与旧服示例、布局 ID、91 个矩形点击区的四角边界。
- CS2 resourcecompiler 实际编译成功：两个资源，零失败。图标使用游戏内置 SVG 的 CSS background-size:contain，避免拉伸。
- 本地引擎导出的 42 种武器/投掷物/特殊物品价格全部与默认配置一致；额外护甲 650、全甲 1000、拆弹器 400。确认 Bizon 1300、MP7/MP5-SD 1400、FAMAS 1950、M4A4 2900、燃烧弹 500。旧配置价格仅在示例中保留。
- 发布包白名单只带新插件、翻译/配置模板、UI 编译产物、配置示例与文档；不包含框架 SDK DLL、旧插件、参考 pbin/源码、测试探针。

## 真实客户端与经济

| 场景 | 实际结果与证据 |
| --- | --- |
| 鼠标 UI | 可点击分类和枪械，原生图标保持比例；cursor-angle-before/after 快照确认光标移动不改变视角 |
| 下蹲关闭 | CloseOnDuck=true 后 HUD 清零；false 时 HUD 保留；cursor-duck、duck-disabled |
| 最终版下蹲 | final-duck-close.json 确认所有 HUD 清零；当前版本真实客户端通过 |
| 原生 B | 保留 bind b buymenu，接管 open_buymenu；原生页面关闭，自己的 HUD=1、捕获输入=true、BuyMenuOpen=false；native-relay-b.json / native-relay-b-result.png |
| 经典与英语 | 六分类原版顺序，英文标题/分类/原因/署名；hidden-buttons-root-en.png、competitive-final-root.png |
| 最终版与全武器 | 独立描边使圆心/扇区/点击区使用同一680坐标；final-rim-root-zh.png；全武器开关已实际重载为 All，完整目录由规则检查覆盖 |
| 允许队伍 | AllowedTeam=ct 时 T 的呼出被拒绝，HUD=0；only-ct-denies-t.json |
| 隐藏按钮 | 悬停只显示整个扇区高亮，图标区域无方框；hidden-buttons-flash-denied-en.png；一至十二项及根六/七项的点击区四角通过几何检查 |
| 手雷休闲限制 | 原生 ammo_grenade_limit_flashbang=1，第一颗扣 200，第二颗提示无法携带更多且余额不变；hidden-buttons-flash-denied-en.png |
| 最终版休闲限制 | final-casual-flash-one/two-denied.json 均保持15800，弹药1；final-casual-flash-denied-zh.png |
| 手雷竞技限制 | 切换 game_mode=1 并换图，原生闪光上限 2：第一颗钱 15800/弹药1，第二颗 15600/弹药2，第三颗拒绝且保持 15600；comp-final-flash-one/two/three-denied.json |
| 三颗总上限 | 两闪加 HE 后钱 15300，总数3；再买烟雾拒绝，钱和库存不变；comp-final-third-he / comp-final-fourth-smoke-denied.json、comp-final-grenade-limit.png |
| 投掷物持续购买 | 所有上述成功和拒绝均保持投掷物页面，不返回首页 |
| 枪械购买 | AK-47 扣 2700，沙鹰扣 700，真实库存匹配；ak-purchased 等早期鼠标基线快照 |
| 刀实体 | 爪刀507、蝴蝶刀515免费发放，实际客户端显示蝴蝶刀；knife-karambit/knife-butterfly.json、butterfly-visible.png |
| 刀收费与重复 | 暂设蝴蝶刀500，16000→15500且定义515；再次购买提示已拥有，15500不变；paid-butterfly-verified / same-knife-denied.json |
| 刀分页 | 两页共22种图标正常；hidden-buttons-knives-first/second.png |
| 关闭事件 | 中心返回再关闭，HUD=0；探针关闭事件没有原先 NullReferenceException；closed-no-null-error.json、close-event-checked.txt |
| 换图与重载 | 实际切换地图并重新打开；三文件重载关闭旧页面、应用新快照，刀开关及价格生效 |

竞技第二颗闪光弹由引擎给现有实体加弹药，GiveItem 可不返回新实体。实测发现旧判定会把成功发放误判失败退款，已改为库存数量增长判定并重新通过两次各扣200的测试；没有手动增加弹药或突破原生上限。

用户报告的 NullReferenceException 来自临时探针关闭后继续解引用已失效 HUD。探针已先判断 CustomHudLayout 是否有效，新插件也校验所有者与完整 HUD 句柄；探针不属于正式插件交付。

## 未覆盖项与分发范围

尚未用两名真实玩家同时测试隔离；单人加机器人不视为多人通过。尚未逐一手持确认全部22种刀模型，已确认507/508/515及全部图标。官方声音预缓存与私人收件人实现已编译、换图/点击无插件声音异常，但没有录音或第二客户端听音证据。护甲各种受损状态、全部枪型、长期地图轮换、第三方菜单共存仍建议服主在实际模式验收。

本地散文件 UI 与 Workshop 下载是两种部署路径。项目已交付可编译源码与完整本地 UI addon，尚未公开发布 Workshop，也未用完全无散文件的新客户端验证 Workshop 分发。需服务器选用实际 Workshop/地图资源挂载方案后再测，不填写虚假 Workshop ID。

原鼠标版本完整备份保留于 `artifacts/backups/mouse-baseline-20261012-043453`，回滚步骤见 rollback.md。测试结束恢复框架原 core.jsonc，并移出临时探针；保留新插件、可编辑配置与本地专用 UI 资源供后续测试。

回滚脚本另在 `.local/rollback-check-*` 隔离副本实际执行，确认当前目录归档、旧版恢复且没有新源文件残留。没有对真实项目执行回滚。最终安装的本机主配置为经典/any/刀关闭、原生 B 开启、买区/买时开启；公开包默认原生 B 关闭，可自行开启。


## 1.0.1 本轮修复验收（2026-10-12）

- 接缝：真实客户端 seam-native-b-current.png 确认 #484848 实线与外圈同色，透明缺口已消除；一至十二项均生成实线。
- 图标：从本机 CS2 pak01_dir.vpk 确认 kevlar.vsvg_c、assaultsuit.vsvg_c、defuser.vsvg_c 均存在，源默认配置、模板、旧服示例及现有服务器空图标项已补齐，UI 实际重新编译并部署。
- 限制恢复：native-strict-restored.json 中玩家 InBuyZone=false、回合99秒、BuyTimeEnded=true，cvar 恢复为0/45，HUD=0。
- 原生 B 命令：native-9999-outside.json 中玩家 InBuyZone=false、回合244秒、存活CT，mp_buy_anywhere=1、mp_buytime=9999，原生 buymenu 命令成功接管为自己的HUD=1、Capture=true、原生BuyMenuOpen=false。
- 同步：实测发现当前SDK的普通setter未可靠更新客户端原生cvar，补充显式 ReplicateToAll，以及出生就绪后新会话 ReplicateToClient；配置重载/地图/回合均按false开关应用。
- 当前部署保留服主的 AllWeapons=true、AllowKnives=true、两限制false、音量1.0、回合加10000。测试已在原有其它插件同时加载的服务器执行，未修改旧插件源码/文件。
- 临时 ClassicWheelProbe 已卸载并移出 plugins，正式源码没有 CWB/cwbprobe/Console.Write/LogDebug/LogTrace，正式包不含探针。现有服务器继续运行供服主测试。

最终无探针验证：卸载探针后使用真实 B 按键并点击装备分类，final-equipment-icons-no-probe.png 明确显示防弹衣、头盔+背心、拆弹器三个官方图标，以及同色灰色接缝。玩家站在中路，原生 B 正常接管；此期间检查控制台没有 CWB 输出。
