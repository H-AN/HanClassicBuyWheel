using HanClassicBuyWheel.Domain;
using HanClassicBuyWheel.Runtime;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Players;

namespace HanClassicBuyWheel;

public sealed partial class HanClassicBuyWheelPlugin
{
    private const ulong Attack = 1, Duck = 4, Attack2 = 2048;
    private readonly Dictionary<ulong, (Runtime.PlayerIdentity Owner, ulong Mask)> _heldMasks = [];
    private void InstallInput()
    {
        if (_inputInstalled) return;
        Core.GameHooks.Movement.RunCommand.Pre += OnRunCommand;
        _inputInstalled = true;
    }
    private void TryUninstallInput()
    {
        if (!_inputInstalled || _sessions.Count > 0 || _wasdMenus.Count > 0 || _heldMasks.Count > 0) return;
        Core.GameHooks.Movement.RunCommand.Pre -= OnRunCommand;
        _inputInstalled = false;
    }
    private void SuppressHeld(WheelSession view)
    {
        ulong mask = view.LastButtons & (Attack | Attack2 | (view.Catalog.Config.CloseOnDuck ? Duck : 0));
        if (mask != 0 && !_unloaded) _heldMasks[view.Owner.Session] = (view.Owner, mask);
    }
    private void OnRunCommand(ref RunCommandMovementPreContext context)
    {
        var player = context.Params.Player;
        if (_unloaded || player is not { IsValid: true, IsFakeClient: false }) return;
        var command = context.Params.UserCmd;
        var protobuf = command.CSGOUserCmd.Base;
        ulong buttons = command.ButtonState.ButtonStates[0];
        if (_heldMasks.TryGetValue(player.SessionId, out var held))
        {
            ulong mask = held.Mask;
            mask &= buttons;
            if (mask == 0) { _heldMasks.Remove(player.SessionId); TryUninstallInput(); }
            else
            {
                _heldMasks[player.SessionId] = (held.Owner, mask);
                for (int i = 0; i < 3; i++) command.ButtonState.ButtonStates[i] &= ~mask;
                protobuf.ButtonsPb.Buttonstate1 &= ~mask; protobuf.ButtonsPb.Buttonstate2 &= ~mask; protobuf.ButtonsPb.Buttonstate3 &= ~mask;
            }
        }
        if (!_sessions.TryGetValue(player.SessionId, out var view))
        {
            if (_catalog is { Config.CloseOnDuck: true } && (buttons & Duck) != 0
                && _wasdMenus.TryGetValue(player.SessionId, out var menu) && ReferenceEquals(Core.MenusAPI.GetCurrentMenu(player), menu))
            {
                var owner = Identity(player);
                Core.Scheduler.NextTick(() => { if (Resolve(owner) is { } current && ReferenceEquals(Core.MenusAPI.GetCurrentMenu(current), menu)) CloseWasd(current); });
            }
            return;
        }
        if (!IsCurrent(view, out _)) { Close(player.SessionId); return; }
        try
        {
            var config = view.Catalog.Config;
            ulong pressed = buttons & ~view.LastButtons;
            view.LastButtons = buttons;
            // 原生 HUD 捕获鼠标后，游戏仍上传蹲键。只负责关闭与可选冻结移动。
            if (config.CloseOnDuck && (pressed & Duck) != 0)
            {
                Close(player.SessionId);
                return;
            }
            if (config.FreezeMovement) { protobuf.Forwardmove = 0; protobuf.Leftmove = 0; protobuf.Upmove = 0; }
        }
        catch (Exception ex) { Close(player.SessionId); Core.Logger.LogError(ex, "轮盘输入处理失败，已关闭。"); }
    }
}
