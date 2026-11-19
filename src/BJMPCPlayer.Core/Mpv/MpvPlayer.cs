// Pilotage haut niveau de libmpv : lecture, pistes, métadonnées, sortie WASAPI (exclusive possible)
using System.Globalization;
using System.Runtime.InteropServices;

namespace BJMPCPlayer.Core.Mpv;

public sealed class MpvOptions
{
    /// <summary>Fenêtre (HWND) dans laquelle mpv dessine la vidéo ; 0 = pas de vidéo intégrée.</summary>
    public long WindowHandle { get; set; }
    public bool VideoOutput { get; set; } = true;     // false : vo=null (tests)
    public bool AudioOutput { get; set; } = true;     // false : ao=null (tests)
    public bool Exclusive { get; set; }               // WASAPI exclusif
    public string AudioDevice { get; set; } = "auto";
    public bool HardwareDecoding { get; set; } = true;
    public bool ResumePlayback { get; set; } = true;
    public string? WatchLaterDirectory { get; set; }
    public string SubtitleLanguages { get; set; } = "fr,fre,fra,en,eng";
    public string AudioLanguages { get; set; } = "fr,fre,fra";
    public bool NormalizeVolume { get; set; }
    public double Volume { get; set; } = 80;
}

public sealed record MpvTrack(long Id, string Type, string? Title, string? Language, string? Codec,
                              bool Selected, bool AlbumArt, bool External, int Channels)
{
    /// <summary>Libellé lisible : « Français — AC-3 5.1 », « Anglais (forcés) »…</summary>
    public string Label
    {
        get
        {
            var parts = new List<string>();
            var lang = Languages.Name(Language);
            if (!string.IsNullOrEmpty(Title)) parts.Add(Title!);
            if (lang != null && (Title == null || !Title.Contains(lang, StringComparison.OrdinalIgnoreCase))) parts.Insert(0, lang);
            var tech = Codec?.ToUpperInvariant();
            if (Type == "audio" && Channels > 0) tech += " " + Channels switch { 1 => "mono", 2 => "stéréo", 6 => "5.1", 8 => "7.1", _ => Channels + " can." };
            if (!string.IsNullOrEmpty(tech)) parts.Add(tech!.Trim());
            if (External) parts.Add("externe");
            return parts.Count > 0 ? string.Join(" — ", parts) : $"Piste {Id}";
        }
    }
}

public sealed record AudioOutputDevice(string Name, string Description);

public sealed class MpvPlayer : IDisposable
{
    private IntPtr _ctx;
    private readonly Thread _thread;
    private volatile bool _disposing;
    private readonly List<(string Name, MpvFormat Format)> _observed = new();

    public event Action<string, object?>? PropertyChanged;
    public event Action? FileLoaded;
    public event Action? FileStarted;
    public event Action<MpvEndReason, string?>? EndFile;
    public event Action<string[]>? ClientMessage;
    public event Action? PlaybackRestarted;

    public double Position { get; private set; }
    public double Duration { get; private set; }
    public bool Paused { get; private set; }
    public double Volume { get; private set; }
    public bool Muted { get; private set; }
    public double Speed { get; private set; } = 1;
    public string? MediaTitle { get; private set; }
    public bool Idle { get; private set; } = true;

    public static bool IsLibraryAvailable() => LibMpv.IsAvailable();

    public MpvPlayer(MpvOptions o)
    {
        _ctx = LibMpv.mpv_create();
        if (_ctx == IntPtr.Zero) throw new InvalidOperationException("libmpv : création impossible");

        Opt("terminal", "no");
        Opt("config", "no");
        Opt("msg-level", "all=warn");
        Opt("idle", "yes");
        Opt("keep-open", "no");
        Opt("title", "BJMPC Player");
        // pas d'interface ni de raccourcis mpv : c'est l'application qui pilote
        Opt("osc", "no");
        Opt("osd-bar", "no");
        Opt("input-default-bindings", "no");
        Opt("input-vo-keyboard", o.WindowHandle != 0 ? "yes" : "no");
        Opt("drag-and-drop", "no");
        Opt("cursor-autohide", "1500");
        Opt("audio-display", "no");             // les pochettes sont affichées par l'interface
        if (o.WindowHandle != 0)
        {
            Opt("wid", o.WindowHandle.ToString(CultureInfo.InvariantCulture));
            Opt("force-window", "yes");
        }
        Opt("vo", o.VideoOutput ? "gpu-next,gpu,direct3d" : "null");
        Opt("hwdec", o.HardwareDecoding ? "auto-safe" : "no");
        Opt("ao", o.AudioOutput ? (OperatingSystem.IsWindows() ? "wasapi" : "") : "null");
        Opt("audio-exclusive", o.Exclusive ? "yes" : "no");
        Opt("audio-device", string.IsNullOrEmpty(o.AudioDevice) ? "auto" : o.AudioDevice);
        Opt("volume-max", "150");
        Opt("volume", o.Volume.ToString(CultureInfo.InvariantCulture));
        Opt("slang", o.SubtitleLanguages);
        Opt("alang", o.AudioLanguages);
        Opt("sub-auto", "fuzzy");
        Opt("ytdl", "yes");
        // la sauvegarde de position est décidée par l'application (vidéos longues uniquement)
        Opt("save-position-on-quit", "no");
        Opt("resume-playback", o.ResumePlayback ? "yes" : "no");
        if (!string.IsNullOrEmpty(o.WatchLaterDirectory))
        {
            Directory.CreateDirectory(o.WatchLaterDirectory);
            Opt("watch-later-dir", o.WatchLaterDirectory);
        }
        if (o.NormalizeVolume) Opt("af", NormalizeFilter);

        int r = LibMpv.mpv_initialize(_ctx);
        if (r < 0) throw new InvalidOperationException("libmpv : initialisation impossible (" + LibMpv.ErrorString(r) + ")");

        Observe("time-pos", MpvFormat.Double);
        Observe("duration", MpvFormat.Double);
        Observe("pause", MpvFormat.Flag);
        Observe("volume", MpvFormat.Double);
        Observe("mute", MpvFormat.Flag);
        Observe("speed", MpvFormat.Double);
        Observe("media-title", MpvFormat.String);
        Observe("idle-active", MpvFormat.Flag);
        Observe("track-list/count", MpvFormat.Int64);
        Observe("aid", MpvFormat.String);
        Observe("sid", MpvFormat.String);
        Observe("vid", MpvFormat.String);
        Observe("paused-for-cache", MpvFormat.Flag);

        _thread = new Thread(EventLoop) { IsBackground = true, Name = "mpv-events" };
        _thread.Start();
    }

