using HanClassicBuyWheel.Domain;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HanClassicBuyWheel.Runtime;

public readonly record struct PlayerIdentity(int Slot, ulong Session, ulong Steam);
public sealed class WheelSession(PlayerIdentity owner, long generation, BuyCatalog catalog, int team, CHandle<CCSPlayerPawn> pawn)
{
    public PlayerIdentity Owner { get; } = owner;
    public long Generation { get; } = generation;
    public BuyCatalog Catalog { get; } = catalog;
    public int Team { get; } = team;
    public CHandle<CCSPlayerPawn> Pawn { get; } = pawn;
    public WheelNavigation Navigation { get; } = new(catalog.BuildTree(team));
    public MenuNode[] Rows = [];
    public readonly Dictionary<string, string> TextCache = [];
    public readonly Dictionary<string, bool> ClassCache = [];
    public int Selected = -1;
    public ulong LastButtons;
    public long NextAction;
    public bool Busy;
    public BuyItem? StatusItem;
    public PurchaseQuote? LastPurchase;
    public int CountClass;
}
