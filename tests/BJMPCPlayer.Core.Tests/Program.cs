using System.Diagnostics;
using System.Text;
using BJMPCPlayer.Core;
using BJMPCPlayer.Core.Mpv;

int checks = 0, fails = 0;
void Check(bool ok, string what) { checks++; if (!ok) { fails++; Console.WriteLine("ECHEC : " + what); } }
QueueItem I(string n) => new(n + ".mp3");

// ---------------------------------------------------------------- file d'attente
{
    var q = new PlayQueue(seed: 1);
    q.Set(new[] { I("a"), I("b"), I("c"), I("d"), I("e") }, 1);
    Check(q.Current?.Title == "b", "file : démarre sur l'élément demandé");
    Check(q.Next()?.Title == "c" && q.Next()?.Title == "d", "file : suivant");
    Check(q.Previous()?.Title == "c", "file : précédent");
    q.MoveTo(4);
    Check(q.Next(auto: true) == null, "file : fin de liste sans répétition");
    q.Repeat = RepeatMode.All;
    Check(q.Next(auto: true)?.Title == "a", "file : répéter tout reboucle");
    q.Repeat = RepeatMode.One;
    Check(q.Next(auto: true)?.Title == "a" && q.Next()?.Title == "b", "file : répéter un (le bouton Suivant avance quand même)");
    q.Repeat = RepeatMode.Off;

    q.PlayNext(new[] { I("x"), I("y") });
    Check(q.Next()?.Title == "x" && q.Next()?.Title == "y" && q.Next()?.Title == "c", "file : « lire ensuite » s'insère après le titre en cours");

    q.MoveTo(0);
    var cur = q.Current;
    q.Shuffle = true;
    Check(q.Current == cur, "aléatoire : le titre en cours reste le premier");
    var seen = new HashSet<string> { q.Current!.Title };
    for (int i = 1; i < q.Count; i++) seen.Add(q.Next()!.Title);
    Check(seen.Count == q.Count && q.Next(auto: true) == null, "aléatoire : chaque titre une seule fois, puis fin");
    q.Shuffle = false;

    q.Set(new[] { I("a"), I("b"), I("c") }, 1);
    q.RemoveAt(0);
    Check(q.Current?.Title == "b" && q.Count == 2, "file : retirer un titre avant le titre en cours");
    q.RemoveAt(q.CurrentIndex);
    Check(q.Current?.Title == "c", "file : retirer le titre en cours passe au suivant");
    q.Set(new[] { I("a"), I("b"), I("c") }, 2);
    q.Reorder(new[] { q.Items[2], q.Items[0], q.Items[1] });
    Check(q.Current?.Title == "c" && q.CurrentIndex == 0 && q.Next()?.Title == "a", "file : réorganisation par glisser-déposer");
}

// ---------------------------------------------------------------- listes de lecture
var tmp = Path.Combine(Path.GetTempPath(), "bjmpc-tests-" + Environment.ProcessId);
Directory.CreateDirectory(Path.Combine(tmp, "Album"));
{
    File.WriteAllText(Path.Combine(tmp, "liste.m3u8"), "\uFEFF#EXTM3U\n#EXTINF:123,Titre\nAlbum/01.flac\n\n" +
        Path.Combine(tmp, "abs.mp3") + "\nhttps://radio.example/flux\n", Encoding.UTF8);
    var l = MediaFormats.ReadPlaylist(Path.Combine(tmp, "liste.m3u8"));
    Check(l.Count == 3 && l[0] == Path.Combine(tmp, "Album", "01.flac") && l[2].StartsWith("https://"), "M3U : chemins relatifs, absolus et URL");
    File.WriteAllText(Path.Combine(tmp, "radio.pls"), "[playlist]\nFile1=http://a/b\nTitle1=x\nFile2=Album/02.mp3\nNumberOfEntries=2\n");
    var p = MediaFormats.ReadPlaylist(Path.Combine(tmp, "radio.pls"));
    Check(p.Count == 2 && p[1] == Path.Combine(tmp, "Album", "02.mp3"), "PLS lu");
    File.WriteAllText(Path.Combine(tmp, "Album", "b.flac"), "");
    File.WriteAllText(Path.Combine(tmp, "Album", "a.mkv"), "");
    File.WriteAllText(Path.Combine(tmp, "Album", "notes.txt"), "");
    var ex = MediaFormats.Expand(new[] { Path.Combine(tmp, "Album") });
    Check(ex.Count == 2 && ex[0].EndsWith("a.mkv"), "dossier développé, trié, sans fichiers non média");
}

