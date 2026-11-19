using System.ComponentModel;
using System.Text.Json;
using BJMPCPlayer.Core;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace BJMPCPlayer.Services;

/// <summary>Objet de bibliothèque dont la vignette (pochette, image de vidéo) se charge à la demande.</summary>
public abstract class LibraryItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private ImageSource? _cover;
    private bool _coverRequested;
    protected abstract string CoverPath { get; }
    protected abstract ThumbnailMode CoverMode { get; }

    /// <summary>La première lecture déclenche le chargement asynchrone de la vignette.</summary>
    public ImageSource? Cover
    {
        get
        {
            if (!_coverRequested) { _coverRequested = true; _ = LoadCoverAsync(); }
            return _cover;
        }
    }

    private async Task LoadCoverAsync()
    {
        _cover = await PlayerService.LoadThumbnailAsync(CoverPath, CoverMode, 256);
        if (_cover != null) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cover)));
    }
}

public sealed class Song : LibraryItem
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string AlbumArtist { get; set; } = "";
    public uint Track { get; set; }
    public uint Year { get; set; }
    public double Duration { get; set; }
    public long Stamp { get; set; }
    public string DurationText => TimeFormat.Clock(Duration);
    protected override string CoverPath => Path;
    protected override ThumbnailMode CoverMode => ThumbnailMode.MusicView;
}

public sealed class Album : LibraryItem
{
    public string Name { get; init; } = "";
    public string Artist { get; init; } = "";
    public List<Song> Songs { get; init; } = new();
    public string Details => $"{Artist} · {Songs.Count} titre{(Songs.Count > 1 ? "s" : "")}";
    protected override string CoverPath => Songs[0].Path;
    protected override ThumbnailMode CoverMode => ThumbnailMode.MusicView;
}

public sealed class VideoFile : LibraryItem
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public double Duration { get; set; }
    public string Details { get; set; } = "";
    protected override string CoverPath => Path;
    protected override ThumbnailMode CoverMode => ThumbnailMode.VideosView;
}

/// <summary>Analyse des dossiers Musique / Vidéos, avec cache des étiquettes (library.json).</summary>
public sealed class LibraryService
{
    public static LibraryService Instance { get; } = new();

    public List<Song> Songs { get; private set; } = new();
    public List<Album> Albums { get; private set; } = new();
    public List<VideoFile> Videos { get; private set; } = new();
    public bool MusicScanned { get; private set; }
    public bool VideosScanned { get; private set; }

    private static string CachePath => System.IO.Path.Combine(AppSettings.Directory, "bibliotheque.json");

