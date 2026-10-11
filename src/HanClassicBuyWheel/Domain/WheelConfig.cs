using System.Text.Json;
using System.Text.Json.Serialization;

namespace HanClassicBuyWheel.Domain;

public enum WheelMode { Classic, All }
public enum AllowedTeam { any, ct, t }
public enum LocalizationMode { Auto, Fixed }
public enum ItemKind { Weapon, Kevlar, AssaultSuit, Defuser, Knife }

public sealed record BuyItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string NameEnglish { get; init; } = "";
    public string Classname { get; init; } = "";
    public string Category { get; init; } = "";
    public int Price { get; init; }
    public int[] Teams { get; init; } = [2, 3];
    public ItemKind Kind { get; init; }
    public bool Enabled { get; init; } = true;
    public bool StrictTeam { get; init; }
    public int Order { get; init; }
    public int CarryLimit { get; init; } = 1;
    public string Icon { get; init; } = "";
    public int ClassicSlotCT { get; init; } = -1;
    public int ClassicSlotT { get; init; } = -1;
    public ushort DefinitionIndex { get; init; }
}

public sealed record WheelConfig
{
    public bool Enabled { get; init; } = true;
    public bool AllWeapons { get; init; }
    public AllowedTeam AllowedTeam { get; init; } = AllowedTeam.any;
    public bool AllowKnives { get; init; }
    public string OpenCommand { get; init; } = "buywheel";
    public bool UseNativeBuyKey { get; init; }
    public bool SoundsEnabled { get; init; } = true;
    public float SoundVolume { get; init; } = 0.75f;
    public bool ClassicStyle { get; init; } = true;
    [JsonIgnore] public WheelMode Mode => AllWeapons ? WheelMode.All : WheelMode.Classic;
    public bool CloseOnDuck { get; init; } = true;
    public LocalizationMode LanguageMode { get; init; } = LocalizationMode.Auto;
    public string DefaultLanguage { get; init; } = "en";
    public bool RequireBuyZone { get; init; } = true;
    public bool RequireBuyTime { get; init; } = true;
    public bool BlockNativePurchases { get; init; } = true;
    public bool FreezeMovement { get; init; } = true;
    public int ClickCooldownMs { get; init; } = 220;
    public int MaxGrenades { get; init; } = 3;
    public int MaxFlashbangs { get; init; } = 2;
    public bool RoundStartMoneyEnabled { get; init; }
    public bool RoundStartMoneyAdd { get; init; } = true;
    public int RoundStartMoney { get; init; } = 1000;
    public string[] BannedWeapons { get; init; } = [];
    [JsonIgnore] public BuyItem[] Items { get; init; } = [];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static WheelConfig Parse(string json, string weapons, string knives)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var config = document.RootElement.GetProperty("HanClassicBuyWheel").Deserialize<WheelConfig>(JsonOptions)
            ?? throw new InvalidDataException("配置为空");
        BuyItem[] ReadItems(string text, string section)
        {
            using var itemsDoc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return itemsDoc.RootElement.GetProperty(section).GetProperty("Items").Deserialize<BuyItem[]>(JsonOptions) ?? throw new InvalidDataException("Empty items");
        }
        var weaponItems = ReadItems(weapons, "Weapons");
        var knifeItems = ReadItems(knives, "Knives");
        if (weaponItems.Any(i => i.Kind == ItemKind.Knife) || knifeItems.Any(i => i.Kind != ItemKind.Knife)) throw new InvalidDataException("Weapons / Knives item kind mismatch");
        config = config with { Items = [.. weaponItems, .. knifeItems] };
        config.Validate();
        return config;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(AllowedTeam) || !Enum.IsDefined(LanguageMode)) throw new InvalidDataException("模式无效");
        if (OpenCommand is null || OpenCommand.Length is < 1 or > 40 || !OpenCommand.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) throw new InvalidDataException("OpenCommand invalid");
        if (!float.IsFinite(SoundVolume) || SoundVolume is < 0 or > 1) throw new InvalidDataException("SoundVolume invalid");
        if (DefaultLanguage is not ("en" or "zh-CN")) throw new InvalidDataException("DefaultLanguage 只能为 en 或 zh-CN");
        if (ClickCooldownMs is < 50 or > 2000 || MaxGrenades is < 1 or > 20 || MaxFlashbangs is < 1 or > 10
            || RoundStartMoney is < 0 or > 1000000) throw new InvalidDataException("输入/数量/金钱配置越界");
        if (Items is null || Items.Length is < 1 or > 120 || BannedWeapons is null) throw new InvalidDataException("物品数量必须为 1~120");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id)
                || !item.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 64 || item.Price is < 0 or > 1000000
                || item.NameEnglish is null || item.NameEnglish.Length > 64 || item.Classname is null || item.Icon is null
                || item.Teams is null || item.Teams.Length == 0 || item.Teams.Any(t => t is not (2 or 3))
                || item.CarryLimit is < 1 or > 10 || !Enum.IsDefined(item.Kind)
                || item.ClassicSlotCT is < -1 or > 5 || item.ClassicSlotT is < -1 or > 5
                || item.Kind == ItemKind.Knife && (item.DefinitionIndex == 0 || item.Classname != "weapon_knife" || item.Category != "knives")
                || !BuyCatalog.CategoryIds.Contains(item.Category)
                || item.Kind == ItemKind.Weapon && (!item.Classname.StartsWith("weapon_", StringComparison.Ordinal)
                    || !item.Classname.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                || item.Icon.Length > 40 || !item.Icon.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                throw new InvalidDataException("物品配置无效或 ID 重复：" + item?.Id);
        }
    }
}