// ---------------------------------------------------------------- réglages, formats
{
    AppSettings.Directory = Path.Combine(tmp, "cfg");
    var s = new AppSettings { ExclusiveAudio = true, Volume = 42, Repeat = RepeatMode.All };
    s.MusicFolders.Add(@"D:\Musique");
    for (int i = 0; i < 40; i++) s.AddRecent("f" + i);
    s.AddRecent("f5");
    s.Save();
    var r = AppSettings.Load();
    Check(r.ExclusiveAudio && r.Volume == 42 && r.Repeat == RepeatMode.All && r.MusicFolders[0] == @"D:\Musique", "réglages relus à l'identique");
    Check(r.Recent.Count == 30 && r.Recent[0] == "f5", "récents : 30 max, dernier en tête, sans doublon");
    File.WriteAllText(AppSettings.FilePath, "{ abîmé");
    Check(AppSettings.Load().Volume == 80, "réglages abîmés : valeurs par défaut");
    Check(TimeFormat.Clock(187) == "3:07" && TimeFormat.Clock(3725) == "1:02:05", "format de durée");
    var t = new MpvTrack(2, "audio", null, "fre", "ac3", true, false, false, 6);
    Check(t.Label == "Français — AC3 5.1", "libellé de piste : " + t.Label);
}