    public static IEnumerable<string> MusicFolders(AppSettings s) =>
        s.MusicFolders.Count > 0 ? s.MusicFolders : new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) };

    public static IEnumerable<string> VideoFolders(AppSettings s) =>
        s.VideoFolders.Count > 0 ? s.VideoFolders : new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) };

    public async Task ScanMusicAsync(AppSettings settings, IProgress<int>? progress = null)
    {
        var files = await Task.Run(() => MusicFolders(settings).Where(Directory.Exists)
            .SelectMany(d => SafeEnumerate(d)).Where(MediaFormats.IsAudio).ToList());

        var cache = LoadCache();
        var result = new List<Song>();
        var todo = new List<string>();
        foreach (var f in files)
        {
            long stamp = File.GetLastWriteTimeUtc(f).Ticks;
            if (cache.TryGetValue(f, out var c) && c.Stamp == stamp) result.Add(c);
            else todo.Add(f);
        }
        int done = 0;
        using var gate = new SemaphoreSlim(8);
        var tasks = todo.Select(async f =>
        {
            await gate.WaitAsync();
            try
            {
                var song = await ReadSongAsync(f);
                lock (result) result.Add(song);
                if (Interlocked.Increment(ref done) % 50 == 0) progress?.Report(done);
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);

        Songs = result.OrderBy(s => s.Artist, StringComparer.CurrentCultureIgnoreCase)
                      .ThenBy(s => s.Album, StringComparer.CurrentCultureIgnoreCase)
                      .ThenBy(s => s.Track).ThenBy(s => s.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        Albums = Songs.GroupBy(s => (Album: s.Album, Artist: string.IsNullOrEmpty(s.AlbumArtist) ? s.Artist : s.AlbumArtist))
                      .Select(g => new Album { Name = g.Key.Album.Length > 0 ? g.Key.Album : "Album inconnu", Artist = g.Key.Artist, Songs = g.OrderBy(s => s.Track).ToList() })
                      .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        MusicScanned = true;
        SaveCache(Songs);
    }

    public async Task ScanVideosAsync(AppSettings settings)
    {
        var files = await Task.Run(() => VideoFolders(settings).Where(Directory.Exists)
            .SelectMany(d => SafeEnumerate(d)).Where(MediaFormats.IsVideo)
            .OrderByDescending(File.GetLastWriteTimeUtc).ToList());
        var list = files.Select(f => new VideoFile { Path = f, Name = System.IO.Path.GetFileNameWithoutExtension(f) }).ToList();
        using var gate = new SemaphoreSlim(8);
        await Task.WhenAll(list.Select(async v =>
        {
            await gate.WaitAsync();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(v.Path);
                var vp = await file.Properties.GetVideoPropertiesAsync();
                v.Duration = vp.Duration.TotalSeconds;
                v.Details = (vp.Width > 0 ? $"{vp.Width}×{vp.Height} · " : "") + TimeFormat.Clock(v.Duration);
            }
            catch { v.Details = System.IO.Path.GetExtension(v.Path).TrimStart('.').ToUpperInvariant(); }
            finally { gate.Release(); }
        }));
        Videos = list;
        VideosScanned = true;
    }

    private static async Task<Song> ReadSongAsync(string path)
    {
        var song = new Song
        {
            Path = path,
            Title = System.IO.Path.GetFileNameWithoutExtension(path),
            Stamp = File.GetLastWriteTimeUtc(path).Ticks,
        };
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            var mp = await file.Properties.GetMusicPropertiesAsync();
            if (!string.IsNullOrWhiteSpace(mp.Title)) song.Title = mp.Title;
            song.Artist = mp.Artist ?? "";
            song.Album = mp.Album ?? "";
            song.AlbumArtist = mp.AlbumArtist ?? "";
            song.Track = mp.TrackNumber;
            song.Year = mp.Year;
            song.Duration = mp.Duration.TotalSeconds;
        }
        catch { /* fichier illisible par Windows : on garde le nom */ }
        if (song.Artist.Length == 0) song.Artist = "Artiste inconnu";
        return song;
    }

    private static IEnumerable<string> SafeEnumerate(string dir)
    {
        var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
        try { return Directory.EnumerateFiles(dir, "*", opts).ToList(); }
        catch { return Array.Empty<string>(); }
    }

    private sealed record CachedSong(string Path, string Title, string Artist, string Album, string AlbumArtist, uint Track, uint Year, double Duration, long Stamp);

    private static Dictionary<string, Song> LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return new();
            var list = JsonSerializer.Deserialize<List<CachedSong>>(File.ReadAllText(CachePath)) ?? new();
            return list.ToDictionary(c => c.Path, c => new Song
            {
                Path = c.Path, Title = c.Title, Artist = c.Artist, Album = c.Album, AlbumArtist = c.AlbumArtist,
                Track = c.Track, Year = c.Year, Duration = c.Duration, Stamp = c.Stamp,
            }, StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(); }
    }

    private static void SaveCache(List<Song> songs)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Directory);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(songs.Select(s =>
                new CachedSong(s.Path, s.Title, s.Artist, s.Album, s.AlbumArtist, s.Track, s.Year, s.Duration, s.Stamp))));
        }
        catch { }
    }
}