    public const string NormalizeFilter = "lavfi=[dynaudnorm=f=250:g=15]";

    // ------------------------------------------------------------------ commandes
    public void Load(string pathOrUrl, bool append = false) =>
        Command("loadfile", pathOrUrl, append ? "append-play" : "replace");

    public void Play() => SetFlag("pause", false);
    public void Pause() => SetFlag("pause", true);
    public void TogglePause() => SetFlag("pause", !Paused);
    public void Stop() => Command("stop");
    public void Seek(double seconds, bool absolute = true) =>
        Command("seek", seconds.ToString(CultureInfo.InvariantCulture), absolute ? "absolute" : "relative");
    public void SetVolume(double v) => SetDouble("volume", Math.Clamp(v, 0, 150));
    public void SetMute(bool m) => SetFlag("mute", m);
    public void SetSpeed(double s) => SetDouble("speed", Math.Clamp(s, 0.25, 4));
    public void SaveResumePoint() => Command("write-watch-later-config");
    public void AddSubtitle(string path) => Command("sub-add", path, "select");

    /// <summary>Choisit une piste ; id = null pour la désactiver (« no »).</summary>
    public void SelectTrack(string type, long? id)
    {
        var prop = type switch { "audio" => "aid", "sub" => "sid", "video" => "vid", _ => throw new ArgumentException(type) };
        SetString(prop, id?.ToString(CultureInfo.InvariantCulture) ?? "no");
    }

    /// <summary>Associe une touche ou un geste souris de la fenêtre vidéo à un message pour l'application.</summary>
    public void BindToMessage(string key, string message) => Command("keybind", key, "script-message bjmpc " + message);

    public void ApplyAudioSettings(bool exclusive, string device, bool normalize)
    {
        SetString("audio-exclusive", exclusive ? "yes" : "no");
        SetString("audio-device", string.IsNullOrEmpty(device) ? "auto" : device);
        SetString("af", normalize ? NormalizeFilter : "");
        Command("ao-reload");
    }

    public void SetHardwareDecoding(bool on) => SetString("hwdec", on ? "auto-safe" : "no");

    public int Command(params string[] args)
    {
        var ptrs = new IntPtr[args.Length + 1];
        try
        {
            for (int i = 0; i < args.Length; i++) ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            return LibMpv.mpv_command(_ctx, ptrs);
        }
        finally
        {
            for (int i = 0; i < args.Length; i++) if (ptrs[i] != IntPtr.Zero) Marshal.FreeCoTaskMem(ptrs[i]);
        }
    }

    // ------------------------------------------------------------------ propriétés
    public string? GetString(string name)
    {
        var p = LibMpv.mpv_get_property_string(_ctx, name);
        if (p == IntPtr.Zero) return null;
        try { return LibMpv.PtrToUtf8(p); } finally { LibMpv.mpv_free(p); }
    }

    public double? GetDouble(string name) =>
        LibMpv.mpv_get_property(_ctx, name, MpvFormat.Double, out double v) >= 0 ? v : null;

    public long? GetLong(string name) =>
        LibMpv.mpv_get_property_long(_ctx, name, MpvFormat.Int64, out long v) >= 0 ? v : null;

    public bool? GetFlag(string name) =>
        LibMpv.mpv_get_property_int(_ctx, name, MpvFormat.Flag, out int v) >= 0 ? v != 0 : null;

    public int SetString(string name, string value) => LibMpv.mpv_set_property_string(_ctx, name, value);
    public int SetDouble(string name, double value) => LibMpv.mpv_set_property(_ctx, name, MpvFormat.Double, ref value);
    public int SetFlag(string name, bool value) { int v = value ? 1 : 0; return LibMpv.mpv_set_property_int(_ctx, name, MpvFormat.Flag, ref v); }

