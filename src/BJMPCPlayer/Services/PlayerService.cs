using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BJMPCPlayer.Core;
using BJMPCPlayer.Core.Mpv;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace BJMPCPlayer.Services;

/// <summary>
/// Cœur de l'application : relie libmpv, la file d'attente et les réglages, et expose un état
/// observable (titre, position, lecture/pause…) pour l'interface.
/// </summary>
public sealed class PlayerService : INotifyPropertyChanged
{
    public static PlayerService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? Error;
    public event Action<string>? VideoMessage;   // touches / souris sur la fenêtre vidéo
    public event Action? MediaChanged;

    public AppSettings Settings { get; } = AppSettings.Load();
    public PlayQueue Queue { get; } = new();
    public ObservableCollection<QueueItem> QueueItems { get; } = new();
    public MpvPlayer? Mpv { get; private set; }

    private DispatcherQueue? _ui;
    private DateTime _lastPosUpdate = DateTime.MinValue;

    private PlayerService()
    {
        Queue.Shuffle = Settings.Shuffle;
        Queue.Repeat = Settings.Repeat;
    }

    // ------------------------------------------------------------------ état observable
    private string _title = "Aucune lecture";
    public string Title { get => _title; private set => Set(ref _title, value); }
    private string _subtitle = "";
    public string Subtitle { get => _subtitle; private set => Set(ref _subtitle, value); }
    private string _album = "";
    public string Album { get => _album; private set => Set(ref _album, value); }
    private double _position;
    public double Position { get => _position; private set => Set(ref _position, value); }
    private double _duration;
    public double Duration { get => _duration; private set => Set(ref _duration, value); }
    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; private set => Set(ref _isPlaying, value); }
    private bool _hasMedia;
    public bool HasMedia { get => _hasMedia; private set => Set(ref _hasMedia, value); }
    private bool _hasVideo;
    public bool HasVideo { get => _hasVideo; private set => Set(ref _hasVideo, value); }
    private bool _buffering;
    public bool Buffering { get => _buffering; private set => Set(ref _buffering, value); }
    private double _volume;
    public double Volume { get => _volume; private set => Set(ref _volume, value); }
    private bool _muted;
    public bool Muted { get => _muted; private set => Set(ref _muted, value); }
    private double _speed = 1;
    public double Speed { get => _speed; private set => Set(ref _speed, value); }
    private ImageSource? _cover;
    public ImageSource? Cover { get => _cover; private set => Set(ref _cover, value); }
    private int _currentIndex = -1;
    public int CurrentIndex { get => _currentIndex; private set => Set(ref _currentIndex, value); }
    public bool Shuffle => Queue.Shuffle;
    public RepeatMode Repeat => Queue.Repeat;
    public IReadOnlyList<MpvTrack> Tracks { get; private set; } = Array.Empty<MpvTrack>();

    // ------------------------------------------------------------------ démarrage / arrêt
    public bool Initialize(IntPtr videoWindow, DispatcherQueue ui)
    {
        _ui = ui;
        if (!MpvPlayer.IsLibraryAvailable())
        {
            Error?.Invoke("libmpv-2.dll est introuvable à côté de BJMPCPlayer.exe : la lecture est impossible. Réinstallez avec build-windows.bat.");
            return false;
        }
        try
        {
            Mpv = new MpvPlayer(new MpvOptions
            {
                WindowHandle = videoWindow.ToInt64(),
                Exclusive = Settings.ExclusiveAudio,
                AudioDevice = Settings.AudioDevice,
                HardwareDecoding = Settings.HardwareDecoding,
                ResumePlayback = Settings.ResumePlayback,
                WatchLaterDirectory = AppSettings.WatchLaterDirectory,
                SubtitleLanguages = Settings.SubtitleLanguages,
                AudioLanguages = Settings.AudioLanguages,
                NormalizeVolume = Settings.NormalizeVolume,
                Volume = Settings.Volume,
            });
        }
        catch (Exception ex)
        {
            Error?.Invoke("Le moteur de lecture n'a pas démarré : " + ex.Message);
            return false;
        }
        Mpv.SetMute(Settings.Muted);
        Volume = Settings.Volume;
        Muted = Settings.Muted;

        // Touches et souris au-dessus de la vidéo : renvoyées à l'application
        foreach (var (key, msg) in new[]
        {
            ("SPACE", "pause"), ("MBTN_LEFT", "click"), ("MBTN_LEFT_DBL", "fullscreen"), ("f", "fullscreen"), ("F11", "fullscreen"),
            ("ESC", "escape"), ("LEFT", "back"), ("RIGHT", "forward"), ("UP", "volup"), ("DOWN", "voldown"),
            ("WHEEL_UP", "volup"), ("WHEEL_DOWN", "voldown"), ("m", "mute"), ("n", "next"), ("p", "previous"),
            ("MOUSE_MOVE", "mousemove"), ("MBTN_RIGHT", "click"), ("NEXT", "next"), ("PREV", "previous"),
            ("PLAYPAUSE", "pause"), ("PLAY", "pause"), ("PAUSE", "pause"), ("STOP", "stop"),
        })
            Mpv.BindToMessage(key, msg);

        Mpv.PropertyChanged += OnMpvProperty;
        Mpv.FileLoaded += () => Ui(OnFileLoaded);
        Mpv.EndFile += (reason, err) => Ui(() => OnEndFile(reason, err));
        Mpv.ClientMessage += a => { if (a.Length >= 2 && a[0] == "bjmpc") Ui(() => VideoMessage?.Invoke(a[1])); };
        return true;
    }

    public void Shutdown()
    {
        SaveResumeIfUseful();
        Settings.Volume = Volume;
        Settings.Muted = Muted;
        Settings.Shuffle = Queue.Shuffle;
        Settings.Repeat = Queue.Repeat;
        Settings.Save();
        Mpv?.Dispose();
        Mpv = null;
    }

    // ------------------------------------------------------------------ commandes
    /// <summary>Remplace la file par ces éléments (fichiers, dossiers, listes M3U, URL) et lance la lecture.</summary>
    public void Play(IEnumerable<string> pathsOrFolders, int start = 0)
    {
        var files = MediaFormats.Expand(pathsOrFolders);
        if (files.Count == 0) { Error?.Invoke("Aucun fichier lisible."); return; }
        PlayItems(files.Select(f => new QueueItem(f)), start);
    }

    public void PlayItems(IEnumerable<QueueItem> items, int start = 0)
    {
        Queue.Set(items, start);
        SyncQueue();
        PlayCurrent();
    }

    public void Enqueue(IEnumerable<string> pathsOrFolders, bool playNext = false)
    {
        var items = MediaFormats.Expand(pathsOrFolders).Select(f => new QueueItem(f)).ToList();
        if (items.Count == 0) return;
        bool wasEmpty = Queue.Count == 0;
        if (playNext) Queue.PlayNext(items); else Queue.Add(items);
        SyncQueue();
        if (wasEmpty) PlayCurrent();
    }

    public void PlayIndex(int index) { if (Queue.MoveTo(index) != null) PlayCurrent(); }

    public void Next()
    {
        if (Queue.Count == 0) return;
        var before = Queue.CurrentIndex;
        var item = Queue.Next();
        if (item != null && (Queue.CurrentIndex != before || Queue.Repeat != RepeatMode.Off)) PlayCurrent();
    }

    public void Previous()
    {
        if (Position > 3) { Seek(0); return; }
        if (Queue.Previous() != null) PlayCurrent();
    }

    public void TogglePlay()
    {
        if (Mpv == null) return;
        if (!HasMedia && Queue.Current != null) { PlayCurrent(); return; }
        Mpv.TogglePause();
    }

    public void Stop()
    {
        SaveResumeIfUseful();
        Mpv?.Stop();
        HasMedia = false; HasVideo = false; IsPlaying = false;
        Title = "Aucune lecture"; Subtitle = ""; Album = ""; Cover = null; Position = 0; Duration = 0;
        MediaChanged?.Invoke();
    }

    public void Seek(double seconds) => Mpv?.Seek(Math.Clamp(seconds, 0, Math.Max(0, Duration - 0.2)));
    public void SeekRelative(double delta) => Mpv?.Seek(delta, absolute: false);

    public void SetVolume(double v)
    {
        v = Math.Clamp(v, 0, 150);
        Mpv?.SetVolume(v);
        Volume = v;
        if (Muted && v > 0) ToggleMute();
    }

    public void ToggleMute() { Muted = !Muted; Mpv?.SetMute(Muted); }
    public void SetSpeed(double s) { Mpv?.SetSpeed(s); }

    public void ToggleShuffle()
    {
        Queue.Shuffle = !Queue.Shuffle;
        Settings.Shuffle = Queue.Shuffle;
        OnChanged(nameof(Shuffle));
    }

    public void CycleRepeat()
    {
        Queue.Repeat = Queue.Repeat switch { RepeatMode.Off => RepeatMode.All, RepeatMode.All => RepeatMode.One, _ => RepeatMode.Off };
        Settings.Repeat = Queue.Repeat;
        OnChanged(nameof(Repeat));
    }

    public void SelectTrack(string type, long? id)
    {
        Mpv?.SelectTrack(type, id);
        Tracks = Mpv?.Tracks() ?? Tracks;
        OnChanged(nameof(Tracks));
    }

    public void AddSubtitle(string path) { Mpv?.AddSubtitle(path); Tracks = Mpv?.Tracks() ?? Tracks; OnChanged(nameof(Tracks)); }

    public void RemoveFromQueue(int index)
    {
        bool wasCurrent = index == Queue.CurrentIndex;
        Queue.RemoveAt(index);
        SyncQueue();
        if (wasCurrent) { if (Queue.Current != null) PlayCurrent(); else Stop(); }
    }

    public void ClearQueue() { Queue.Clear(); SyncQueue(); Stop(); }

    public void QueueReordered()
    {
        Queue.Reorder(QueueItems.ToList());
        CurrentIndex = Queue.CurrentIndex;
    }

    public void ApplyAudioSettings()
    {
        Mpv?.ApplyAudioSettings(Settings.ExclusiveAudio, Settings.AudioDevice, Settings.NormalizeVolume);
        Settings.Save();
    }

    public void ApplyVideoSettings()
    {
        Mpv?.SetHardwareDecoding(Settings.HardwareDecoding);
        Mpv?.SetString("slang", Settings.SubtitleLanguages);
        Mpv?.SetString("alang", Settings.AudioLanguages);
        Mpv?.SetString("resume-playback", Settings.ResumePlayback ? "yes" : "no");
        Settings.Save();
    }

    // ------------------------------------------------------------------ interne
    private void PlayCurrent()
    {
        var item = Queue.Current;
        if (item == null || Mpv == null) return;
        SaveResumeIfUseful();
        CurrentIndex = Queue.CurrentIndex;
        Title = item.Title;
        Subtitle = item.Artist ?? "";
        Album = "";
        Cover = null;
        HasMedia = true;
        Position = 0;
        Mpv.Load(item.Path);
        Mpv.Play();
        if (!MediaFormats.IsUrl(item.Path)) Settings.AddRecent(item.Path);
    }

    /// <summary>Mémorise la position des vidéos longues (reprise au prochain lancement).</summary>
    private void SaveResumeIfUseful()
    {
        if (Mpv == null || !HasMedia || !HasVideo || !Settings.ResumePlayback) return;
        if (Duration > 300 && Position > 30 && Duration - Position > 60) Mpv.SaveResumePoint();
    }

    private void SyncQueue()
    {
        QueueItems.Clear();
        foreach (var i in Queue.Items) QueueItems.Add(i);
        CurrentIndex = Queue.CurrentIndex;
    }

    private async void OnFileLoaded()
    {
        if (Mpv == null) return;
        var item = Queue.Current;
        Tracks = Mpv.Tracks();
        OnChanged(nameof(Tracks));
        HasVideo = Tracks.Any(t => t.Type == "video" && !t.AlbumArt);
        Duration = Mpv.GetDouble("duration") ?? 0;
        var title = Mpv.Metadata("title");
        var artist = Mpv.Metadata("artist") ?? Mpv.Metadata("album_artist");
        var album = Mpv.Metadata("album");
        Title = !string.IsNullOrWhiteSpace(title) ? title! : Mpv.MediaTitle ?? item?.Title ?? "";
        Subtitle = artist ?? item?.Artist ?? (HasVideo ? VideoInfo() : "");
        Album = album ?? "";
        if (item != null)
        {
            item.Title = Title;
            item.Artist = artist ?? item.Artist;
            item.Duration = Duration;
        }
        MediaChanged?.Invoke();
        Cover = (!HasVideo && item != null && !MediaFormats.IsUrl(item.Path)) ? await LoadThumbnailAsync(item.Path, ThumbnailMode.MusicView, 512) : null;
    }

    private string VideoInfo()
    {
        if (Mpv == null) return "";
        var w = Mpv.GetLong("width"); var h = Mpv.GetLong("height");
        var codec = Mpv.GetString("video-format")?.ToUpperInvariant();
        return w > 0 ? $"{w}×{h} · {codec}" : "";
    }

    private void OnEndFile(MpvEndReason reason, string? error)
    {
        if (reason == MpvEndReason.Error)
        {
            Error?.Invoke($"Lecture impossible : {Queue.Current?.Title} ({error})");
            var n = Queue.Next(auto: true);
            if (n != null) PlayCurrent(); else Stop();
            return;
        }
        if (reason != MpvEndReason.Eof) return;
        var next = Queue.Next(auto: true);
        if (next != null) PlayCurrent();
        else { IsPlaying = false; HasMedia = false; HasVideo = false; Position = 0; MediaChanged?.Invoke(); }
    }

    private void OnMpvProperty(string name, object? value)
    {
        if (name == "time-pos")
        {
            // limitée à ~8 mises à jour par seconde vers l'interface
            var now = DateTime.UtcNow;
            if ((now - _lastPosUpdate).TotalMilliseconds < 120) return;
            _lastPosUpdate = now;
        }
        Ui(() =>
        {
            switch (name)
            {
                case "time-pos": Position = value as double? ?? 0; break;
                case "duration": Duration = value as double? ?? 0; break;
                case "pause": IsPlaying = !(value as bool? ?? true); break;
                case "volume": Volume = value as double? ?? Volume; break;
                case "mute": Muted = value as bool? ?? false; break;
                case "speed": Speed = value as double? ?? 1; break;
                case "paused-for-cache": Buffering = value as bool? ?? false; break;
                case "idle-active": if (value as bool? == true) IsPlaying = false; break;
                case "aid": case "sid": case "vid":
                    if (Mpv != null) { Tracks = Mpv.Tracks(); OnChanged(nameof(Tracks)); }
                    break;
            }
        });
    }

    public static async Task<ImageSource?> LoadThumbnailAsync(string path, ThumbnailMode mode, uint size)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumb = await file.GetThumbnailAsync(mode, size, ThumbnailOptions.UseCurrentScale);
            if (thumb == null || thumb.Size == 0) return null;
            if (mode == ThumbnailMode.MusicView && thumb.Type == ThumbnailType.Icon) return null;   // pas de vraie pochette
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(thumb);
            return bmp;
        }
        catch { return null; }
    }

    private void Ui(Action a)
    {
        if (_ui == null || _ui.HasThreadAccess) a();
        else _ui.TryEnqueue(() => a());
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged(name);
    }

    private void OnChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
