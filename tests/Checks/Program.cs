using HanClassicBuyWheel.Domain;
using System.Xml.Linq;
using System.Text.Json;

string repo = args.Single();
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
WheelConfig Read(string file) => WheelConfig.Parse(File.ReadAllText(Path.Combine(repo, "config", file)), File.ReadAllText(Path.Combine(repo, "config/Weapons.jsonc")), File.ReadAllText(Path.Combine(repo, "config/Knives.jsonc")));
var config = Read("HanClassicBuyWheel.jsonc");
var classic = new BuyCatalog(config);
var ak = classic.Items["ak47"];
Check(classic.AllowsItem(ak, 2) && !classic.AllowsItem(ak, 3), "经典模式按阵营限制 AK");
Check(classic.AllowsItem(classic.Items["m4a1"], 3) && !classic.AllowsItem(classic.Items["m4a1"], 2), "经典模式按阵营限制 M4");
var all = new BuyCatalog(config with { AllWeapons = true });
Check(all.AllowsItem(ak, 3) && all.AllowsItem(all.Items["m4a1"], 2), "全武器模式双方可买另一阵营枪械");
Check(!all.AllowsItem(all.Items["defuser"], 2), "全武器模式保留拆弹器强制 CT 限制");
var only = new BuyCatalog(config with { AllowedTeam = AllowedTeam.ct, AllWeapons = true });
Check(!only.AllowsWheel(2) && only.AllowsItem(ak, 3), "Only 模式只允许 CT 并可用全武器");
var onlyClassic = new BuyCatalog(config with { AllowedTeam = AllowedTeam.t });
Check(onlyClassic.AllowsItem(ak, 2) && !onlyClassic.AllowsWheel(3) && !onlyClassic.AllowsItem(onlyClassic.Items["m4a1"], 2), "Only 模式允许 T 并可用经典规则");
var banned = new BuyCatalog(config with { BannedWeapons = ["weapon_ak47"] });
Check(!banned.AllowsItem(banned.Items["ak47"], 2), "服务端禁用列表生效");
var disabled = new BuyCatalog(config with { Items = config.Items.Select(i => i.Id == "ak47" ? i with { Enabled = false } : i).ToArray() });
Check(!disabled.AllowsItem(disabled.Items["ak47"], 2), "单物品禁用生效");
Check(!classic.AllowsWheel(0) && !classic.AllowsWheel(1), "旁观者不可用");
var nav = new WheelNavigation(classic.BuildTree(2));
Check(nav.Visible.Select(n => n.Id).SequenceEqual(new[] {"secondary","heavy","smg","rifle","equipment","grenades"}), "原版六分类顺序");
nav.Activate(3);
Check(nav.Current.Id == "rifle" && nav.Depth == 1 && nav.Visible.Select(i => i.Item!.Id).SequenceEqual(new[] {"galilar","ak47","ssg08","sg556","awp","g3sg1"}), "T 步枪固定六槽位含狙击枪");
nav.Back(); Check(nav.Current.Id == "root", "返回父分类");
nav.Home(); Check(nav.Current.Id == "root" && nav.Depth == 0, "购买成功返回根节点");
var single = new WheelNavigation(new("root", "root", [new("only", "only", [new("leaf", "AK", [], ak)])]));
Check(single.Activate(0) == ak && single.Depth == 0, "单项目分类直接购买");
var many = new WheelNavigation(new("root", "root", Enumerable.Range(0, 26).Select(i => new MenuNode("i"+i, "n"+i, [], ak)).ToArray()));
many.ChangePage(100); Check(many.Page == 2 && many.Visible.Length == 2 && many.Activate(5) is null, "超 12 项分页，越界点击不购买");
int balance = 3000, grants = 0;
Check(PurchaseTransaction.Execute(2700, () => balance, v => balance = v, () => { grants++; return true; }) && balance == 300 && grants == 1, "成功只扣一次款");
Check(!PurchaseTransaction.Execute(2700, () => balance, v => balance = v, () => { grants++; return true; }) && balance == 300 && grants == 1, "资金不足不发放不扣款");
balance = 3000;
Check(!PurchaseTransaction.Execute(2700, () => balance, v => balance = v, () => false) && balance == 3000, "发放返回失败全额退款");
try { PurchaseTransaction.Execute(2700, () => balance, v => balance = v, () => throw new InvalidOperationException()); } catch (InvalidOperationException) { }
Check(balance == 3000, "发放异常全额退款");
var ids = XDocument.Load(Path.Combine(repo, "panorama/layout/custom_game/hanclassicbuywheel.vxml")).Descendants().Attributes("id").Select(a => a.Value).ToArray();
Check(ids.Distinct().Count() == ids.Length && Enumerable.Range(0, 12).All(i => ids.Contains("hcw_choice_" + i)), "布局 ID 唯一且覆盖 12 个扇区");
foreach (var file in new[] { "HanClassicBuyWheel.jsonc", "all.jsonc", "only-ct.jsonc" }) Check(Read(file).Items.Length == 67, "独立配置有效：" + file);
string legacy = Path.Combine(repo, "config/examples/legacy-ct");
var legacyConfig = WheelConfig.Parse(File.ReadAllText(Path.Combine(legacy, "HanClassicBuyWheel.jsonc")), File.ReadAllText(Path.Combine(legacy, "Weapons.jsonc")), File.ReadAllText(Path.Combine(legacy, "Knives.jsonc")));
Check(legacyConfig.AllWeapons && legacyConfig.AllowedTeam == AllowedTeam.ct && legacyConfig.Items.Single(i => i.Id == "hegrenade").Price == 1000, "旧服独立配置示例与旧手雷价格");
bool rejected = false;
try { (config with { Items = [ak, ak] }).Validate(); } catch (InvalidDataException) { rejected = true; }
Check(rejected, "重复 ID 配置拒绝");
var texts = WheelTranslations.Load(Path.Combine(repo, "src/HanClassicBuyWheel/resources/translations"));
Check(texts.Get("en", "category.grenades") == "GRENADES" && texts.Get("zh-CN", "category.grenades") == "投掷物", "中英分类翻译");
Check(texts.Get("en", "purchase.success", "AK-47", 2700).Contains("AK-47 · $2700"), "购买文案占位符");
Check(WheelTranslations.ResolveLanguage(config, "zh-TW", null) == "zh-CN" && WheelTranslations.ResolveLanguage(config, "fr", null) == "en", "自动语言及英文兜底");
Check(WheelTranslations.ResolveLanguage(config with { LanguageMode = LocalizationMode.Fixed, DefaultLanguage = "en" }, "zh-CN", null) == "en"
    && WheelTranslations.ResolveLanguage(config, "zh-CN", "en") == "en", "固定语言与玩家覆盖");