    public string? Metadata(string key) => GetString("metadata/by-key/" + key);

    public IReadOnlyList<MpvTrack> Tracks()
    {
        var list = new List<MpvTrack>();
        long n = GetLong("track-list/count") ?? 0;
        for (long i = 0; i < n; i++)
        {
            string b = "track-list/" + i + "/";
            list.Add(new MpvTrack(
                GetLong(b + "id") ?? 0,
                GetString(b + "type") ?? "",
                GetString(b + "title"),
                GetString(b + "lang"),
                GetString(b + "codec"),
                GetFlag(b + "selected") ?? false,
                GetFlag(b + "albumart") ?? false,
                GetFlag(b + "external") ?? false,
                (int)(GetLong(b + "demux-channel-count") ?? 0)));
        }
        return list;
    }

    /// <summary>Le fichier en cours contient-il une vraie vidéo (et pas seulement une pochette) ?</summary>
    public bool HasVideo() => Tracks().Any(t => t.Type == "video" && !t.AlbumArt);

    public IReadOnlyList<AudioOutputDevice> AudioDevices()
    {
        var list = new List<AudioOutputDevice>();
        long n = GetLong("audio-device-list/count") ?? 0;
        for (long i = 0; i < n; i++)
        {
            var name = GetString($"audio-device-list/{i}/name");
            var desc = GetString($"audio-device-list/{i}/description");
            if (name != null) list.Add(new AudioOutputDevice(name, desc ?? name));
        }
        return list;
    }

    private void Opt(string name, string value) => LibMpv.mpv_set_option_string(_ctx, name, value);

    private void Observe(string name, MpvFormat format)
    {
        _observed.Add((name, format));
        LibMpv.mpv_observe_property(_ctx, (ulong)_observed.Count, name, format);
    }

    // ------------------------------------------------------------------ évènements
    private void EventLoop()
    {
        while (!_disposing)
        {
            var p = LibMpv.mpv_wait_event(_ctx, 0.5);
            if (p == IntPtr.Zero) continue;
            var ev = Marshal.PtrToStructure<MpvEvent>(p);
            try { Dispatch(ev); }
            catch { /* une erreur d'un abonné ne doit pas arrêter la boucle */ }
            if (ev.EventId == MpvEventId.Shutdown) break;
        }
    }

    private void Dispatch(MpvEvent ev)
    {
        switch (ev.EventId)
        {
            case MpvEventId.PropertyChange:
            {
                var pe = Marshal.PtrToStructure<MpvEventProperty>(ev.Data);
                var name = LibMpv.PtrToUtf8(pe.Name) ?? "";
                object? value = null;
                if (pe.Data != IntPtr.Zero)
                {
                    value = pe.Format switch
                    {
                        MpvFormat.Double => Marshal.PtrToStructure<double>(pe.Data),
                        MpvFormat.Flag => Marshal.ReadInt32(pe.Data) != 0,
                        MpvFormat.Int64 => Marshal.ReadInt64(pe.Data),
                        MpvFormat.String => LibMpv.PtrToUtf8(Marshal.ReadIntPtr(pe.Data)),
                        _ => null,
                    };
                }
                switch (name)
                {
                    case "time-pos": Position = value as double? ?? 0; break;
                    case "duration": Duration = value as double? ?? 0; break;
                    case "pause": Paused = value as bool? ?? false; break;
                    case "volume": Volume = value as double? ?? 0; break;
                    case "mute": Muted = value as bool? ?? false; break;
                    case "speed": Speed = value as double? ?? 1; break;
                    case "media-title": MediaTitle = value as string; break;
                    case "idle-active": Idle = value as bool? ?? false; break;
                }
                PropertyChanged?.Invoke(name, value);
                break;
            }
            case MpvEventId.StartFile:
                FileStarted?.Invoke();
                break;
            case MpvEventId.FileLoaded:
                FileLoaded?.Invoke();
                break;
            case MpvEventId.PlaybackRestart:
                PlaybackRestarted?.Invoke();
                break;
            case MpvEventId.EndFile:
            {
                var ef = Marshal.PtrToStructure<MpvEventEndFile>(ev.Data);
                EndFile?.Invoke(ef.Reason, ef.Reason == MpvEndReason.Error ? LibMpv.ErrorString(ef.Error) : null);
                break;
            }
            case MpvEventId.ClientMessage:
            {
                var cm = Marshal.PtrToStructure<MpvEventClientMessage>(ev.Data);
                var args = new string[cm.NumArgs];
                for (int i = 0; i < cm.NumArgs; i++) args[i] = LibMpv.PtrToUtf8(Marshal.ReadIntPtr(cm.Args, i * IntPtr.Size)) ?? "";
                ClientMessage?.Invoke(args);
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_ctx == IntPtr.Zero) return;
        _disposing = true;
        LibMpv.mpv_wakeup(_ctx);
        _thread.Join(2000);
        LibMpv.mpv_terminate_destroy(_ctx);
        _ctx = IntPtr.Zero;
    }
}
