using HanClassicBuyWheel.Domain;
using HanClassicBuyWheel.Runtime;
using HanClassicBuyWheel.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanClassicBuyWheel;

public sealed partial class HanClassicBuyWheelPlugin
{
    public const string LayoutPath = "panorama/layout/custom_game/hanclassicbuywheel.vxml_c";
    private CHandle<CCSCustomHudLayout> _sharedHud;

    private int CountSharedHuds() => _worldReady && WorldAvailable()
        ? Core.EntitySystem.GetAllEntitiesByClass<CCSCustomHudLayout>().Count(h => h.IsValid && h.StrLayout == LayoutPath)
        : 0;

    private CCSCustomHudLayout EnsureSharedHud()
    {
        // 首位玩家入服时创建，全体复用；打开路径为热重载等情况补建。
        if (!_worldReady || !WorldAvailable()) throw new InvalidOperationException("地图世界尚未就绪，不能创建购买轮盘 HUD。");
        if (_sharedHud.Value is { IsValid: true } existing) return existing;
        var hud = Core.EntitySystem.CreateEntity<CCSCustomHudLayout>();
        if (hud is not { IsValid: true }) throw new InvalidOperationException("HUD 创建失败");
        _sharedHud = Core.EntitySystem.GetRefEHandle(hud);
        try
        {
            hud.StrLayout = LayoutPath; hud.StrLayoutUpdated();
            hud.Observable = false; hud.ObservableUpdated();
            hud.DispatchSpawn();
            hud.SetHasClass("hcw_screen", "empty", EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass);
            hud.SetInputCaptureEnabled(false);
            hud.SetTransmitState(false);
            return hud;
        }
        catch { DestroySharedHud(); throw; }
    }

    private void DestroySharedHud()
    {
        var handle = _sharedHud;
        _sharedHud = default;
        if (!WorldAvailable()) return;
        var hud = handle.Value;
        if (hud is { IsValid: true }) hud.Despawn();
    }

    private void OnPutInServer(IOnClientPutInServerEvent e)
    {
        if (_unloaded || !WorldAvailable()) return;
        PrepareWorld();
        try
        {
            var hud = EnsureSharedHud();
            // 新连接的传输屏蔽表会重建，不能只在创建实体时屏蔽已有玩家。
            hud.SetHasClassForPlayer(e.PlayerId, "hcw_screen", "empty", EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass);
            hud.SetInputCaptureEnabledForPlayer(e.PlayerId, false);
            hud.SetTransmitState(false, e.PlayerId);
        }
        catch (Exception ex) { Core.Logger.LogError(ex, "购买轮盘入服 HUD 初始化失败：slot={Slot}", e.PlayerId); }
    }

