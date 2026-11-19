namespace BJMPCPlayer.Core;

/// <summary>Extensions reconnues (libmpv/FFmpeg lit bien plus ; ces listes servent aux filtres et à la bibliothèque).</summary>
public static class MediaFormats
{
    public static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".wma", ".alac", ".aiff", ".aif", ".ape",
        ".wv", ".mka", ".ac3", ".eac3", ".dts", ".thd", ".mlp", ".dsf", ".dff", ".tta", ".mpc", ".mid", ".midi",
        ".amr", ".au", ".caf", ".spx", ".m4b", ".mp2", ".mod", ".xm", ".it", ".s3m",
    };

    public static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".avi", ".mov", ".wmv", ".webm", ".flv", ".ts", ".m2ts", ".mts", ".mpg", ".mpeg",
        ".vob", ".ogv", ".3gp", ".3g2", ".divx", ".xvid", ".asf", ".rm", ".rmvb", ".f4v", ".hevc", ".h264", ".264",
        ".265", ".y4m", ".mxf", ".dv", ".nut", ".gif",
    };

    public static readonly HashSet<string> Subtitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".sup", ".smi", ".lrc",
    };

    public static readonly HashSet<string> Playlists = new(StringComparer.OrdinalIgnoreCase) { ".m3u", ".m3u8", ".pls" };

    public static bool IsAudio(string path) => Audio.Contains(Path.GetExtension(path));
    public static bool IsVideo(string path) => Video.Contains(Path.GetExtension(path));
    public static bool IsMedia(string path) => IsAudio(path) || IsVideo(path);
    public static bool IsSubtitle(string path) => Subtitles.Contains(Path.GetExtension(path));
    public static bool IsPlaylist(string path) => Playlists.Contains(Path.GetExtension(path));
    public static bool IsUrl(string s) => Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "rtsp" or "rtmp" or "mms" or "udp";

    /// <summary>Filtre pour les boîtes de dialogue (« *.mp3;*.mkv… »).</summary>
    public static IEnumerable<string> AllExtensions() => Audio.Concat(Video).Concat(Playlists);

    /// <summary>Lit une liste M3U/M3U8/PLS ; les chemins relatifs sont résolus par rapport à la liste.</summary>
    public static List<string> ReadPlaylist(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        var result = new List<string>();
        bool pls = Path.GetExtension(path).Equals(".pls", StringComparison.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0) continue;
            if (pls)
            {
                if (!line.StartsWith("File", StringComparison.OrdinalIgnoreCase)) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                line = line[(eq + 1)..].Trim();
            }
            else if (line.StartsWith('#')) continue;
            result.Add(Resolve(dir, line));
        }
        return result;
    }

    private static string Resolve(string dir, string entry)
    {
        if (IsUrl(entry)) return entry;
        if (entry.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return new Uri(entry).LocalPath;
        entry = entry.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(entry) ? entry : Path.GetFullPath(Path.Combine(dir, entry));
    }

    /// <summary>Développe une liste de chemins (fichiers, dossiers, listes de lecture) en fichiers lisibles, triés.</summary>
    public static List<string> Expand(IEnumerable<string> items)
    {
        var result = new List<string>();
        foreach (var item in items)
        {
            if (IsUrl(item)) { result.Add(item); continue; }
            if (Directory.Exists(item))
            {
                result.AddRange(Directory.EnumerateFiles(item, "*", SearchOption.AllDirectories)
                    .Where(IsMedia).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase));
            }
            else if (IsPlaylist(item) && File.Exists(item)) result.AddRange(ReadPlaylist(item));
            else if (File.Exists(item)) result.Add(item);
        }
        return result;
    }
}
