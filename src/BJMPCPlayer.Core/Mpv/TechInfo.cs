using System.Globalization;

namespace BJMPCPlayer.Core.Mpv;

public sealed record TechLine(string Section, string Label, string Value, bool Highlight = false);

/// <summary>Informations techniques en direct (façon « Informations sur le codec » de VLC).</summary>
public static class TechInfo
{
    public static IReadOnlyList<TechLine> Collect(MpvPlayer p)
    {
        var l = new List<TechLine>();
        string? S(string n) => p.GetString(n);
        void Add(string sec, string label, string? value, bool hi = false) { if (!string.IsNullOrWhiteSpace(value)) l.Add(new(sec, label, value!, hi)); }

        // ---- Audio
        var ao = S("current-ao");
        bool exclusiveWanted = S("audio-exclusive") == "yes";
        Add("Audio", "Sortie", ao switch
        {
            "wasapi" => "WASAPI (Windows Audio Session API)",
            null or "" => "aucune (rien en lecture)",
            _ => ao,
        }, ao == "wasapi");
        if (ao == "wasapi")
            Add("Audio", "Mode", exclusiveWanted ? "Exclusif — la carte son reçoit le flux directement, sans le mélangeur de Windows"
                                                 : "Partagé — passe par le mélangeur de Windows", true);
        Add("Audio", "Périphérique", DeviceName(p, S("audio-device")));
        Add("Audio", "Codec", Join(S("audio-codec"), Kbps(p.GetDouble("audio-bitrate"))));
        Add("Audio", "Format du fichier", AudioFormat(p, "audio-params"));
        Add("Audio", "Envoyé à la carte son", AudioFormat(p, "audio-out-params"), true);
        var af = S("af");
        Add("Audio", "Traitements", string.IsNullOrEmpty(af) ? "aucun (signal intact)" : af.Contains("dynaudnorm") ? "égalisation du volume" : af);

        // ---- Vidéo
        if (p.HasVideo())
        {
            Add("Vidéo", "Rendu", S("current-vo"));
            var hw = S("hwdec-current");
            Add("Vidéo", "Décodage", string.IsNullOrEmpty(hw) || hw == "no" ? "logiciel (processeur)" : $"matériel — {hw} (carte graphique)", true);
            Add("Vidéo", "Codec", Join(S("video-codec"), Kbps(p.GetDouble("video-bitrate"))));
            var w = p.GetLong("video-params/w"); var h = p.GetLong("video-params/h");
            Add("Vidéo", "Image", w > 0 ? Join($"{w}×{h}", S("video-params/pixelformat"), S("video-params/primaries"), S("video-params/gamma")) : null);
            var fps = p.GetDouble("container-fps");
            Add("Vidéo", "Images/s", fps > 0 ? fps.Value.ToString("0.###", CultureInfo.InvariantCulture) : null);
            var dropped = (p.GetLong("frame-drop-count") ?? 0) + (p.GetLong("decoder-frame-drop-count") ?? 0);
            Add("Vidéo", "Images perdues", dropped.ToString(CultureInfo.InvariantCulture));
        }

        // ---- Fichier
        Add("Fichier", "Conteneur", S("file-format"));
        var size = p.GetLong("file-size");
        Add("Fichier", "Taille", size > 0 ? $"{size / 1048576.0:0.0} Mo" : null);
        Add("Fichier", "Moteur", Join(S("mpv-version"), "FFmpeg " + S("ffmpeg-version")));
        return l;
    }

    private static string? AudioFormat(MpvPlayer p, string prop)
    {
        var rate = p.GetLong(prop + "/samplerate");
        if (rate is null or 0) return null;
        var fmt = p.GetString(prop + "/format");
        var ch = p.GetLong(prop + "/channel-count") ?? 0;
        var layout = p.GetString(prop + "/hr-channels") ?? p.GetString(prop + "/channels");
        return Join($"{rate / 1000.0:0.#} kHz".Replace('.', ','), SampleFormat(fmt), ch > 0 ? $"{ch} {(ch == 1 ? "canal" : "canaux")} ({layout})" : layout);
    }

    /// <summary>« s16 » → « 16 bits entiers », « floatp » → « 32 bits flottants »…</summary>
    public static string? SampleFormat(string? f) => f switch
    {
        null or "" => null,
        "u8" or "u8p" => "8 bits",
        "s16" or "s16p" => "16 bits entiers",
        "s24" or "s24p" => "24 bits entiers",
        "s32" or "s32p" => "32 bits entiers",
        "float" or "floatp" => "32 bits flottants",
        "double" or "doublep" => "64 bits flottants",
        _ when f.StartsWith("spdif") => "bitstream S/PDIF (" + f + ")",
        _ => f,
    };

    private static string? DeviceName(MpvPlayer p, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id == "auto") return "automatique (sortie par défaut de Windows)";
        return p.AudioDevices().FirstOrDefault(d => d.Name == id)?.Description ?? id;
    }

    private static string? Kbps(double? bps) => bps > 0 ? $"{bps / 1000:0} kbit/s" : null;
    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
}
