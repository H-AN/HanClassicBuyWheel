using HanClassicBuyWheel.Domain;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanClassicBuyWheel.Services;

public sealed class BuyService(ISwiftlyCore core, bool allowBots = false)
{
    public static int Money(IPlayer player) => player.Controller.InGameMoneyServices?.Account ?? 0;
    public static void WriteMoney(IPlayer player, int amount)
    {
        var money = player.Controller.InGameMoneyServices ?? throw new InvalidOperationException("玩家经济服务无效");
        money.Account = amount;
        money.AccountUpdated();
        player.Controller.InGameMoneyServicesUpdated();
    }
    public PurchaseFailure CheckPlayer(IPlayer player, BuyCatalog catalog)
    {
        if (!player.IsValid || player.IsFakeClient && !allowBots || player.PlayerPawn is not { IsValid: true } pawn
            || player.Controller is not { IsValid: true } controller || controller.InGameMoneyServices is not { IsValid: true }
            || pawn.ItemServices is not { IsValid: true } || pawn.WeaponServices is not { IsValid: true }) return PurchaseFailure.NotReady;
        if (!controller.PawnIsAlive || pawn.Health <= 0) return PurchaseFailure.Dead;
        if (controller.TeamNum is not (2 or 3) || !catalog.AllowsWheel(pawn.TeamNum)) return PurchaseFailure.Team;
        if (catalog.Config.RequireBuyZone && !pawn.InBuyZone) return PurchaseFailure.BuyZone;
        if (catalog.Config.RequireBuyTime && core.EntitySystem.GetGameRules() is not { BuyTimeEnded: false }) return PurchaseFailure.BuyTime;
        return PurchaseFailure.None;
    }
    public PurchaseQuote Quote(IPlayer player, BuyCatalog catalog, BuyItem item)
    {
        var failure = CheckPlayer(player, catalog);
        if (failure != PurchaseFailure.None) return new(failure, item.Price);
        var pawn = player.PlayerPawn!;
        if (!catalog.Items.TryGetValue(item.Id, out var current) || !ReferenceEquals(current, item) || !catalog.AllowsItem(item, pawn.TeamNum)) return new(PurchaseFailure.Disabled, item.Price);
        int price = item.Price;
        var services = pawn.ItemServices!.As<CCSPlayer_ItemServices>();
        if (item.Kind is ItemKind.Kevlar or ItemKind.AssaultSuit)
        {
            if (pawn.ArmorValue >= 100 && (item.Kind == ItemKind.Kevlar || services.HasHelmet)) return new(PurchaseFailure.AlreadyOwned, price);
            if (item.Kind == ItemKind.AssaultSuit)
            {
                int kevlar = catalog.Config.Items.FirstOrDefault(i => i.Kind == ItemKind.Kevlar)?.Price ?? 650;
                if (services.HasHelmet) price = Math.Min(price, kevlar);
                else if (pawn.ArmorValue >= 100) price = Math.Max(0, price - kevlar);
            }
        }
        else if (item.Kind == ItemKind.Defuser && services.HasDefuser) return new(PurchaseFailure.AlreadyOwned, price);
        else if (item.Kind == ItemKind.Knife && pawn.WeaponServices!.MyWeapons.Any(h => h.Value is { IsValid: true } w
            && w.As<CCSWeaponBase>().WeaponBaseVData.GearSlot == gear_slot_t.GEAR_SLOT_KNIFE
            && w.As<CCSWeaponBase>().AttributeManager.Item.ItemDefinitionIndex == item.DefinitionIndex)) return new(PurchaseFailure.AlreadyOwned, price);
        else if (item.Kind == ItemKind.Weapon)
        {
            int count = Count(pawn, item.Classname);
            if (item.Category == "grenades")
            {
                int nativeLimit = NativeGrenadeLimit(item.Classname == "weapon_flashbang" ? "ammo_grenade_limit_flashbang" : "ammo_grenade_limit_default", 1);
                int limit = Math.Min(item.CarryLimit, nativeLimit);
                if (item.Classname == "weapon_flashbang") limit = Math.Min(limit, catalog.Config.MaxFlashbangs);
                int totalLimit = Math.Min(catalog.Config.MaxGrenades, NativeGrenadeLimit("ammo_grenade_limit_total", 3));
                if (count >= limit || CountGrenades(pawn) >= totalLimit) return new(PurchaseFailure.CarryLimit, price);
                if ((item.Classname == "weapon_molotov" && Count(pawn, "weapon_incgrenade") > 0)
                    || (item.Classname == "weapon_incgrenade" && Count(pawn, "weapon_molotov") > 0)) return new(PurchaseFailure.CarryLimit, price);
            }
            else if (count >= item.CarryLimit) return new(PurchaseFailure.AlreadyOwned, price);
        }
        return new(Money(player) >= price ? PurchaseFailure.None : PurchaseFailure.Money, price);
    }
    public PurchaseQuote Purchase(IPlayer player, BuyCatalog catalog, BuyItem item)
    {
        var quote = Quote(player, catalog, item);
        if (!quote.Allowed) return quote;
        try
        {
            bool success = PurchaseTransaction.Execute(quote.Price, () => Money(player), amount => WriteMoney(player, amount), () => Grant(player.PlayerPawn!, item));
            return success ? quote : new(PurchaseFailure.GiveFailed, quote.Price);
        }
        catch (Exception ex)
        {
            core.Logger.LogError(ex, "购买发放失败，已退还本次款项：{Item}", item.Id);
            return new(PurchaseFailure.GiveFailed, quote.Price);
        }
    }
    private bool Grant(CCSPlayerPawn pawn, BuyItem item)
    {
        var services = pawn.ItemServices!.As<CCSPlayer_ItemServices>();
        if (item.Kind is ItemKind.Kevlar or ItemKind.AssaultSuit)
        {
            pawn.ArmorValue = 100; pawn.ArmorValueUpdated();
            if (item.Kind == ItemKind.AssaultSuit) { services.HasHelmet = true; services.HasHelmetUpdated(); }
            return true;
        }
        if (item.Kind == ItemKind.Defuser) { services.HasDefuser = true; services.HasDefuserUpdated(); return true; }
        if (item.Kind == ItemKind.Knife) return GrantKnife(pawn, item);
        var weapons = pawn.WeaponServices!;
        gear_slot_t? slot = item.Category is "rifle" or "smg" or "heavy" ? gear_slot_t.GEAR_SLOT_RIFLE
            : item.Category == "secondary" ? gear_slot_t.GEAR_SLOT_PISTOL : null;
        // 保存具体旧武器。CS2 在枪槽已占用时会创建新实体，却拒绝装备它。
        // 先尝试发放以验证实体有效；只有确认此情况才释放旧枪槽并重试。
        var previous = slot.HasValue ? weapons.MyWeapons.Where(h => h.Value is { IsValid: true } w
            && w.As<CCSWeaponBase>().WeaponBaseVData.GearSlot == slot.Value).ToArray() : [];
        int countBefore = Count(pawn, item.Classname);
        CCSWeaponBase? given = null;
        try
        {
            given = services.GiveItem<CCSWeaponBase>(item.Classname);
            // 第二颗闪光弹由引擎增加现有实体的弹药，GiveItem 可以不返回新实体。
            // 是否发放成功必须检查真实库存数量，不能因此误判失败并退款。
            if (item.Category == "grenades") return Count(pawn, item.Classname) > countBefore;
            if (given is not { IsValid: true }) return false;
            if (Count(pawn, item.Classname) <= countBefore && previous.Length > 0)
            {
                given.Despawn(); given = null;
                foreach (var handle in previous)
                    if (handle.Value is { IsValid: true } old) weapons.DropWeapon(old);
                given = services.GiveItem<CCSWeaponBase>(item.Classname);
            }
            if (given is not { IsValid: true } || Count(pawn, item.Classname) <= countBefore) return false;
            // 此时已确认装备，切枪/旧枪清理失败不能把已完成购买当作发放失败退款。
            try
            {
                foreach (var handle in previous)
                    if (handle.Value is { IsValid: true } old && old.Address != given.Address
                        && weapons.MyWeapons.Any(h => h.Raw == handle.Raw)) weapons.DropWeapon(old);
                if (slot.HasValue) weapons.SelectWeapon(given);
            }
            catch (Exception ex) { core.Logger.LogWarning(ex, "武器已经发放，但切枪或旧枪清理失败：{Item}", item.Id); }
            return true;
        }
        finally
        {
            // 拒绝装备的实体不能遗留成免费地面武器；失败退款由交易层负责。
            if (given is { IsValid: true } && !weapons.MyWeapons.Any(h => h.Value is { IsValid: true } w && w.Address == given.Address))
                given.Despawn();
        }
    }
    private int NativeGrenadeLimit(string name, int fallback) => Math.Max(0, core.ConVar.Find<int>(name)?.Value ?? fallback);
    private bool GrantKnife(CCSPlayerPawn pawn, BuyItem item)
    {
        var weapons = pawn.WeaponServices!;
        var oldKnives = weapons.MyWeapons.Where(h => h.Value is { IsValid: true } w
            && w.As<CCSWeaponBase>().WeaponBaseVData.GearSlot == gear_slot_t.GEAR_SLOT_KNIFE).ToArray();
        CCSWeaponBase? knife = null;
        bool committed = false;
        try
        {
            knife = pawn.ItemServices!.GiveItem<CCSWeaponBase>("weapon_knife");
            if (knife is not { IsValid: true }) return false;
            bool Owns(CCSWeaponBase w) => weapons.MyWeapons.Any(h => h.Value is { IsValid: true } v && v.Address == w.Address);
            if (!Owns(knife))
            {
                knife.Despawn(); knife = null;
                weapons.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
                knife = pawn.ItemServices!.GiveItem<CCSWeaponBase>("weapon_knife");
            }
            if (knife is not { IsValid: true } || !Owns(knife)) return false;
            knife.AcceptInput("ChangeSubclass", item.DefinitionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            knife.AttributeManager.Item.Initialized = true;
            knife.AttributeManager.Item.ItemDefinitionIndex = item.DefinitionIndex;
            knife.AttributeManager.Item.ItemDefinitionIndexUpdated();
            committed = knife.AttributeManager.Item.ItemDefinitionIndex == item.DefinitionIndex;
            if (!committed) return false;
            try
            {
                foreach (var handle in oldKnives)
                    if (handle.Value is { IsValid: true } old && old.Address != knife.Address) old.Despawn();
                weapons.SelectWeapon(knife);
            }
            catch (Exception ex) { core.Logger.LogWarning(ex, "Knife granted; cleanup failed"); }
            return true;
        }
        finally { if (!committed && knife is { IsValid: true }) knife.Despawn(); }
    }
    public static int Count(CCSPlayerPawn pawn, string classname)
    {
        int result = 0;
        var services = pawn.WeaponServices;
        if (services is not { IsValid: true }) return 0;
        foreach (var handle in services.MyWeapons)
        {
            if (handle.Value is not { IsValid: true } weapon || weapon.DesignerName != classname) continue;
            var vdata = weapon.As<CCSWeaponBase>().WeaponBaseVData;
            int ammoIndex = vdata.PrimaryAmmoType.Value;
            if (vdata.GearSlot == gear_slot_t.GEAR_SLOT_GRENADES && ammoIndex >= 0 && ammoIndex < services.Ammo.ElementCount)
                result += Math.Max(1, (int)services.Ammo[ammoIndex]);
            else result++;
        }
        return result;
    }
    private static int CountGrenades(CCSPlayerPawn pawn)
    {
        int total = 0;
        var services = pawn.WeaponServices!;
        foreach (var handle in services.MyWeapons)
        {
            if (handle.Value is not { IsValid: true } weapon) continue;
            var data = weapon.As<CCSWeaponBase>().WeaponBaseVData;
            if (data.GearSlot != gear_slot_t.GEAR_SLOT_GRENADES) continue;
            int index = data.PrimaryAmmoType.Value;
            total += index >= 0 && index < services.Ammo.ElementCount ? Math.Max(1, (int)services.Ammo[index]) : 1;
        }
        return total;
    }
    public static string Explain(PurchaseFailure failure) => failure switch
    {
        PurchaseFailure.None => "可以购买", PurchaseFailure.NotReady => "玩家尚未就绪", PurchaseFailure.Dead => "死亡时不能购买",
        PurchaseFailure.Team => "当前队伍不能使用轮盘", PurchaseFailure.BuyZone => "请进入购买区域", PurchaseFailure.BuyTime => "购买时间已结束",
        PurchaseFailure.Disabled => "物品已禁用或阵营不允许", PurchaseFailure.Money => "资金不足", PurchaseFailure.AlreadyOwned => "已拥有 / 无需补充",
        PurchaseFailure.CarryLimit => "携带数量已达到上限", _ => "发放失败，已退款"
    };
}
