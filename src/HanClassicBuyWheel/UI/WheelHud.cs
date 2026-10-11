using HanClassicBuyWheel.Domain;
using HanClassicBuyWheel.Runtime;
using HanClassicBuyWheel.Services;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanClassicBuyWheel;

public sealed partial class HanClassicBuyWheelPlugin
{
    public const string LayoutPath = "panorama/layout/custom_game/hanclassicbuywheel.vxml_c";
    private void Open(IPlayer player, BuyCatalog catalog)
    {
        var pawn = player.PlayerPawn!;
        CloseWasd(player);
        var view = new WheelSession(Identity(player), _generation, catalog, pawn.TeamNum, Core.EntitySystem.GetRefEHandle(pawn));
        _sessions.Add(player.SessionId, view);
        try
        {
            var hud = Core.EntitySystem.CreateEntity<CCSCustomHudLayout>();
            if (hud is not { IsValid: true }) throw new InvalidOperationException("HUD 创建失败");
            view.Hud = Core.EntitySystem.GetRefEHandle(hud);
            hud.StrLayout = LayoutPath; hud.StrLayoutUpdated(); hud.DispatchSpawn();
            hud.SetTransmitState(false); hud.SetTransmitState(true, player.PlayerID);
            hud.SetInputCaptureEnabled(false);
            view.LastButtons = pawn.MovementServices?.Buttons.ButtonStates[0] ?? 0;
            Render(view, player);
            hud.SetInputCaptureEnabledForPlayer(player.PlayerID, true);
            InstallInput();
        }
        catch (Exception ex)
        {
            Close(player.SessionId);
            Core.Logger.LogError(ex, "购买轮盘实体创建失败。");
            Chat(player, "ui.failed");
        }
    }
    private void Close(ulong session)
    {
        if (!_sessions.Remove(session, out var view)) return;
        SuppressHeld(view);
        if (view.Hud.Value is { IsValid: true } hud)
        {
            try { hud.SetInputCaptureEnabledForPlayer(view.Owner.Slot, false); hud.SetInputCaptureEnabled(false); }
            catch (Exception ex) { Core.Logger.LogWarning(ex, "HUD 输入释放失败，继续回收实体。"); }
            try { hud.Despawn(); }
            catch (Exception ex) { Core.Logger.LogWarning(ex, "HUD 实体回收失败。"); }
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
    private static void SetText(WheelSession view, string id, string text)
    {
        if (view.TextCache.TryGetValue(id, out var old) && old == text) return;
        view.Hud.Value?.SetDialogVariableString(id, "value", text); view.TextCache[id] = text;
    }
    private static void SetClass(WheelSession view, string id, string name, bool enabled)
    {
        string key = id + ":" + name;
        if (view.ClassCache.TryGetValue(key, out bool old) && old == enabled) return;
        view.Hud.Value?.SetHasClass(id, name, enabled ? EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass
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
            || view.Hud.Raw != Core.EntitySystem.GetRefEHandle(e.CustomHudLayout).Raw) return;
        if (e.ButtonId == "hcw_language")
        { _playerLanguages[player.SessionId] = LanguageFor(player) == "en" ? "zh-CN" : "en"; PlayUiSound(player); Render(view, player); return; }
        if (e.ButtonId.StartsWith("hcw_choice_", StringComparison.Ordinal)
            && int.TryParse(e.ButtonId.AsSpan(11), out int index)) Act(player, "select", index);
        else if (e.ButtonId is "hcw_back" or "hcw_close" or "hcw_next" or "hcw_prev" or "hcw_home") Act(player, e.ButtonId[4..]);
    }
}
