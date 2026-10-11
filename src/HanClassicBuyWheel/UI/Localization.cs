using HanClassicBuyWheel.Domain;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace HanClassicBuyWheel;

public sealed partial class HanClassicBuyWheelPlugin
{
    private WheelTranslations? _texts;
    private string _translationsPath = "";
    private readonly Dictionary<ulong, string> _playerLanguages = [];
    private string LanguageFor(IPlayer player) => WheelTranslations.ResolveLanguage(_catalog!.Config, player.PlayerLanguage.Value, _playerLanguages.GetValueOrDefault(player.SessionId));
    private string T(IPlayer player, string key, params object[] args) => _texts?.Get(LanguageFor(player), key, args) ?? key;
    private string Failure(IPlayer player, PurchaseFailure failure) => T(player, "failure." + failure);
    private string NodeName(IPlayer player, MenuNode node) => node.Item is { } item ? WheelTranslations.ItemName(item, LanguageFor(player)) : T(player, "category." + node.Id);
    private void Chat(IPlayer player, string key, params object[] args) => player.SendChat("[Buy Wheel] " + T(player, key, args));
    private void ChangeLanguage(ICommandContext context) => WithPlayer(context, player =>
    {
        string language = context.Args.FirstOrDefault() ?? "";
        if (language is not ("en" or "zh-CN" or "auto")) { context.Reply("sw_buywheel_language en | zh-CN | auto"); return; }
        if (language == "auto") _playerLanguages.Remove(player.SessionId);
        else _playerLanguages[player.SessionId] = language;
        if (_sessions.TryGetValue(player.SessionId, out var view)) Render(view, player);
        if (_wasdMenus.ContainsKey(player.SessionId)) { CloseWasd(player); OpenWasd(player); }
        if (_catalog is not null) Chat(player, "language.changed");
    });
}
