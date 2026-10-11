namespace HanClassicBuyWheel.Domain;

public sealed record MenuNode(string Id, string Name, MenuNode[] Children, BuyItem? Item = null)
{
    public bool IsLeaf => Item is not null;
    public bool IsBlank => Id.StartsWith("blank.", StringComparison.Ordinal);
}

public sealed class BuyCatalog
{
    public static readonly string[] CategoryIds = ["secondary", "heavy", "smg", "rifle", "equipment", "grenades", "knives"];
    private readonly HashSet<string> _banned;
    public WheelConfig Config { get; }
    public IReadOnlyDictionary<string, BuyItem> Items { get; }
    public BuyCatalog(WheelConfig config)
    {
        config.Validate();
        Config = config;
        Items = config.Items.ToDictionary(i => i.Id, StringComparer.Ordinal);
        _banned = new(config.BannedWeapons, StringComparer.OrdinalIgnoreCase);
        foreach (int team in new[] { 2, 3 })
            foreach (string category in CategoryIds.Where(c => c != "knives"))
            {
                var slots = config.Items.Where(i => i.Category == category && i.Enabled && i.Teams.Contains(team))
                    .Select(i => team == 3 ? i.ClassicSlotCT : i.ClassicSlotT).Where(i => i >= 0).ToArray();
                if (slots.Distinct().Count() != slots.Length || category == "equipment" && slots.Any(i => i >= 4))
                    throw new InvalidDataException($"Duplicate or invalid classic slot: {team}/{category}");
            }
    }
    public bool AllowsWheel(int team) => Config.Enabled && team is 2 or 3 && (Config.AllowedTeam == AllowedTeam.any || team == (Config.AllowedTeam == AllowedTeam.ct ? 3 : 2));
    public bool AllowsItem(BuyItem item, int team) => AllowsWheel(team) && item.Enabled && !_banned.Contains(item.Classname)
        && (item.Kind != ItemKind.Knife || Config.AllowKnives)
        && (item.Teams.Contains(team) || !item.StrictTeam && Config.AllWeapons);
    public MenuNode BuildTree(int team)
    {
        MenuNode Category(string id)
        {
            var items = Config.Items.Where(i => i.Category == id && AllowsItem(i, team)).OrderBy(i => i.Order).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
            if (Config.AllWeapons || id == "knives") return new(id, id, items.Select(i => new MenuNode("item." + i.Id, i.Name, [], i)).ToArray());
            var children = Enumerable.Range(0, id == "equipment" ? 4 : 6).Select(slot =>
            {
                var item = items.FirstOrDefault(i => (team == 3 ? i.ClassicSlotCT : i.ClassicSlotT) == slot);
                return item is null ? new MenuNode($"blank.{id}.{slot}", "", []) : new MenuNode("item." + item.Id, item.Name, [], item);
            }).ToArray();
            return new(id, id, children);
        }
        return new("root", "root", CategoryIds.Where(id => id != "knives" || Config.AllowKnives).Select(Category).ToArray());
    }
    public static bool HasItems(MenuNode node) => node.IsLeaf || node.Children.Any(HasItems);
    public static MenuNode Collapse(MenuNode node)
    {
        while (!node.IsLeaf && node.Children.Length == 1) node = node.Children[0];
        return node;
    }
}
