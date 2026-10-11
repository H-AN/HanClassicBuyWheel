using System.Globalization;
using HanClassicBuyWheel.Domain;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace HanClassicBuyWheel.Services;

// 原生 B 在客户端先检查复制的引擎规则，必须与插件配置一起放开。
// 仅恢复仍由本服务持有的值，避免卸载覆盖管理员刚修改的服务器设置。
public sealed class NativeBuyRules(ISwiftlyCore core)
{
    private sealed record OwnedValue(string Original, string Applied);
    private readonly Dictionary<string, OwnedValue> _owned = [];
    private readonly HashSet<string> _reported = [];
    private readonly HashSet<ulong> _syncedClients = [];
    private long _nextCheck;

    public void Apply(WheelConfig config)
    {
        _syncedClients.Clear();
        UpdateRules(config);
        SyncClients(config);
        _nextCheck = Environment.TickCount64 + 1000;
    }

    private void UpdateRules(WheelConfig config)
    {
        SetOverride("mp_buy_anywhere", config.Enabled && !config.RequireBuyZone ? "1" : null);
        SetOverride("mp_buytime", config.Enabled && !config.RequireBuyTime ? "9999" : null);
        // 买时结束是锁存状态；过期后再打开无限买时也必须立即解除。
        if (config.Enabled && !config.RequireBuyTime && core.EntitySystem.GetGameRules() is { BuyTimeEnded: true } rules)
        {
            rules.BuyTimeEnded = false;
        }
    }

    public void Refresh(WheelConfig config)
    {
        if (Environment.TickCount64 < _nextCheck) return;
        UpdateRules(config);
        SyncClients(config);
        _nextCheck = Environment.TickCount64 + 1000;
    }

    public void Restore()
    {
        foreach (string name in _owned.Keys.ToArray()) SetOverride(name, null);
        _syncedClients.Clear();
    }

    private void SyncClients(WheelConfig config)
    {
        if (!config.Enabled || config.RequireBuyZone && config.RequireBuyTime) return;
        _syncedClients.RemoveWhere(session => core.PlayerManager.GetPlayerFromSessionId(session) is not { IsValid: true });
        foreach (var player in core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid || player.IsFakeClient || player.Controller is not { IsValid: true, PawnIsAlive: true }
                || player.PlayerPawn is not { IsValid: true } || _syncedClients.Contains(player.SessionId)) continue;
            if (!config.RequireBuyZone) core.ConVar.ReplicateToClient(player.PlayerID, "mp_buy_anywhere", "1");
            if (!config.RequireBuyTime) core.ConVar.ReplicateToClient(player.PlayerID, "mp_buytime", "9999");
            _syncedClients.Add(player.SessionId);
        }
    }

    private static bool Same(string a, string b) => a == b
        || double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
        && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y) && x == y;

    private void SetOverride(string name, string? desired)
    {
        try
        {
            var convar = core.ConVar.FindAsString(name) ?? throw new InvalidOperationException("Missing ConVar: " + name);
            string current = convar.ValueAsString;
            if (desired is null)
            {
                if (_owned.Remove(name, out var saved) && (Same(current, saved.Applied) || Same(current, saved.Original)))
                {
                    convar.ValueAsString = saved.Original;
                    core.ConVar.ReplicateToAll(name, saved.Original);
                }
                return;
            }
            if (Same(current, desired)) return;
            // 换图/服务器模式重设规则后，保存该模式的新原值，再维持已开启的自由购买。
            string original = _owned.TryGetValue(name, out var previous) && Same(current, previous.Applied)
                ? previous.Original : current;
            _owned[name] = new(original, desired);
            convar.ValueAsString = desired; // 使用正常设置队列，下行由下面显式复制保证。
            // 当前 SDK 对原生 cvar 的 setter 未可靠下发，显式复制以确保 B 的本地检查同步。
            core.ConVar.ReplicateToAll(name, desired);
            _reported.Remove(name);
        }
        catch (Exception ex)
        {
            if (_reported.Add(name)) core.Logger.LogError(ex, "原生购买限制同步失败：{ConVar}", name);
        }
    }
}
