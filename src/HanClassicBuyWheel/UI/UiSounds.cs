using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Sounds;

namespace HanClassicBuyWheel;

public sealed partial class HanClassicBuyWheelPlugin
{
    private void OnPrecache(IOnPrecacheResourceEvent e) => e.AddItem("soundevents/game_sounds_ui.vsndevts");
    private void PlayUiSound(IPlayer player, bool failed = false)
    {
        if (_catalog is not { Config.SoundsEnabled: true } catalog || !player.IsValid) return;
        try
        {
            using var sound = new SoundEvent(failed ? "Vote.Failed" : "UIPanorama.submenu_select", catalog.Config.SoundVolume, 1f);
            sound.SourceEntityIndex = -1;
            sound.Recipients.RemoveAllPlayers();
            sound.Recipients.AddRecipient(player.PlayerID);
            sound.Emit();
        }
        catch (Exception ex) { Core.Logger.LogWarning(ex, "UI sound failed"); }
    }
}
