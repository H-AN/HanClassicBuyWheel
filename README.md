# HanClassicBuyWheel

全新、独立的 SwiftlyS2 购买轮盘插件，包含服务器购买系统与真实 CS2 Panorama UI。旧 Han_Weapon_Manager 只作参考，本插件不依赖旧插件。

默认严格按原版六分类排列：手枪、重型、微冲、步枪、装备、投掷物。经典队伍模式保持固定槽位，缺失物品留空；全武器模式显示双方武器及替换枪型。只使用鼠标，隐藏按钮方框，悬停时高亮扇区。中心返回/关闭，下蹲关闭可配置，中英双语。

## 安装

需要 SwiftlyS2 1.4.13 与 .NET 10。将发布包 `server/plugins/HanClassicBuyWheel` 复制到服务器 `game/csgo/addons/swiftlys2/plugins`。首次加载在 `configs/plugins/HanClassicBuyWheel` 生成：

| 文件 | 内容 |
| --- | --- |
| HanClassicBuyWheel.jsonc | 命令、模式、允许队伍、按键、语言、声音、购买条件 |
| Weapons.jsonc | 45 项武器/手雷/装备的自定义价格与经典槽位 |
| Knives.jsonc | 22 种刀的实体定义编号与价格，0 为免费 |

**服务器和客户端均需加载 UI 资源。** 发布包 `addon` 包含编译好的两个 `_c` 文件。本地测试将其中 `panorama` 复制到服务端与客户端各自的 `game/csgo/panorama`，随后重启客户端。公共服务器需要地图/Workshop addon 挂载并下载资源；只安装 DLL 不能显示轮盘。项目未发布 Workshop，也未预设 Workshop ID。

控制台 `sw_buywheel` 或聊天 `!buywheel` 打开。设置 `UseNativeBuyKey=true` 后可保留原生 `bind b buymenu`，插件通过 `open_buymenu` 接管、关闭原生页面并打开轮盘，已用真实客户端验证。从 1.0.1 起，购买区域/时间开关会同步引擎规则：关闭限制后原生 B 也能随时随地打开，无需另写服务器 cvar。也可自行绑定 `bind b sw_buywheel`。

## 配置

编辑首次生成的完整文件。以下是主要字段：

| 主配置字段 | 用途 |
| --- | --- |
| AllWeapons | false 为经典队伍模式，true 为双方全武器；StrictTeam 特殊物品仍保留限制 |
| AllowedTeam | ct / t / any，与全武器开关独立，可禁止丧尸队伍打开和购买 |
| AllowKnives | 默认 false；开启后根轮盘增加第七个刀入口 |
| OpenCommand | 不含 sw_，例如 wheel 对应 sw_wheel / !wheel |
| UseNativeBuyKey | 接管原生 B，默认 false，由服主选择开启 |
| ClassicStyle | 默认黑灰原版风格；false 切回透明队伍色风格 |
| CloseOnDuck | 默认 true，下蹲关闭本插件全部层级与备用菜单 |
| LanguageMode / DefaultLanguage | Auto 按玩家语言，中文 zh-CN、其余 en；Fixed 使用指定默认语言 |
| RequireBuyZone / RequireBuyTime | true 复用原生买区/买时；false 开启任何地点购买/无限购买时间，默认 true |
| BlockNativePurchases | 阻止获准队伍用 buy/autobuy/rebuy 绕过自定义价格 |
| FreezeMovement | 页面打开时冻结移动，光标捕获负责隔离鼠标与视角 |
| SoundsEnabled / SoundVolume | 当前玩家的官方 UI 音效，音量 0~1 |
| ClickCooldownMs | 防连买，默认 220 毫秒 |
| MaxGrenades / MaxFlashbangs | 配置额外上限，实际同时受 CS2 原生上限限制 |
| BannedWeapons | 按 classname 禁用；也可在物品配置设 Enabled=false |
| RoundStartMoneyEnabled | 默认 false；可按 RoundStartMoneyAdd / RoundStartMoney 补钱 |


僵尸服可使用：

```jsonc
"UseNativeBuyKey": true,
"RequireBuyZone": false,
"RequireBuyTime": false
```