Check(WheelTranslations.ItemName(classic.Items["hegrenade"], "en") == "HE Grenade", "物品英文名称");
var ctTree = classic.BuildTree(3);
Check(ctTree.Children[0].Children.Length == 6 && ctTree.Children[0].Children[5].IsBlank && ctTree.Children[0].Children[0].Item!.Id == "hkp2000", "CT 手枪固定顺序且末槽留空");
Check(classic.BuildTree(2).Children[4].Children.Length == 4 && classic.BuildTree(2).Children[4].Children[3].IsBlank, "T 装备保留四槽位与拆弹器空位");
Check(disabled.BuildTree(2).Children[3].Children[1].IsBlank && disabled.BuildTree(2).Children[3].Children[2].Item!.Id == "ssg08", "禁用枪留空且后续槽位不移动");
Check(all.BuildTree(3).Children[0].Children.Any(n => n.Item!.Id == "glock") && all.BuildTree(3).Children[0].Children.Any(n => n.Item!.Id == "usp_silencer"), "全武器显示另一队武器和替换武器");
Check(!classic.AllowsItem(classic.Items["knife_515"], 3) && new BuyCatalog(config with { AllowKnives = true }).BuildTree(3).Children.Length == 7, "刀默认关闭且开启时添加第七入口");
Check(config.MaxGrenades == 3 && config.Items.Where(i => i.Kind == ItemKind.Knife).All(i => i.Price == 0 && i.DefinitionIndex > 0), "三颗手雷默认与免费刀型定义");
balance = 10; Check(PurchaseTransaction.Execute(0, () => balance, v => balance = v, () => true) && balance == 10, "免费购买无需扣款");
rejected = false;
try { _ = new BuyCatalog(config with { Items = config.Items.Select(i => i.Id == "usp_silencer" ? i with { ClassicSlotCT = 0 } : i).ToArray() }); } catch (InvalidDataException) { rejected = true; }
Check(rejected, "重复经典槽位拒绝加载");
using var regions = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "panorama/hit-regions.json")));
var hitRegions = regions.RootElement.EnumerateArray().ToArray();
Check(hitRegions.Length == 91, "覆盖一至十二槽与六/七分类的点击区");
bool insideWedges = hitRegions.All(region =>
{
    double cx = region.GetProperty("CenterX").GetDouble() - 340;
    double cy = region.GetProperty("CenterY").GetDouble() - 340;
    double angle = region.GetProperty("Angle").GetDouble();
    int count = region.GetProperty("Count").GetInt32();
    // CSS 输出保留两位小数；验证实际输出尺寸，不只验证生成器浮点结果。
    double width = Math.Round(region.GetProperty("Width").GetDouble(), 2);
    double height = Math.Round(region.GetProperty("Height").GetDouble(), 2);
    return width > 0 && height > 0 && new[] { -1, 1 }.All(sx => new[] { -1, 1 }.All(sy =>
    {
        double x = cx + sx * width / 2, y = cy + sy * height / 2;
        double projection = x * Math.Cos(angle) + y * Math.Sin(angle);
        double cross = y * Math.Cos(angle) - x * Math.Sin(angle);
        return x * x + y * y <= 329 * 329 && projection >= 89
            && (count == 1 || Math.Abs(Math.Atan2(cross, projection)) <= (180.0 / count - 1.9) * Math.PI / 180);
    }));
});
Check(insideWedges, "所有点击区四角严格位于对应扇区、圆周内且避开中心按钮");
string css = File.ReadAllText(Path.Combine(repo, "panorama/styles/custom_game/hanclassicbuywheel.vcss"));
Check(css.Contains(".HitTarget:hover { background-color: transparent; border: 0px solid transparent; }")
    && css.Contains(".classic .Slot:hover .Wedge"), "按钮方框隐藏，仅扇区高亮");
Console.WriteLine($"{checks} checks passed");