// ---------------------------------------------------------------- vraie lecture avec libmpv
if (!MpvPlayer.IsLibraryAvailable())
{
    Console.WriteLine("libmpv absente : tests de lecture ignorés");
}
else
{
    bool Run(string exe, string args)
    {
        try { using var pr = Process.Start(new ProcessStartInfo(exe, args) { RedirectStandardError = true, RedirectStandardOutput = true }); pr!.WaitForExit(); return pr.ExitCode == 0; }
        catch { return false; }
    }
    // fichier audio de 3 s généré sans dépendance
    var wav = Path.Combine(tmp, "la440.wav");
    {
        const int rate = 48000; int n = rate * 3;
        using var bw = new BinaryWriter(File.Create(wav));
        bw.Write("RIFF"u8); bw.Write(36 + n * 2); bw.Write("WAVEfmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16); bw.Write("data"u8); bw.Write(n * 2);
        for (int i = 0; i < n; i++) bw.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 12000));
    }

    using var mpv = new MpvPlayer(new MpvOptions { VideoOutput = false, AudioOutput = false, ResumePlayback = false });
    var loaded = new ManualResetEventSlim(); var ended = new ManualResetEventSlim(); var msg = new ManualResetEventSlim();
    MpvEndReason? reason = null; string[]? lastMsg = null;
    mpv.FileLoaded += () => loaded.Set();
    mpv.EndFile += (r, e) => { reason = r; if (r != MpvEndReason.Stop) ended.Set(); };
    mpv.ClientMessage += a => { lastMsg = a; msg.Set(); };

    Console.WriteLine("  libmpv : " + mpv.GetString("mpv-version") + " / FFmpeg " + mpv.GetString("ffmpeg-version"));
    mpv.Load(wav);
    Check(loaded.Wait(5000), "mpv : fichier chargé");
    Thread.Sleep(300);
    Check(Math.Abs(mpv.Duration - 3) < 0.05, $"mpv : durée lue ({mpv.Duration:0.00} s)");
    Check(!mpv.HasVideo(), "mpv : un fichier audio n'est pas une vidéo");
    var t0 = mpv.Position; Thread.Sleep(600);
    Check(mpv.Position > t0 + 0.3, $"mpv : la lecture avance ({t0:0.00} -> {mpv.Position:0.00})");
    mpv.Pause(); Thread.Sleep(200);
    var tp = mpv.Position; Thread.Sleep(400);
    Check(mpv.Paused && Math.Abs(mpv.Position - tp) < 0.01, "mpv : pause");
    mpv.Seek(2.0); mpv.Play(); Thread.Sleep(300);
    Check(mpv.Position >= 2.0 && !mpv.Paused, $"mpv : déplacement à 2 s ({mpv.Position:0.00})");
    mpv.SetVolume(55); Thread.Sleep(150);
    Check(Math.Abs(mpv.Volume - 55) < 0.01, "mpv : volume");
    Check(ended.Wait(4000) && reason == MpvEndReason.Eof, "mpv : fin de fichier signalée (EOF)");

    Check(mpv.Command("keybind", "SPACE", "script-message bjmpc espace") >= 0, "mpv : raccourci vers l'application accepté");
    mpv.Command("script-message", "bjmpc", "test");
    Check(msg.Wait(2000) && lastMsg is { Length: 2 } && lastMsg[1] == "test", "mpv : message client reçu");

    // vidéo avec 2 pistes audio (fr, en) + sous-titres, si ffmpeg est disponible
    var mkv = Path.Combine(tmp, "film.mkv");
    var srt = Path.Combine(tmp, "film.srt");
    File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:02,000\nBonjour\n");
    if (Run("ffmpeg", $"-v error -y -f lavfi -i testsrc=d=2:s=320x240 -f lavfi -i sine=f=440:d=2 -f lavfi -i sine=f=880:d=2 -i \"{srt}\" " +
                      $"-map 0 -map 1 -map 2 -map 3 -c:v mpeg4 -c:a aac -c:s srt -metadata:s:a:0 language=fre -metadata:s:a:1 language=eng " +
                      $"-metadata:s:s:0 language=fre -metadata title=\"Essai BJMPC\" \"{mkv}\""))
    {
        loaded.Reset();
        mpv.Load(mkv);
        Check(loaded.Wait(5000), "mpv : vidéo chargée");
        Thread.Sleep(200);
        var tr = mpv.Tracks();
        Check(mpv.HasVideo(), "mpv : vidéo détectée");
        Check(tr.Count(x => x.Type == "audio") == 2 && tr.Count(x => x.Type == "sub" && !x.External) == 1, $"mpv : pistes ({tr.Count})");
        Check(tr.Any(x => x.Type == "sub" && x.External), "mpv : sous-titres .srt voisins chargés automatiquement");
        var sel = tr.First(x => x.Type == "audio" && x.Selected);
        Check(sel.Language == "fre", "mpv : piste française choisie par défaut");
        var en = tr.First(x => x.Type == "audio" && x.Language == "eng");
        mpv.SelectTrack("audio", en.Id); Thread.Sleep(200);
        Check(mpv.Tracks().First(x => x.Type == "audio" && x.Selected).Id == en.Id, "mpv : changement de piste audio");
        mpv.SelectTrack("sub", null); Thread.Sleep(150);
        Check(!mpv.Tracks().Any(x => x.Type == "sub" && x.Selected), "mpv : sous-titres désactivés");
        Check(mpv.MediaTitle == "Essai BJMPC" && mpv.Metadata("title") == "Essai BJMPC", "mpv : titre et métadonnées");
        Console.WriteLine("  pistes : " + string.Join(" | ", mpv.Tracks().Select(x => x.Type + ":" + x.Label)));
    }
    else Console.WriteLine("  ffmpeg absent : test vidéo ignoré");
}

try { Directory.Delete(tmp, true); } catch { }
Console.WriteLine($"{checks - fails}/{checks} vérifications réussies");
return fails == 0 ? 0 : 1;
