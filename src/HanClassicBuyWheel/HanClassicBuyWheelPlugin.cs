using HanClassicBuyWheel.Domain;
using HanClassicBuyWheel.Runtime;
using HanClassicBuyWheel.Services;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace HanClassicBuyWheel;

[PluginMetadata(Id = "HanClassicBuyWheel", Name = "Han Classic Buy Wheel", Author = "H-AN", Version = "1.0.2", Description = "共享单实体经典购买轮盘、原生 Panorama UI 与配置驱动购买系统")]
public sealed partial class HanClassicBuyWheelPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private readonly Dictionary<ulong, WheelSession> _sessions = [];
    private readonly List<Guid> _commands = [], _events = [];
#if WHEEL_LOCAL_TEST
    private const bool AllowBotsForLocalTest = true;
#else
    private const bool AllowBotsForLocalTest = false;
#endif
    private readonly BuyService _buy = new(core, AllowBotsForLocalTest);
    private readonly NativeBuyRules _nativeBuyRules = new(core);
    private BuyCatalog? _catalog;
    private Guid _clientHook;
    private long _generation, _configRequest;
    private bool _unloaded, _inputInstalled;
    private CancellationTokenSource? _refresh;
    private string _configPath = "";
    private string _weaponsPath = "", _knivesPath = "";
    private Guid? _openCommand;
    private readonly HashSet<ulong> _pendingNativeOpens = [];
    private string _configStatus = "读取中";
    private static PlayerIdentity Identity(IPlayer p) => new(p.PlayerID, p.SessionId, p.SteamID);
    private IPlayer? Resolve(PlayerIdentity identity)
    {
        var player = Core.PlayerManager.GetPlayerFromSessionId(identity.Session);
        return player is { IsValid: true } && (!player.IsFakeClient || AllowBotsForLocalTest) && Identity(player) == identity ? player : null;
    }
    private void Lifecycle(Action action)
    {
        // 框架生命周期是同步契约，必须清理完实体和订阅后才让卸载返回；业务路径不阻塞。
        if (Core.IsGameThread) action();
        else Core.Scheduler.NextTickAsync(action).GetAwaiter().GetResult();
    }
    public override void Load(bool hotReload) => Lifecycle(() =>
    {
        _unloaded = false;
        Core.Configuration.InitializeWithTemplate("HanClassicBuyWheel.jsonc", "HanClassicBuyWheel.jsonc");
        Core.Configuration.InitializeWithTemplate("Weapons.jsonc", "Weapons.jsonc");
        Core.Configuration.InitializeWithTemplate("Knives.jsonc", "Knives.jsonc");
        _configPath = Core.Configuration.GetConfigPath("HanClassicBuyWheel.jsonc");
        _weaponsPath = Core.Configuration.GetConfigPath("Weapons.jsonc");
        _knivesPath = Core.Configuration.GetConfigPath("Knives.jsonc");
        _translationsPath = Path.Combine(Core.PluginPath, "resources", "translations");
        Register("buywheel_close", context => WithPlayer(context, p => { Close(p.SessionId); CloseWasd(p); }));
        Register("buywheel_back", context => WithPlayer(context, p => Act(p, "back")));
        Register("buywheel_next", context => WithPlayer(context, p => Act(p, "next")));
        Register("buywheel_prev", context => WithPlayer(context, p => Act(p, "prev")));
        Register("buywheel_wasd", context => WithPlayer(context, p => { Close(p.SessionId); OpenWasd(p); }));
        Register("buywheel_language", ChangeLanguage);
        Register("buywheel_reload", context => { context.Reply("配置重新读取已提交"); ReadConfiguration(); }, "hanclassicbuywheel.admin");
        Register("buywheel_status", context => context.Reply($"HanClassicBuyWheel 1.0.2 | {_configStatus} | mode={_catalog?.Config.Mode} | sessions={_sessions.Count} | sharedHud={_sharedHud.Raw} | worldReady={_worldReady} | hudCount={CountSharedHuds()}"));
#if WHEEL_LOCAL_TEST
        RegisterLocalHudTests();
#endif
        Core.Event.OnCustomHudClicked += OnHudClicked;
        Core.Event.OnClientDisconnected += OnDisconnected;
        Core.Event.OnClientPutInServer += OnPutInServer;
        Core.Event.OnMapLoad += OnMapLoad;
        Core.Event.OnMapUnload += OnMapUnload;
        Core.Event.OnWorldUpdate += OnWorldUpdate;
        Core.Event.OnPrecacheResource += OnPrecache;
        Core.MenusAPI.MenuOpened += OnOtherMenu;
        _clientHook = Core.Command.HookClientCommand(OnClientCommand);
        _events.Add(Core.GameEvent.HookPost<EventRoundStart>(_ => { CloseAll(); if (_catalog is { } catalog && WorldAvailable()) _nativeBuyRules.Apply(catalog.Config); GiveRoundMoney(); return HookResult.Continue; }));
        _events.Add(Core.GameEvent.HookPost<EventRoundEnd>(_ => { CloseAll(); return HookResult.Continue; }));
        _events.Add(Core.GameEvent.HookPost<EventPlayerDeath>(_ => { Prune(); return HookResult.Continue; }));
        AwaitWorld();
        ReadConfiguration();
        Core.Logger.LogInformation("HanClassicBuyWheel 1.0.2 已加载，共享一个 HUD，SDK 1.4.13。HotReload={HotReload}", hotReload);
    });
    private void Register(string name, ICommandService.CommandListener action, string permission = "")
    {
        if (Core.Command.IsCommandRegistered("sw_" + name)) throw new InvalidOperationException("命令冲突：sw_" + name);
        _commands.Add(Core.Command.RegisterCommand(name, action, false, permission));
    }
    private void WithPlayer(ICommandContext context, Action<IPlayer> action)
    {
        if (context.Sender is { IsValid: true, IsFakeClient: false } player) action(player);
        else context.Reply("请在游戏中执行此命令");
    }
    private async void ReadConfiguration()
    {
        long request = ++_configRequest;
        try
        {
            string path = _configPath;
            string translationsPath = _translationsPath;
            string weaponsPath = _weaponsPath, knivesPath = _knivesPath;
            var result = await Task.Run(async () => (Catalog: new BuyCatalog(WheelConfig.Parse(await File.ReadAllTextAsync(path), await File.ReadAllTextAsync(weaponsPath), await File.ReadAllTextAsync(knivesPath))), Texts: WheelTranslations.Load(translationsPath)));
            await Core.Scheduler.NextWorldUpdateAsync(() =>
            {
                if (_unloaded || request != _configRequest) return;
                if (_catalog?.Config.OpenCommand != result.Catalog.Config.OpenCommand)
                {
                    string name = "sw_" + result.Catalog.Config.OpenCommand;
                    if (Core.Command.IsCommandRegistered(name)) throw new InvalidOperationException("Command conflict: " + name);
                    var next = Core.Command.RegisterCommand(result.Catalog.Config.OpenCommand, c => WithPlayer(c, Toggle), false, "");
                    if (_openCommand is { } previous) Core.Command.UnregisterCommand(previous);
                    _openCommand = next;
                }
                CloseAll(); _catalog = result.Catalog; _texts = result.Texts; _configStatus = "配置有效";
                if (_worldReady && WorldAvailable()) _nativeBuyRules.Apply(result.Catalog.Config);
                Core.Logger.LogInformation("购买轮盘配置生效：{Mode}，{Count} 个物品。", result.Catalog.Config.Mode, result.Catalog.Items.Count);
            });
        }
        catch (Exception ex)
        {
            if (_unloaded || request != _configRequest) return;
            _configStatus = "配置读取失败，保留上次有效配置";
            Core.Logger.LogError(ex, "购买轮盘配置无效，未替换运行配置。");
        }
    }
    private void Toggle(IPlayer player)
    {
        if (_sessions.ContainsKey(player.SessionId)) { Close(player.SessionId); return; }
        if (_catalog is not { } catalog) { player.SendChat("[Buy Wheel] Configuration is not ready"); return; }
        if (!_worldReady || !WorldAvailable()) { player.SendChat("[Buy Wheel] " + Failure(player, PurchaseFailure.NotReady)); return; }
        var failure = _buy.CheckPlayer(player, catalog);
        if (failure != PurchaseFailure.None) { player.SendChat("[Buy Wheel] " + Failure(player, failure)); PlayUiSound(player, true); return; }
        Open(player, catalog);
    }
    private HookResult OnClientCommand(int slot, string command)
    {
        if (_unloaded || _catalog is not { } catalog) return HookResult.Continue;
        var player = Core.PlayerManager.GetPlayer(slot);
        if (player is not { IsValid: true, IsFakeClient: false } || player.PlayerPawn is not { IsValid: true } pawn || !catalog.AllowsWheel(pawn.TeamNum)) return HookResult.Continue;
        string name = command.Split(' ', 2)[0];
        if (name.Equals("open_buymenu", StringComparison.OrdinalIgnoreCase) && catalog.Config.UseNativeBuyKey)
        {
            if (!_pendingNativeOpens.Add(player.SessionId)) return HookResult.Stop;
            // buymenu 是客户端命令；真正上传的通知是 open_buymenu。
            // 在客户端下一次 buymenu 切换关闭原生页面，再创建本插件的输入上下文。
            var owner = Identity(player);
            long generation = _generation;
            Core.Scheduler.NextTick(() =>
            {
                if (_unloaded || generation != _generation || !ReferenceEquals(catalog, _catalog) || Resolve(owner) is not { } current) { _pendingNativeOpens.Remove(owner.Session); return; }
                try { current.ExecuteCommand("buymenu"); }
                catch (Exception ex) { _pendingNativeOpens.Remove(owner.Session); Core.Logger.LogWarning(ex, "Native buy menu close failed"); return; }
                Core.Scheduler.NextTick(() =>
                {
                    _pendingNativeOpens.Remove(owner.Session);
                    if (!_unloaded && generation == _generation && ReferenceEquals(catalog, _catalog) && Resolve(owner) is { } ready) Toggle(ready);
                });
            });
            return HookResult.Stop;
        }
        if (catalog.Config.BlockNativePurchases && (name is "buy" or "autobuy" or "rebuy"))
        { Chat(player, "buy.usewheel", "sw_" + catalog.Config.OpenCommand); return HookResult.Stop; }
        return HookResult.Continue;
    }
    private void StartRefresh()
    {
        StopRefresh(); _refresh = Core.Scheduler.RepeatBySeconds(0.25f, Refresh);
        Core.Scheduler.StopOnMapChange(_refresh);
    }
    private void StopRefresh() { _refresh?.Cancel(); _refresh = null; }
    private void Refresh()
    {
        if (_unloaded || !_worldReady || !WorldAvailable()) return;
        if (_catalog is { } catalog) _nativeBuyRules.Refresh(catalog.Config);
        foreach (var session in _playerLanguages.Keys.ToArray()) if (Core.PlayerManager.GetPlayerFromSessionId(session) is null) _playerLanguages.Remove(session);
        foreach (var held in _heldMasks.Values.ToArray()) if (Resolve(held.Owner) is null) _heldMasks.Remove(held.Owner.Session);
        foreach (var owner in _wasdOwners.Values.ToArray())
        {
            var player = Resolve(owner);
            if (player is null) { _wasdMenus.Remove(owner.Session); _wasdOwners.Remove(owner.Session); }
            else if (_catalog is not { } wasdCatalog || _buy.CheckPlayer(player, wasdCatalog) != PurchaseFailure.None) CloseWasd(player);
            else if (!_wasdMenus.TryGetValue(owner.Session, out var menu) || !ReferenceEquals(Core.MenusAPI.GetCurrentMenu(player), menu))
            { _wasdMenus.Remove(owner.Session); _wasdOwners.Remove(owner.Session); }
        }
        TryUninstallInput();
        foreach (var view in _sessions.Values.ToArray())
        {
            if (!IsCurrent(view, out var player)) { Close(view.Owner.Session); continue; }
            try { Render(view, player!); }
            catch (Exception ex) { Close(view.Owner.Session); Core.Logger.LogError(ex, "购买轮盘刷新失败，已关闭。"); }
        }
    }
    private bool IsCurrent(WheelSession view, out IPlayer? player)
    {
        player = Resolve(view.Owner);
        return !_unloaded && _worldReady && WorldAvailable() && view.Generation == _generation && ReferenceEquals(view.Catalog, _catalog)
            && _sharedHud.Value is { IsValid: true }
            && _sessions.TryGetValue(view.Owner.Session, out var current) && ReferenceEquals(view, current)
            && player?.PlayerPawn is { IsValid: true } pawn && pawn.TeamNum == view.Team && view.Pawn.Raw == Core.EntitySystem.GetRefEHandle(pawn).Raw
            && _buy.CheckPlayer(player, view.Catalog) == PurchaseFailure.None;
    }
    private void Prune() { foreach (var view in _sessions.Values.ToArray()) if (!IsCurrent(view, out _)) Close(view.Owner.Session); }
    private void GiveRoundMoney()
    {
        if (_catalog is not { Config.RoundStartMoneyEnabled: true } catalog) return;
        foreach (var player in Core.PlayerManager.GetAllPlayers())
            if (player is { IsValid: true } && player.PlayerPawn is { IsValid: true } pawn && catalog.AllowsWheel(pawn.TeamNum)
                && player.Controller.InGameMoneyServices is { IsValid: true })
                BuyService.WriteMoney(player, catalog.Config.RoundStartMoneyAdd ? Math.Min(1000000, BuyService.Money(player) + catalog.Config.RoundStartMoney) : catalog.Config.RoundStartMoney);
    }
    private void OnDisconnected(IOnClientDisconnectedEvent e)
    {
        var disconnecting = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (disconnecting is not null) { _playerLanguages.Remove(disconnecting.SessionId); _pendingNativeOpens.Remove(disconnecting.SessionId); }
        foreach (var view in _sessions.Values.Where(s => s.Owner.Slot == e.PlayerId).ToArray())
        { Close(view.Owner.Session); _heldMasks.Remove(view.Owner.Session); }
        foreach (var held in _heldMasks.Values.Where(h => h.Owner.Slot == e.PlayerId).ToArray()) _heldMasks.Remove(held.Owner.Session);
        foreach (var owner in _wasdOwners.Values.Where(o => o.Slot == e.PlayerId).ToArray())
        { _wasdOwners.Remove(owner.Session); _wasdMenus.Remove(owner.Session); }
        TryUninstallInput();
    }
    private void OnMapLoad(IOnMapLoadEvent _) { if (!_unloaded) AwaitWorld(); }
    private void OnMapUnload(IOnMapUnloadEvent _)
    {
#if WHEEL_LOCAL_TEST
        StopLocalHudTests();
#endif
        CancelWorld();
        ++_generation; StopRefresh(); CloseAll(); DestroySharedHud(); _heldMasks.Clear(); _pendingNativeOpens.Clear(); TryUninstallInput();
    }
    private void OnOtherMenu(object? _, MenuManagerEventArgs e)
    { if (e.Player is { IsValid: true } player) Close(player.SessionId); }
    public override void Unload() => Lifecycle(() =>
    {
        _unloaded = true; ++_generation; ++_configRequest;
        CancelWorld();
#if WHEEL_LOCAL_TEST
        StopLocalHudTests();
#endif
        StopRefresh(); CloseAll(); DestroySharedHud();
        _nativeBuyRules.Restore();
        Core.Event.OnCustomHudClicked -= OnHudClicked;
        Core.Event.OnClientDisconnected -= OnDisconnected;
        Core.Event.OnClientPutInServer -= OnPutInServer;
        Core.Event.OnMapLoad -= OnMapLoad;
        Core.Event.OnMapUnload -= OnMapUnload;
        Core.Event.OnWorldUpdate -= OnWorldUpdate;
        Core.Event.OnPrecacheResource -= OnPrecache;
        Core.MenusAPI.MenuOpened -= OnOtherMenu;
        Core.Command.UnhookClientCommand(_clientHook);
        foreach (var id in _events) Core.GameEvent.Unhook(id);
        foreach (var id in _commands) Core.Command.UnregisterCommand(id);
        if (_openCommand is { } openCommand) Core.Command.UnregisterCommand(openCommand);
        _openCommand = null;
        _events.Clear(); _commands.Clear(); _playerLanguages.Clear(); _pendingNativeOpens.Clear(); _catalog = null; _texts = null;
    });
}