    private void Open(IPlayer player, BuyCatalog catalog)
    {
        foreach (var previous in _sessions.Values.Where(v => v.Owner.Slot == player.PlayerID).ToArray()) Close(previous.Owner.Session);
        var pawn = player.PlayerPawn!;
        CloseWasd(player);
        var view = new WheelSession(Identity(player), _generation, catalog, pawn.TeamNum, Core.EntitySystem.GetRefEHandle(pawn));
        _sessions.Add(player.SessionId, view);
        try
        {
            var hud = EnsureSharedHud();
            view.LastButtons = pawn.MovementServices?.Buttons.ButtonStates[0] ?? 0;
            Render(view, player);
            SetClass(view, "hcw_screen", "empty", false);
            hud.SetInputCaptureEnabledForPlayer(player.PlayerID, true);
            hud.SetTransmitState(true, player.PlayerID);
            InstallInput();
        }
        catch (Exception ex)
        {
            Close(player.SessionId);
            Core.Logger.LogError(ex, "购买轮盘实体创建失败。");
            Chat(player, "ui.failed");
        }
    }
    private void Close(ulong session, [CallerMemberName] string caller = "")
    {
        if (!_sessions.Remove(session, out var view)) return;
#if WHEEL_LOCAL_TEST
        try { RecordLocalHudClose(view, caller); }
        catch (Exception ex) { Core.Logger.LogWarning(ex, "本地关闭来源记录失败，继续清理玩家状态。"); }
#endif
        bool worldAvailable = WorldAvailable();
        if (worldAvailable) SuppressHeld(view);
        if (worldAvailable && _sharedHud.Value is { IsValid: true } hud && !_sessions.Values.Any(v => v.Owner.Slot == view.Owner.Slot))
        {
            try
            {
                // 停止传输不会通知客户端关闭布局，且会阻止下面的隐藏状态送达。
                hud.SetHasClassForPlayer(view.Owner.Slot, "hcw_screen", "empty", EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass);
                hud.SetInputCaptureEnabledForPlayer(view.Owner.Slot, false);
                foreach (var id in view.TextCache.Keys)
                    if (!id.StartsWith("icon:", StringComparison.Ordinal)) hud.RemoveDialogVariableStringForPlayer(view.Owner.Slot, id, "value");
                foreach (var key in view.ClassCache.Keys)
                {
                    if (key == "hcw_screen:empty") continue;
                    int separator = key.IndexOf(':');
                    hud.SetHasClassForPlayer(view.Owner.Slot, key[..separator], key[(separator + 1)..], EHudPanelClassStatus_t.k_eHudPanelClassStatus_Undefined);
                }
                // 已接收过布局的客户端继续接收个人隐藏状态，直到换图／卸载回收实体。
                hud.SetTransmitState(true, view.Owner.Slot);
            }
            catch (Exception ex) { Core.Logger.LogWarning(ex, "共享 HUD 玩家状态清理失败：slot={Slot}, session={Session}", view.Owner.Slot, view.Owner.Session); }
        }
        TryUninstallInput();
    }
    private void CloseAll()
    {
        foreach (var session in _sessions.Keys.ToArray()) Close(session);
        foreach (var entry in _wasdMenus.ToArray())
            if (Core.PlayerManager.GetPlayerFromSessionId(entry.Key) is { IsValid: true } player) CloseWasd(player);
        _wasdMenus.Clear(); _wasdOwners.Clear();
        if (_unloaded) { _heldMasks.Clear(); TryUninstallInput(); }
    }
    private void SetText(WheelSession view, string id, string text)
    {
        if (view.TextCache.TryGetValue(id, out var old) && old == text) return;
        var hud = _sharedHud.Value ?? throw new InvalidOperationException("共享 HUD 已失效");
        hud.SetDialogVariableStringForPlayer(view.Owner.Slot, id, "value", text); view.TextCache[id] = text;
    }
    private void SetClass(WheelSession view, string id, string name, bool enabled)
    {
        string key = id + ":" + name;
        if (view.ClassCache.TryGetValue(key, out bool old) && old == enabled) return;
        var hud = _sharedHud.Value ?? throw new InvalidOperationException("共享 HUD 已失效");
        hud.SetHasClassForPlayer(view.Owner.Slot, id, name, enabled ? EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass
            : EHudPanelClassStatus_t.k_eHudPanelClassStatus_DoesNotHaveClass);
        view.ClassCache[key] = enabled;
    }
    private void Render(WheelSession view, IPlayer player)
    {
        view.Rows = view.Navigation.Visible;
        int count = view.Rows.Length;
        if (view.CountClass != count)
        {
            if (view.CountClass > 0) SetClass(view, "hcw_wheel", "count_" + view.CountClass, false);
            if (count > 0) SetClass(view, "hcw_wheel", "count_" + count, true);
            view.CountClass = count;
        }
        SetClass(view, "hcw_wheel", "root", view.Navigation.Depth == 0);
        SetClass(view, "hcw_screen", "classic", view.Catalog.Config.ClassicStyle);
        SetText(view, "hcw_credit", T(player, "ui.credit"));
        SetText(view, "hcw_title", NodeName(player, view.Navigation.Current));
        SetText(view, "hcw_money", "$" + BuyService.Money(player));
        SetText(view, "hcw_team", view.Team == 2 ? "TERRORIST" : "COUNTER-TERRORIST");
        SetText(view, "hcw_mode", T(player, "mode." + view.Catalog.Config.Mode));
        SetText(view, "hcw_path", T(player, "ui.path", NodeName(player, view.Navigation.Current)));
        SetText(view, "hcw_page", view.Navigation.LastPage > 0 ? $"{view.Navigation.Page + 1} / {view.Navigation.LastPage + 1}" : "");
        SetText(view, "hcw_hint", T(player, "ui.hint") + T(player, view.Catalog.Config.CloseOnDuck ? "ui.duck" : "ui.closehint"));
        foreach (string button in new[] { "back", "prev", "next", "home", "close", "language" }) SetText(view, "hcw_" + button + "_text", T(player, "ui." + button));
        SetClass(view, "hcw_screen", "ct", view.Team == 3);
        SetClass(view, "hcw_prev", "empty", view.Navigation.LastPage == 0);
        SetClass(view, "hcw_next", "empty", view.Navigation.LastPage == 0);
        for (int index = 0; index < WheelNavigation.PageSize; index++)
        {
            string wedge = "hcw_slot_" + index;
            MenuNode? node = index < count ? view.Rows[index] : null;
            var display = node is null ? null : BuyCatalog.Collapse(node);
            var item = display?.Item;
            var quote = item is null ? new PurchaseQuote(PurchaseFailure.None, 0) : _buy.Quote(player, view.Catalog, item);
            bool disabled = node is not null && (!BuyCatalog.HasItems(node) || item is not null && !quote.Allowed);
            SetClass(view, wedge, "empty", node is null);
            SetClass(view, wedge, "blank", node?.IsBlank == true);
            SetClass(view, wedge, "disabled", disabled);
            SetClass(view, wedge, "selected", index == view.Selected);
            SetText(view, "hcw_name_" + index, display is null || display.IsBlank ? "" : NodeName(player, display));
            SetText(view, "hcw_price_" + index, item is null ? "" : "$" + quote.Price);
            // 图标 class 只切换已知内置资源；不把配置内容拼进资源 URI 或 XML。
            string icon = item?.Icon ?? "";
            string cacheKey = "icon:" + index;
            if (!view.TextCache.TryGetValue(cacheKey, out string? oldIcon) || oldIcon != icon)
            {
                if (!string.IsNullOrEmpty(oldIcon)) SetClass(view, "hcw_card_" + index, "icon_" + oldIcon, false);
                if (!string.IsNullOrEmpty(icon)) SetClass(view, "hcw_card_" + index, "icon_" + icon, true);
                view.TextCache[cacheKey] = icon;
            }
        }
        RenderSelection(view, player);
        // 连接生命周期可能重置捕获位；仅为仍有效的个人菜单恢复输入。
        var hud = _sharedHud.Value ?? throw new InvalidOperationException("共享 HUD 已失效");
        if (!hud.IsInputCaptureEnabledForPlayer(view.Owner.Slot)) hud.SetInputCaptureEnabledForPlayer(view.Owner.Slot, true);
    }
    private void RenderSelection(WheelSession view, IPlayer player)
    {
        MenuNode? node = view.Selected >= 0 && view.Selected < view.Rows.Length ? BuyCatalog.Collapse(view.Rows[view.Selected]) : null;
        var item = node?.Item;
        SetText(view, "hcw_selected", node is null ? T(player, "ui.choose") : NodeName(player, node));
        SetText(view, "hcw_details", item is null ? T(player, "ui.instructions") : T(player, "ui.price", _buy.Quote(player, view.Catalog, item).Price));
        string status = view.LastPurchase is { } purchase ? purchase.Allowed && view.StatusItem is { } bought
            ? T(player, "purchase.success", WheelTranslations.ItemName(bought, LanguageFor(player)), purchase.Price)
            : Failure(player, purchase.Failure) : item is null ? "" : Failure(player, _buy.Quote(player, view.Catalog, item).Failure);
        SetText(view, "hcw_status", status);
    }
    private void Act(IPlayer player, string action, int index = -1)
    {
        if (!_sessions.TryGetValue(player.SessionId, out var view) || !IsCurrent(view, out _)) return;
        if (action == "close") { PlayUiSound(player); Close(player.SessionId); return; }
        if (view.Busy || Environment.TickCount64 < view.NextAction) return;
        view.NextAction = Environment.TickCount64 + view.Catalog.Config.ClickCooldownMs;
        view.Busy = true;
        try
        {
            bool failed = false;
            if (action == "back") { if (!view.Navigation.Back()) { PlayUiSound(player); Close(player.SessionId); return; } }
            else if (action == "next") view.Navigation.ChangePage(1);
            else if (action == "prev") view.Navigation.ChangePage(-1);
            else if (action == "home") view.Navigation.Home();
            else if (action == "select")
            {
                if (index < 0 || index >= view.Rows.Length) return;
                if (view.Rows[index].IsBlank) return;
                var item = view.Navigation.Activate(index);
                if (item is not null)
                {
                    var result = _buy.Purchase(player, view.Catalog, item);
                    view.StatusItem = item; view.LastPurchase = result;
                    failed = !result.Allowed;
                    if (result.Allowed && item.Category != "grenades") view.Navigation.Home();
                }
            }
            PlayUiSound(player, failed);
            view.Selected = action == "select" && view.LastPurchase is { Allowed: false } ? index : -1;
            Render(view, player);
        }
        catch (Exception ex) { Close(player.SessionId); Core.Logger.LogError(ex, "轮盘操作失败，已释放输入。"); }
        finally { view.Busy = false; }
    }
    private void OnHudClicked(IOnCustomHudClickedEvent e)
    {
        if (_unloaded || e.CustomHudLayout is not { IsValid: true }) return;
        var player = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (player is not { IsValid: true } || !_sessions.TryGetValue(player.SessionId, out var view)
            || !IsCurrent(view, out _)
            || _sharedHud.Raw != Core.EntitySystem.GetRefEHandle(e.CustomHudLayout).Raw) return;
#if WHEEL_LOCAL_TEST
        RecordLocalHudClick(player, e.ButtonId);
#endif
        HandleHudButton(player, view, e.ButtonId);
    }
    private void HandleHudButton(IPlayer player, WheelSession view, string buttonId)
    {
        if (buttonId == "hcw_language")
        { _playerLanguages[player.SessionId] = LanguageFor(player) == "en" ? "zh-CN" : "en"; PlayUiSound(player); Render(view, player); return; }
        if (buttonId.StartsWith("hcw_choice_", StringComparison.Ordinal)
            && int.TryParse(buttonId.AsSpan(11), out int index)) Act(player, "select", index);
        else if (buttonId is "hcw_back" or "hcw_close" or "hcw_next" or "hcw_prev" or "hcw_home") Act(player, buttonId[4..]);
    }
}