这会同步 `mp_buy_anywhere=1` 与`mp_buytime=9999`（秒），并解除已经结束的买时状态，支持运行超过45秒后再打开。两个引擎 cvar 对整个服务器生效；AllowedTeam 仍独立限制谁能使用轮盘。开启限制或卸载时恢复本插件接管前的值；地图开始、回合开始及配置重载时按开关设置，并明确复制给客户端；新加入玩家也会同步。配置 Enabled=false 也会释放接管。竞技服两项保持 true 即沿用原设置。

闪光上限实时读取 `ammo_grenade_limit_flashbang`，其他手雷读取 `ammo_grenade_limit_default`，总数读取 `ammo_grenade_limit_total`。实际取配置上限与原生上限较小值，跟随休闲/竞技及服务器修改后的规则，不强行增加原生不允许的数量。默认额外限制总数三颗；想完全跟随原生总数，将 MaxGrenades 设为大于等于原生上限的值。成功或失败均留在投掷物页面，上限提示“你无法携带更多”。

`config/all.jsonc`、`only-ct.jsonc` 是主配置示例，配合独立的武器/刀配置。旧服参考示例在 `config/examples/legacy-ct`。默认武器价格于 2026-10-12 通过本地 CS2 的 WeaponBaseVData.Price 核对；游戏更新后不会自动覆盖用户价格。回血针默认关闭。

Weapons 每项可设 Price、Enabled、Teams、CarryLimit、Order、Name、NameEnglish、Icon。ClassicSlotCT/T 为 0~5（装备 0~3），-1 仅在全武器模式显示；同队同类槽位不可重复。禁用项保持空位。USP-S、M4A1-S、CZ75、R8、MP5-SD 默认只在全武器模式；要替换经典枪，先移出原项再给替换项相同槽位。新增图标须重新生成并编译 UI。

价格与资格在服务器购买时复查，扣款失败不发放，发放失败/异常退款。装备支持护甲/头盔差价；换枪只处理对应枪槽，不丢整组手雷。若释放旧枪槽后引擎仍拒绝新枪，退款，旧枪留在地面可捡回。

刀按 Knives 的 DefinitionIndex 新建 weapon_knife，使用 ChangeSubclass 设置刀型，免费/收费均走同一交易。爪刀 507、蝴蝶刀 515 已确认实体编号和客户端模型。刀默认关闭，开启后分页选择。

## 命令、语言与音效

| 默认命令 | 操作 |
| --- | --- |
| sw_buywheel | 打开/关闭；可由 OpenCommand 改名 |
| sw_buywheel_close | 紧急关闭自己的 UI 和备用菜单，释放光标 |
| sw_buywheel_back | 返回，根分类则关闭 |
| sw_buywheel_next / sw_buywheel_prev | 每页最多 12 项，翻页 |
| sw_buywheel_wasd | 相同购买服务的框架备用菜单 |
| sw_buywheel_language en / zh-CN / auto | 当前玩家语言；底部也能切换 |
| sw_buywheel_reload | 原子重读三份配置和翻译；错误保留有效快照 |
| sw_buywheel_status | 版本、配置状态、当前轮盘数 |

服务端控制台可重载；游戏内需 hanclassicbuywheel.admin 权限。翻译在插件 resources/translations/en.jsonc、zh-CN.jsonc。选择播放 UIPanorama.submenu_select，资金不足/已拥有/受限等失败播放 Vote.Failed，仅发给当前玩家。预缓存加入 soundevents/game_sounds_ui.vsndevts；首次热加载后换图可使新预缓存生效。

## 构建与备份

```powershell
dotnet --version # 10.x
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-panorama.ps1 -ToolsRoot 'CS2 Workshop Tools 根目录'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1
```

生成器 generate-panorama.ps1 输出布局/CSS 与 91 个点击区的几何记录，矩形四角均在对应扇区和圆周内且避开中心按钮，背景与边框透明。generate-defaults.ps1 -Force 重建仓库模板，不要用于覆盖用户配置。

改版前完整备份在 artifacts/backups/mouse-baseline-20261012-043453。快速退回透明风格设 ClassicStyle=false。完整恢复用 scripts/restore-baseline.ps1 -BackupPath '备份目录'，先保存当前版本再还原，见 [回滚说明](docs/rollback.md)。

方法级步骤见 [实施计划](docs/implementation-plan.md)，实际通过与未覆盖项见 [验证记录](docs/validation.md)。测试脚本与临时探针在 scripts/test、tests/ServerProbe；发布包不含探针、框架 SDK、旧插件、CSGO 参考资源和私人测试数据。