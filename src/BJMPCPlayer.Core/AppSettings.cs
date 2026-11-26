using System.Text.Json;
using System.Text.Json.Serialization;

namespace BJMPCPlayer.Core;

/// <summary>Réglages persistants (%LOCALAPPDATA%\BJMPC Player\settings.json).</summary>
public sealed class AppSettings
{
    // Audio
    public bool ExclusiveAudio { get; set; }
    // (pas de choix de périphérique : le lecteur joue sur la sortie de Windows ; l'ancien réglage
    //  « AudioDevice » d'un settings.json existant est simplement ignoré à la lecture)
    public bool NormalizeVolume { get; set; }
    public double Volume { get; set; } = 80;
    public bool Muted { get; set; }
    // Vidéo / sous-titres
    public bool HardwareDecoding { get; set; } = true;
    public string SubtitleLanguages { get; set; } = "fr,fre,fra,en,eng";
    public string AudioLanguages { get; set; } = "fr,fre,fra";
    // Lecture
    public bool ResumePlayback { get; set; } = true;
    public bool Shuffle { get; set; }
    public RepeatMode Repeat { get; set; } = RepeatMode.Off;
    // Bibliothèque
    public List<string> MusicFolders { get; set; } = new();
    public List<string> VideoFolders { get; set; } = new();
    public List<string> Recent { get; set; } = new();
    // Apparence : "system", "light", "dark"
    public string Theme { get; set; } = "system";

    [JsonIgnore] public static string Directory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BJMPC Player");
    [JsonIgnore] public static string FilePath => Path.Combine(Directory, "settings.json");
    [JsonIgnore] public static string WatchLaterDirectory => Path.Combine(Directory, "reprise");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings();
        }
        catch { /* fichier abîmé : on repart des valeurs par défaut */ }
        return new AppSettings();
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, FilePath, overwrite: true);
    }

    public void AddRecent(string path)
    {
        Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, path);
        if (Recent.Count > 30) Recent.RemoveRange(30, Recent.Count - 30);
    }
}
