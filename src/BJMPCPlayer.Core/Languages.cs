namespace BJMPCPlayer.Core;

/// <summary>Noms français des codes de langue des pistes (ISO 639-1 / 639-2).</summary>
public static class Languages
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fr"] = "Français", ["fre"] = "Français", ["fra"] = "Français",
        ["en"] = "Anglais", ["eng"] = "Anglais",
        ["de"] = "Allemand", ["ger"] = "Allemand", ["deu"] = "Allemand",
        ["es"] = "Espagnol", ["spa"] = "Espagnol",
        ["it"] = "Italien", ["ita"] = "Italien",
        ["pt"] = "Portugais", ["por"] = "Portugais",
        ["nl"] = "Néerlandais", ["dut"] = "Néerlandais", ["nld"] = "Néerlandais",
        ["ja"] = "Japonais", ["jpn"] = "Japonais",
        ["zh"] = "Chinois", ["chi"] = "Chinois", ["zho"] = "Chinois",
        ["ko"] = "Coréen", ["kor"] = "Coréen",
        ["ru"] = "Russe", ["rus"] = "Russe",
        ["ar"] = "Arabe", ["ara"] = "Arabe",
        ["pl"] = "Polonais", ["pol"] = "Polonais",
        ["sv"] = "Suédois", ["swe"] = "Suédois",
        ["tr"] = "Turc", ["tur"] = "Turc",
        ["und"] = "Indéterminée",
    };

    public static string? Name(string? code) =>
        string.IsNullOrEmpty(code) ? null : Names.TryGetValue(code, out var n) ? n : code;
}
