# HanClassicBuyWheel

为 CS2 提供经典 CS:GO 风格购买轮盘的 SwiftlyS2 插件，包含 Panorama UI、武器购买、装备升级和可选刀具购买。

视频演示 : https://www.youtube.com/watch?v=t8f1dbHnWIY&t

- 鼠标点击购买、扇区悬停高亮、中心返回/关闭，可设置下蹲关闭。
- 经典模式按 T/CT 分配手枪、重型、微冲、步枪、装备、投掷物六类，禁用物品保留空位。
- 全武器模式显示双方武器，可独立限制只有 CT 或 T 使用轮盘。
- 武器、装备、刀具价格独立配置；刀具支持免费发放。
- 中文和英语，支持跟随玩家语言、固定语言及玩家自行切换。
- 遵守服务器购买区域、购买时间和手雷携带规则，也可配置放宽区域与时间。

## 安装

需要 SwiftlyS2 **1.4.13** 与 **.NET 10** 运行环境。

1. 将插件复制到服务器 game/csgo/addons/swiftlys2/plugins/ 中。
2. 将发布包中的 `addon` UI 资源加入服务器使用的地图或资源 Workshop addon，并让服务器和连接的玩家均加载该 addon。资源路径须保留为：

   ```text
   panorama/layout/custom_game/hanclassicbuywheel.vxml_c
   panorama/styles/custom_game/hanclassicbuywheel.vcss_c
   ```
   ```
   创意工坊资源包 : 3818232419
   ```

3. 启动服务器或换图加载插件和资源。首次加载自动生成以下配置，位置为 `game/csgo/addons/swiftlys2/configs/plugins/HanClassicBuyWheel/`：

   | 文件 | 内容 |
   | --- | --- |
   | `HanClassicBuyWheel.jsonc` | 命令、队伍、模式、按键、语言、声音和购买条件 |
   | `Weapons.jsonc` | 武器、投掷物、装备的价格、启用状态与轮盘位置 |
   | `Knives.jsonc` | 刀型、启用状态和价格 |

4. 修改配置后，在服务器控制台执行 `sw_buywheel_reload`。

**只安装服务器插件不能显示 UI。** 发布包提供编译资源，服主需通过自己的地图或资源 addon 分发和挂载；插件不自动发布或下载 Workshop 资源。更新 UI 后，玩家需重新加载资源。

## 使用

控制台输入 `sw_buywheel`，或聊天输入 `!buywheel` 打开轮盘。点击分类和物品购买，点击中心返回；在首页点击中心关闭。投掷物购买后保持在当前页面。开启 `CloseOnDuck` 后，下蹲可关闭菜单。

使用原生 B 键：将 `UseNativeBuyKey` 设置为 `true`，玩家保留 `bind b buymenu`。也可直接绑定 `bind b sw_buywheel`。更改 `OpenCommand` 时，直接绑定的命令也需对应更改。

## 主配置

以下为默认的 `HanClassicBuyWheel.jsonc`。JSONC 支持 `//` 注释；修改时保留最外层 `HanClassicBuyWheel`。

```jsonc
{
    "HanClassicBuyWheel": {
        "Enabled": true,
        "AllWeapons": false,
        "AllowedTeam": "any",
        "AllowKnives": false,
        "OpenCommand": "buywheel",
        "UseNativeBuyKey": false,
        "ClassicStyle": true,
        "CloseOnDuck": true,
        "LanguageMode": "Auto",
        "DefaultLanguage": "en",
        "RequireBuyZone": true,
        "RequireBuyTime": true,
        "BlockNativePurchases": true,
        "FreezeMovement": true,
        "SoundsEnabled": true,
        "SoundVolume": 0.75,
        "ClickCooldownMs": 220,
        "MaxGrenades": 3,
        "MaxFlashbangs": 2,
        "RoundStartMoneyEnabled": false,
        "RoundStartMoneyAdd": true,
        "RoundStartMoney": 1000,
        "BannedWeapons": []
    }
}
```

