using HanClassicBuyWheel.Domain;
using HanClassicBuyWheel.Services;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace HanClassicBuyWheel;

public sealed partial class HanClassicBuyWheelPlugin
{
    private readonly Dictionary<ulong, IMenuAPI> _wasdMenus = [];
    private readonly Dictionary<ulong, Runtime.PlayerIdentity> _wasdOwners = [];
    private void CloseWasd(IPlayer player)
    {
        _wasdOwners.Remove(player.SessionId);
        if (_wasdMenus.Remove(player.SessionId, out var menu) && ReferenceEquals(Core.MenusAPI.GetCurrentMenu(player), menu))
            Core.MenusAPI.CloseMenuForPlayer(player, menu);
        TryUninstallInput();
    }
    private void OpenWasd(IPlayer player, WheelNavigation? navigation = null)
    {
        if (_catalog is not { } catalog) return;
        var failure = _buy.CheckPlayer(player, catalog);
        if (failure != PurchaseFailure.None) { player.SendChat("[Buy Wheel] " + Failure(player, failure)); return; }
        navigation ??= new(catalog.BuildTree(player.PlayerPawn!.TeamNum));
        var nav = navigation;
        var menu = Core.MenusAPI.CreateBuilder().Design.SetMenuTitle(NodeName(player, nav.Current) + " · $" + BuyService.Money(player)).Build();
        var owner = Identity(player);
        long generation = _generation;
        for (int i = 0; i < nav.Visible.Length; i++)
        {
            int index = i;
            var node = BuyCatalog.Collapse(nav.Visible[i]);
            if (node.IsBlank) continue;
            string name = NodeName(player, node);
            if (node.Item is { } item) { var quote = _buy.Quote(player, catalog, item); name += $" · ${quote.Price}" + (quote.Allowed ? "" : " · " + Failure(player, quote.Failure)); }
            var option = new ButtonMenuOption(name) { CloseAfterClick = false };
            option.Click += async (_, _) => await Core.Scheduler.NextTickAsync(() =>
            {
                if (_unloaded || generation != _generation || !ReferenceEquals(catalog, _catalog) || Resolve(owner) is not { } current
                    || !ReferenceEquals(Core.MenusAPI.GetCurrentMenu(current), menu) || _buy.CheckPlayer(current, catalog) != PurchaseFailure.None) return;
                var chosen = nav.Activate(index);
                if (chosen is not null)
                {
                    var result = _buy.Purchase(current, catalog, chosen);
                    current.SendChat("[Buy Wheel] " + (result.Allowed ? T(current, "purchase.success", WheelTranslations.ItemName(chosen, LanguageFor(current)), result.Price) : Failure(current, result.Failure)));
                    PlayUiSound(current, !result.Allowed);
                    if (result.Allowed && chosen.Category != "grenades") nav.Home();
                }
                OpenWasd(current, nav);
            });
            menu.AddOption(option);
        }
        void AddAction(string title, Action<IPlayer> action)
        {
            var option = new ButtonMenuOption(title) { CloseAfterClick = false };
            option.Click += async (_, _) => await Core.Scheduler.NextTickAsync(() =>
            {
                if (!_unloaded && generation == _generation && ReferenceEquals(catalog, _catalog) && Resolve(owner) is { } current
                    && ReferenceEquals(Core.MenusAPI.GetCurrentMenu(current), menu)) action(current);
            });
            menu.AddOption(option);
        }
        if (nav.LastPage > 0)
        {
            AddAction(T(player, "ui.prev"), current => { nav.ChangePage(-1); OpenWasd(current, nav); });
            AddAction(T(player, "ui.next"), current => { nav.ChangePage(1); OpenWasd(current, nav); });
        }
        if (nav.Depth > 0) AddAction(T(player, "ui.back"), current => { nav.Back(); OpenWasd(current, nav); });
        AddAction(T(player, "ui.close"), CloseWasd);
        _wasdMenus[player.SessionId] = menu;
        _wasdOwners[player.SessionId] = owner;
        Core.MenusAPI.OpenMenuForPlayer(player, menu);
        InstallInput();
    }
}
