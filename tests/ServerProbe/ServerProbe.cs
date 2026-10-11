using System.Text.Json;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.Natives;

[PluginMetadata(Id = "ClassicWheelProbe", Name = "Classic wheel local probe", Author = "H-AN", Version = "1.0.0", Description = "临时测试探针，不放入发布包")]
public sealed class ServerProbe(ISwiftlyCore core) : BasePlugin(core)
{
    private Guid _command;
    private int _clicks;
    private string _lastButton = "";
    private Guid _clientHook;
    public override void Load(bool hotReload)
    {
        _command = Core.Command.RegisterCommand("cwbprobe", OnCommand);
        Core.Event.OnCustomHudClicked += OnClick;
        _clientHook = Core.Command.HookClientCommand((slot, command) => { Console.WriteLine($"CWB_CLIENT slot={slot} command={command}"); return SwiftlyS2.Shared.Misc.HookResult.Continue; });
    }
    private void OnClick(IOnCustomHudClickedEvent e)
    {
        if (e.CustomHudLayout is { IsValid: true } hud && hud.StrLayout?.Contains("hanclassicbuywheel") == true)
        { _clicks++; _lastButton = e.ButtonId; }
    }
    private void OnCommand(ICommandContext context)
    {
        if (context.Sender is not null) { context.Reply("server only"); return; }
        var players = Core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient).ToArray();
        if (context.Args.FirstOrDefault() == "relocate")
        {
            foreach (var player in players)
                if (player.PlayerPawn is { IsValid: true } pawn) pawn.Teleport(new Vector(400, 1000, 0), null, null);
            return;
        }
        if (context.Args.FirstOrDefault() == "clientcommand")
        {
            foreach (var player in players) Core.NetMessage.Send<CNETMsg_StringCmd>(message =>
            { message.Command = string.Join(' ', context.Args.Skip(1)); message.Recipients.AddRecipient(player.PlayerID); });
            return;
        }
        if (context.Args.FirstOrDefault() == "prices")
        {
            var pawn = players.FirstOrDefault(p => p.Controller.PawnIsAlive)?.PlayerPawn;
            if (pawn?.ItemServices is not { IsValid: true } items) { context.Reply("CWB_PRICES needs alive human"); return; }
            var prices = new Dictionary<string, int>();
            foreach (string name in "ak47 galilar famas m4a1 m4a1_silencer sg556 aug hkp2000 usp_silencer glock p250 elite fiveseven tec9 cz75a deagle revolver mac10 mp9 ump45 bizon mp7 mp5sd p90 ssg08 awp scar20 g3sg1 nova xm1014 mag7 sawedoff negev m249 hegrenade smokegrenade molotov incgrenade decoy flashbang taser healthshot".Split(' '))
            {
                var weapon = items.GiveItem<CCSWeaponBase>("weapon_" + name);
                try { if (weapon is { IsValid: true }) prices[name] = weapon.WeaponBaseVData.Price; }
                finally { if (weapon is { IsValid: true }) weapon.Despawn(); }
            }
            string path = Path.Combine(Core.PluginDataDirectory, "prices.json");
            _ = Task.Run(() => { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(prices, new JsonSerializerOptions { WriteIndented = true })); });
            context.Reply("CWB_PRICES=" + prices.Count); return;
        }
        if (context.Args.Length > 0 && context.Args[0] == "money")
        {
            foreach (var player in players)
            {
                var money = player.Controller.InGameMoneyServices!;
                money.Account = int.Parse(context.Args[1]); money.AccountUpdated(); player.Controller.InGameMoneyServicesUpdated();
                context.Reply("CWB_MONEY=" + money.Account);
            }
            return;
        }
        if (context.Args.Length > 0 && context.Args[0] == "command")
        {
            foreach (var player in players) player.ExecuteCommand(string.Join(' ', context.Args.Skip(1)));
            return;
        }
        if (context.Args.Length > 0 && context.Args[0] == "respawn")
        { foreach (var player in players) player.Controller.Respawn(); return; }
        if (context.Args.Length > 3 && context.Args[0] == "armor")
        {
            foreach (var p in players)
            {
                p.PlayerPawn!.ArmorValue = int.Parse(context.Args[1]); p.PlayerPawn.ArmorValueUpdated();
                var items = p.PlayerPawn.ItemServices!.As<CCSPlayer_ItemServices>();
                items.HasHelmet = bool.Parse(context.Args[2]); items.HasHelmetUpdated();
                items.HasDefuser = bool.Parse(context.Args[3]); items.HasDefuserUpdated();
            }
            return;
        }
        if (context.Args.Length > 1 && context.Args[0] == "give")
        {
            foreach (var p in players)
            {
                var pawn = p.PlayerPawn!;
                var given = pawn.ItemServices!.GiveItem<CCSWeaponBase>(context.Args[1]);
                context.Reply($"CWB_GIVE valid={given.IsValid} name={(given.IsValid ? given.DesignerName : "invalid")} inventory={string.Join(',', pawn.WeaponServices!.MyWeapons.Select(h => h.Value?.DesignerName))}");
            }
            return;
        }
        var snapshots = players.Select(p => new
        {
            Slot = p.PlayerID, Session = p.SessionId, Alive = p.Controller.PawnIsAlive, Team = p.PlayerPawn?.TeamNum,
            Money = p.Controller.InGameMoneyServices?.Account, Armor = p.PlayerPawn?.ArmorValue,
            EyePitch = p.PlayerPawn?.EyeAngles.X, EyeYaw = p.PlayerPawn?.EyeAngles.Y,
            BuyMenuOpen = p.PlayerPawn?.IsBuyMenuOpen,
            InBuyZone = p.PlayerPawn?.InBuyZone,
            PositionX = p.PlayerPawn?.AbsOrigin?.X, PositionY = p.PlayerPawn?.AbsOrigin?.Y, PositionZ = p.PlayerPawn?.AbsOrigin?.Z,
            Helmet = p.PlayerPawn?.ItemServices?.As<CCSPlayer_ItemServices>().HasHelmet,
            Defuser = p.PlayerPawn?.ItemServices?.As<CCSPlayer_ItemServices>().HasDefuser,
            Weapons = p.PlayerPawn?.WeaponServices?.MyWeapons.Select(h => h.Value).Where(w => w is { IsValid: true })
                .Select(w => new { Name = w!.DesignerName, Definition = w.As<CCSWeaponBase>().AttributeManager.Item.ItemDefinitionIndex, Ammo = w.As<CCSWeaponBase>().WeaponBaseVData.PrimaryAmmoType.Value >= 0 ? p.PlayerPawn!.WeaponServices!.Ammo[w.As<CCSWeaponBase>().WeaponBaseVData.PrimaryAmmoType.Value] : 0 }).ToArray()
        }).ToArray();
        var huds = Core.EntitySystem.GetAllEntitiesByDesignerName<CCSCustomHudLayout>("custom_hud_layout")
            .Where(h => h.IsValid && h.StrLayout.Contains("hanclassicbuywheel")).Select(h => new
            {
                Handle = Core.EntitySystem.GetRefEHandle(h).Raw, Title = ReadText(h, "hcw_title"),
                Selected = ReadText(h, "hcw_selected"), Status = ReadText(h, "hcw_status"),
                Capture = players.Select(p => h.IsInputCaptureEnabledForPlayer(p.PlayerID)).ToArray()
            }).ToArray();
        var rules = Core.EntitySystem.GetGameRules();
        var snapshot = new { Label = context.Args.FirstOrDefault() ?? "snapshot", Players = snapshots, Huds = huds, Clicks = _clicks, LastButton = _lastButton,
            BuyTimeEnded = rules?.BuyTimeEnded, RoundStartTime = rules?.RoundStartTime.Value,
            CurrentTime = Core.Engine.GlobalVars.CurrentTime,
            BuyAnywhere = Core.ConVar.FindAsString("mp_buy_anywhere")?.ValueAsString,
            BuyTime = Core.ConVar.FindAsString("mp_buytime")?.ValueAsString };
        string directory = Core.PluginDataDirectory;
        string label = context.Args.FirstOrDefault() ?? "snapshot";
        if (!label.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) { context.Reply("invalid label"); return; }
        _ = Task.Run(() => { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, label + ".json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true })); });
        context.Reply("CWB_SNAPSHOT=" + label + " | humans=" + players.Length + " | huds=" + huds.Length + " | clicks=" + _clicks);
    }
    private static string ReadText(CCSCustomHudLayout hud, string panel)
    { try { return hud.GetDialogVariableString(panel, "value") ?? ""; } catch (NullReferenceException) { return ""; } }
    public override void Unload() { Core.Command.UnregisterCommand(_command); Core.Command.UnhookClientCommand(_clientHook); Core.Event.OnCustomHudClicked -= OnClick; }
}