| 字段 | 用途 |
| --- | --- |
| `Enabled` | 插件总开关 |
| `AllWeapons` | `false` 为经典队伍武器；`true` 为双方全武器。物品的 `StrictTeam` 限制仍生效 |
| `AllowedTeam` | `"ct"`、`"t"` 或 `"any"`，控制谁能打开轮盘和购买，与全武器模式独立 |
| `AllowKnives` | 开启后首页增加刀具入口，默认关闭 |
| `OpenCommand` | 呼出命令，不含 `sw_`；例如 `"wheel"` 对应 `sw_wheel` / `!wheel` |
| `UseNativeBuyKey` | 开启后接管原生购买键，默认关闭 |
| `ClassicStyle` | `true` 为黑灰经典外观，`false` 为透明队伍色外观 |
| `CloseOnDuck` | 下蹲关闭本插件的所有菜单层级 |
| `LanguageMode` | `"Auto"` 跟随玩家语言；`"Fixed"` 使用默认语言 |
| `DefaultLanguage` | `"zh-CN"` 或 `"en"`；自动模式下未支持的语言使用英语 |
| `RequireBuyZone` | `true` 要求购买区域；`false` 允许任何地点购买 |
| `RequireBuyTime` | `true` 要求原生购买时间；`false` 放宽购买时间 |
| `BlockNativePurchases` | 阻止获准使用轮盘的队伍通过 `buy` / `autobuy` / `rebuy` 绕过插件价格 |
| `FreezeMovement` | 打开菜单时冻结移动 |
| `SoundsEnabled` / `SoundVolume` | 个人 UI 音效开关及音量，音量范围 `0`～`1` |
| `ClickCooldownMs` | 点击购买冷却，单位毫秒，范围 `50`～`2000` |
| `MaxGrenades` / `MaxFlashbangs` | 插件设置的手雷总数和闪光弹上限，同时受原生携带上限限制 |
| `RoundStartMoneyEnabled` | 开启回合开始发钱，仅作用于允许使用轮盘的队伍 |
| `RoundStartMoneyAdd` | `true` 追加金额；`false` 将余额设置为指定金额 |
| `RoundStartMoney` | 回合开始追加或设置的金额 |
| `BannedWeapons` | 禁用的武器 classname，例如 `["weapon_awp", "weapon_g3sg1"]` |

竞技服保留 `RequireBuyZone=true`、`RequireBuyTime=true`，沿用服务器原有规则。

僵尸服若只允许 CT 在任意地点、任意常规回合时段购买，可在主配置中修改：

```jsonc
"AllWeapons": true,
"AllowedTeam": "ct",
"UseNativeBuyKey": true,
"RequireBuyZone": false,
"RequireBuyTime": false
```

`RequireBuyZone=false` 会设置 `mp_buy_anywhere=1`；`RequireBuyTime=false` 会设置 `mp_buytime=9999` 秒。这两个 cvar 对整个服务器生效，`AllowedTeam` 单独控制轮盘使用资格。重新开启限制、禁用或卸载插件时，会恢复插件接管前的值。

`config/all.jsonc` 是全武器示例，`config/only-ct.jsonc` 是仅 CT 使用全武器轮盘的示例。将所需示例作为主配置使用，武器与刀具仍分别由另外两份配置控制。

## 武器与装备

`Weapons.jsonc` 的结构为 `Weapons.Items` 数组。默认价格采用 CS2 价格，配置生成后由服主自行维护，游戏更新不会覆盖自定义价格。回血针默认关闭。

```jsonc
{
    "Weapons": {
        "Items": [
            {
                "Id": "ak47",
                "Name": "AK-47",
                "NameEnglish": "AK-47",
                "Classname": "weapon_ak47",
                "Category": "rifle",
                "Price": 2700,
                "Teams": [2],
                "Kind": "Weapon",
                "Enabled": true,
                "StrictTeam": false,
                "Order": 0,
                "CarryLimit": 1,
                "Icon": "ak47",
                "ClassicSlotCT": -1,
                "ClassicSlotT": 1,
                "DefinitionIndex": 0
            }
        ]
    }
}
```

