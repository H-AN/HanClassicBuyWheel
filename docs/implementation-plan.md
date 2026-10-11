# HanClassicBuyWheel 实施计划

更新：2026-10-12。以用户后续确认的六分类、鼠标 UI、CS2 价格、独立配置、可选刀型为最终需求。新建独立插件，不修改或依赖旧 Han_Weapon_Manager。

## 参考定位

| 来源 | 定位与用途 |
| --- | --- |
| CSGO 源码 | game/client/cstrike15/panorama/csgo_buymenu.cpp/.h：CreateBuyWheels、RefreshItems、EventPurchaseSlot、NavigateBack、Open/CloseBuyMenu；原版六分类及购买交互 |
| 原版输入 | csgo_radial_selector.cpp：CapturedMouseMove/ButtonUp，客户端 C++ 控件，不能直接搬进服务器插件 |
| code.pbin | PAN1 + ZIP Store，定向提取 buymenu.xml/css/js 与 keybinds；SHA256 8A62B1CD4EA2FBB62B56A842E25142E8ADB9AEF908DF6B4DA01D8400096F7BCE |
| 位置图片 | 用户提供的 13 张完整层级图，确定类别顺序、空位和黑灰外观 |
| 旧武器插件 | classname、经济/发枪 API 参考；新插件重写购买交易和菜单 |
| HanZombiePlagueS2 | HZP.Helpers.cs 的 GiveItem + ChangeSubclass 刀型发放方法 |
| ShopCore | 当前版本 OpenBuyMenu 原始 buy 命令注册；未找到原生 B 接管实现，实际接管由本插件实测确定 |
| SwiftlyS2-Toolkit | SKILL、Custom HUD、typed GameHooks、生命周期、多语言、声音指南 |
| 通用文档 | CS2-Panorama-UI 与 CS2-本地自动测试，编译资源、真实输入、备份和测试证据 |

参考 CSGO 资源仅放本地忽略目录，不随发布包分发。

## 最终页面与配置

根轮盘固定顺序：手枪、重型、微冲、步枪、装备、投掷物。经典六槽从左上开始顺时针，装备四槽为上/右/下/左。少的物品保留空位，不挤压其他位置。

| 经典分类 | CT 顺序 | T 顺序 |
| --- | --- | --- |
| 手枪 | P2000 / 双枪 / P250 / Five-SeveN / 沙鹰 / 空 | Glock / 双枪 / P250 / Tec-9 / 沙鹰 / 空 |
| 重型 | Nova / XM1014 / MAG-7 / M249 / Negev / 空 | Nova / XM1014 / Sawed-Off / M249 / Negev / 空 |
| 微冲 | MP9 / MP7 / UMP45 / P90 / Bizon / 空 | MAC10 / MP7 / UMP45 / P90 / Bizon / 空 |
| 步枪 | FAMAS / M4A4 / SSG08 / AUG / AWP / SCAR20 | Galil / AK47 / SSG08 / SG553 / AWP / G3SG1 |
| 装备 | 护甲 / 全甲 / 电击枪 / 拆弹器 | 护甲 / 全甲 / 电击枪 / 空 |
| 投掷物 | 燃烧弹 / 诱饵 / 闪光 / HE / 烟雾 / 空 | 燃烧瓶 / 诱饵 / 闪光 / HE / 烟雾 / 空 |

AllWeapons=true 时每类加入双方物品和替换枪型，最多十二项一页。AllowKnives=true 时增加第七个根入口与刀型分页；关闭保持上述六项。拆弹器的 StrictTeam 限制保留。

主配置 HanClassicBuyWheel.jsonc 与 Weapons.jsonc、Knives.jsonc 分开，AllowedTeam=ct/t/any 独立于全武器开关。OpenCommand 可配置，UseNativeBuyKey 可接管原生 B。价格默认 CS2 当前 VData.Price 核对值，用户自由调整；刀价 0 免费。

只保留鼠标 UI，不保留视角选择。光标捕获期间鼠标不转游戏视角。按钮背景/边框透明，仅扇区悬停高亮；矩形点击范围逐个计算，使四角位于对应扇区、圆周内并避开中心。子菜单视觉卡片保留 144×120，根卡片 190×160，实际点击区根据角度收缩。中心仅返回/关闭，底部关闭右侧署名跟随队伍色。

购买成功回根菜单，投掷物始终留在当前分类；失败显示本地化原因。默认总手雷三颗，上限可改；闪光/其他手雷/总数分别与当前引擎 ammo_grenade_limit_flashbang/default/total 取最小值。竞技第二闪通过引擎增加现有弹药，成功依据库存数量增长，不强求新实体返回，不手动突破原生上限。

