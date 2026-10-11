using System.Globalization;
using System.Text.Json;

namespace HanClassicBuyWheel.Domain;

public sealed class WheelTranslations
{
    private readonly Dictionary<string, Dictionary<string, string>> _languages;
    private WheelTranslations(Dictionary<string, Dictionary<string, string>> languages) => _languages = languages;
    public static WheelTranslations Load(string directory)
    {
        var languages = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (string language in new[] { "en", "zh-CN" })
        {
            var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(directory, language + ".jsonc")), WheelConfig.JsonOptions)
                ?? throw new InvalidDataException("Empty language: " + language);
            foreach (var text in texts.Values) _ = System.Text.CompositeFormat.Parse(text);
            languages.Add(language, texts);
        }
        if (!languages["en"].Keys.ToHashSet().SetEquals(languages["zh-CN"].Keys)) throw new InvalidDataException("Language key sets differ");
        foreach (string key in BuyCatalog.CategoryIds.Append("root").Append("primary").Select(id => "category." + id)
            .Concat(Enum.GetNames<PurchaseFailure>().Select(id => "failure." + id)))
            if (!languages["en"].ContainsKey(key)) throw new InvalidDataException("Missing translation: " + key);
        return new(languages);
    }
    public string Get(string language, string key, params object[] args)
    {
        var texts = _languages.GetValueOrDefault(language) ?? _languages["en"];
        string text = texts.GetValueOrDefault(key) ?? _languages["en"].GetValueOrDefault(key) ?? key;
        return args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
    }
    public static string ResolveLanguage(WheelConfig config, string? playerLanguage, string? manualLanguage)
    {
        if (manualLanguage is "en" or "zh-CN") return manualLanguage;
        if (config.LanguageMode == LocalizationMode.Fixed || string.IsNullOrWhiteSpace(playerLanguage)) return config.DefaultLanguage;
        return playerLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase) || playerLanguage is "schinese" or "tchinese" ? "zh-CN" : "en";
    }
    public static string ItemName(BuyItem item, string language) => language == "en" && !string.IsNullOrWhiteSpace(item.NameEnglish) ? item.NameEnglish : item.Name;
}