上面仅展示一项的格式；使用时修改完整配置中的对应项，保留其他需要出售的物品。

| 字段 | 用途 |
| --- | --- |
| `Id` | 唯一物品标识，不可重复 |
| `Name` / `NameEnglish` | 中文和英语显示名 |
| `Classname` | 武器实体名；护甲、头盔、拆弹器保持原配置 |
| `Category` | `secondary`、`heavy`、`smg`、`rifle`、`equipment`、`grenades` |
| `Price` / `Enabled` | 自定义价格和启用开关；价格 `0` 为免费 |
| `Teams` | `2` 为 T，`3` 为 CT；经典模式按此限制 |
| `StrictTeam` | 开启后全武器模式也遵守 `Teams`，拆弹器默认仅 CT |
| `Kind` | `Weapon`、`Kevlar`、`AssaultSuit`、`Defuser`，保持与物品用途一致 |
| `Order` | 全武器模式的排列顺序，数值越小越靠前 |
| `CarryLimit` | 单种物品携带上限，手雷还需满足服务器原生上限 |
| `Icon` | 已包含在 UI 中的图标名 |
| `ClassicSlotCT` / `ClassicSlotT` | 队伍经典轮盘槽位 `0`～`5`；装备为 `0`～`3`；`-1` 表示只在全武器模式显示 |

同一队伍、同一分类的经典槽位不可重复。禁用物品不会让其他物品挤占其位置。将经典 P2000 换成 USP-S 时，先把 P2000 的 `ClassicSlotCT` 改为 `-1`，再将 USP-S 的该字段改为 `0`；其他替换枪型同理。使用新增图标需重新生成并编译 UI 资源。

购买护甲和头盔会按已有装备计算差价。换枪处理对应武器槽；若发放失败则退还本次款项，已丢出的旧枪可从地面捡回。

手雷数量同时受插件配置、物品 `CarryLimit` 和服务器 `ammo_grenade_limit_total`、`ammo_grenade_limit_flashbang`、`ammo_grenade_limit_default` 限制，实际采用较小值。默认额外限制总数三颗；要跟随服务器更高的原生总数上限，请相应提高 `MaxGrenades`。

## 刀具

设置 `AllowKnives=true` 后显示刀具入口。`Knives.jsonc` 中 `Knives.Items` 包含 22 种刀型，可逐项设置 `Enabled` 和 `Price`，价格 `0` 为免费。购买会替换当前刀具。

刀具使用 `Kind="Knife"`、`Category="knives"`、`Classname="weapon_knife"`。`DefinitionIndex` 决定刀型，例如爪子刀为 `507`、蝴蝶刀为 `515`。建议保留提供的刀型编号、图标和标识，只修改价格、名称、顺序及启用状态。

## 命令与语言

| 控制台命令 | 操作 |
| --- | --- |
| `sw_buywheel` | 打开或关闭；由 `OpenCommand` 改名 |
| `sw_buywheel_close` | 关闭自己的菜单并释放光标 |
| `sw_buywheel_back` | 返回上一层；首页则关闭 |
| `sw_buywheel_next` / `sw_buywheel_prev` | 翻页，每页最多 12 项 |
| `sw_buywheel_wasd` | 打开框架提供的备用购买菜单 |
| `sw_buywheel_language en` / `zh-CN` / `auto` | 设置个人语言，也可通过 UI 切换 |
| `sw_buywheel_reload` | 重新读取三份配置及翻译；配置错误时继续使用之前的有效配置 |
| `sw_buywheel_status` | 查看插件版本与配置状态 |

`sw_buywheel_reload` 可在服务器控制台执行，游戏内执行需要 `hanclassicbuywheel.admin` 权限。除呼出命令外，其余命令名称固定。

翻译文件位于插件目录的 `resources/translations/en.jsonc` 和 `zh-CN.jsonc`。选择和购买失败音效只播放给操作玩家；可通过 `SoundsEnabled` 关闭。