中英完整翻译支持玩家自动识别、服务器固定与手动覆盖。CloseOnDuck 默认 true，关闭全部本插件层级及备用菜单。预缓存官方声音文件，选择 UIPanorama.submenu_select，失败 Vote.Failed，SoundEvent 收件人仅此玩家。

## 方法级实施步骤

| 步骤 | 实现文件/入口 | 完成标准 |
| --- | --- | --- |
| 1 | extract-reference.ps1 | 解析 PAN/ZIP 边界并提取四个参考资源 |
| 2 | Domain/WheelConfig.Parse/Validate | 三文件严格校验、重复 ID/经典槽拒绝、合法价格与队伍 |
| 3 | Domain/BuyCatalog.BuildTree/AllowsItem | 原版固定槽与留空、全武器、允许队伍、可选刀 |
| 4 | Domain/WheelNavigation | 返回、首页、分页与越界检查 |
| 5 | Services/BuyService.Quote/Purchase/Grant | 实时资格/金额/库存校验、扣款与失败退款、枪槽替换、护甲差价、原生手雷限制 |
| 6 | Services/BuyService.GrantKnife | 新刀实体、ChangeSubclass、定义编号同步、清理旧刀、同刀不重复购买 |
| 7 | UI/WheelHud | 所有者 HUD、图标比例、槽位、语言、状态、光标释放与点击路由 |
| 8 | Input/WheelInput | typed RunCommand 处理下蹲和移动冻结，临时 wrapper 不逃逸 |
| 9 | UI/WasdMenu、UiSounds | 同购买服务备用菜单与私人官方声音，异常安全清理 |
| 10 | Plugin 配置/生命周期 | 后台读取、游戏线程原子替换、动态呼出命令、原生 B 双 Tick 接管、地图/断线/卸载回收 |
| 11 | generate-panorama/build-panorama/build | 91 个几何点击区、真实 CS2 资源编译、锁定 SDK 1.4.13、Release 规则检查 |
| 12 | 本地专服和 Steam 客户端 | 六分类、隐藏按钮、中英、原生 B、扣款发枪、刀型、手雷原生限额实际验证 |
| 13 | 文档、回滚、交付 | 原鼠标版本完整备份、白名单发布包，不带探针/SDK/参考资源/私人证据 |

## 线程与生命周期

游戏线程处理经济、实体、HUD、声音和导航；后台仅读取/解析配置，回写校验请求代次与卸载状态。按 SessionId 存运行状态，同时核对 slot/session/SteamID，HUD 与 pawn 使用完整句柄，配置和地图代次用于丢弃迟到回调。临时 usercmd wrapper 不保存到延迟任务。HUD 收件人只为本人。

刷新周期 0.25 秒，文本/class 差分更新；输入 Hook 仅在本插件页面存在或需等待按键释放时启用。死亡/队伍变化/pawn 变化/购买条件失效关闭，地图/卸载/断线清理自有 HUD、任务、Hook 与备用菜单，不干扰其他菜单。

原生 B 保留玩家 buymenu 绑定，拦截服务端 open_buymenu，下一 Tick 让客户端执行 buymenu 关闭原生页面，再下一 Tick 打开本插件；需符合原生 B 请求的买区/买时门槛。原生 buy/autobuy/rebuy 可按获准队伍阻止。

## 验证与回滚

Release 零警告、四十一项规则/交易/语言/结构/几何检查，两个 Panorama 文件实际编译。真实客户端证据与尚未覆盖的多人和 Workshop 分发见 docs/validation.md，不把单人散文件测试等同于公开分发完成。

完整原版本备份：artifacts/backups/mouse-baseline-20261012-043453。ClassicStyle=false 可保留新功能切回透明外观；完整源代码/产物回滚使用 scripts/restore-baseline.ps1，先归档现版本。正式包仅包含新插件、资源、三配置与示例、文档及源码 UI，不包含旧插件或测试探针。

## 1.0.1 修复（2026-10-12）

RequireBuyZone=false 才应用 mp_buy_anywhere=1，RequireBuyTime=false 才应用 mp_buytime=9999 秒；true 沿用原服务器规则。配置加载/重载、地图开始、回合开始同步设置，显式复制到客户端，新玩家在出生就绪后同步。重新开启限制或卸载只恢复本插件持有的原值。扇区取消透明间隔，使用外圈同色 #484848 实线；补齐 kevlar/assaultsuit/defuser 官方 SVG 图标。正式插件不包含 CWB 测试命令或日志，临时探针从部署目录移出。
