using System.Reflection;
using BJMPCPlayer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BJMPCPlayer.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly PlayerService _p = PlayerService.Instance;
    private bool _loading;

    private static string Spatial3DPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spatial3D", "Spatial3D.exe");

    public SettingsPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _loading = true;
        var s = _p.Settings;
        ExclusiveSwitch.IsOn = s.ExclusiveAudio;
        RefreshAudioOutput();
        _p.PropertyChanged += Player_PropertyChanged;
        NormalizeSwitch.IsOn = s.NormalizeVolume;
        HwdecSwitch.IsOn = s.HardwareDecoding;
        ResumeSwitch.IsOn = s.ResumePlayback;
        AudioLangBox.Text = s.AudioLanguages;
        SubLangBox.Text = s.SubtitleLanguages;
        ThemeChoice.SelectedIndex = s.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        RefreshFolders();
        Spatial3DCard.Visibility = File.Exists(Spatial3DPath) ? Visibility.Visible : Visibility.Collapsed;
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        AboutTitle.Text = $"BJMPC Player {v?.Major}.{v?.Minor}.{v?.Build} — BJMPC Informatique";
        AboutEngine.Text = _p.Mpv != null ? $"{_p.Mpv.GetString("mpv-version")} · FFmpeg {_p.Mpv.GetString("ffmpeg-version")}" : "Moteur de lecture non chargé";
        _loading = false;
    }

    private void RefreshFolders()
    {
        MusicFolders.ItemsSource = LibraryService.MusicFolders(_p.Settings).ToList();
        VideoFolders.ItemsSource = LibraryService.VideoFolders(_p.Settings).ToList();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => _p.PropertyChanged -= Player_PropertyChanged;

    private void Player_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerService.OutputName) or nameof(PlayerService.ExclusiveBlockedReason)) RefreshAudioOutput();
    }

    /// <summary>
    /// Pas de choix de périphérique : le lecteur joue sur la sortie de Windows. Le mode exclusif n'est
    /// proposé que si cette carte est libre ; sinon le son va sur la sortie en cours, en partagé.
    /// </summary>
    private void RefreshAudioOutput()
    {
        OutputText.Text = (string.IsNullOrEmpty(_p.OutputName) ? "La sortie par défaut de Windows" : $"« {_p.OutputName} », la sortie de Windows")
            + " : changez-la dans Windows (icône haut-parleur de la barre des tâches), le lecteur la suit.";
        var reason = _p.ExclusiveBlockedReason;
        ExclusiveCard.Visibility = reason == null ? Visibility.Visible : Visibility.Collapsed;
        ExclusiveBusyCard.Visibility = reason == null ? Visibility.Collapsed : Visibility.Visible;
        if (reason != null)
            ExclusiveBusyText.Text = char.ToUpper(reason[0]) + reason[1..] +
                ". La carte son est déjà utilisée : le lecteur envoie le son sur cette sortie en mode partagé, sans couper personne.";
    }

    private void Audio_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _p.Settings.ExclusiveAudio = ExclusiveSwitch.IsOn;
        _p.Settings.NormalizeVolume = NormalizeSwitch.IsOn;
        _p.ApplyAudioSettings();
    }

    private void Video_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _p.Settings.HardwareDecoding = HwdecSwitch.IsOn;
        _p.Settings.ResumePlayback = ResumeSwitch.IsOn;
        _p.ApplyVideoSettings();
    }

    private void Lang_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _p.Settings.AudioLanguages = AudioLangBox.Text.Trim();
        _p.Settings.SubtitleLanguages = SubLangBox.Text.Trim();
        _p.ApplyVideoSettings();
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _p.Settings.Theme = ThemeChoice.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
        _p.Settings.Save();
        MainWindow.Current?.ApplyTheme(_p.Settings.Theme);
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = await MainWindow.Current!.PickFolderPathAsync();
        if (path == null) return;
        var list = (sender as FrameworkElement)?.Tag as string == "video" ? _p.Settings.VideoFolders : _p.Settings.MusicFolders;
        if (!list.Contains(path, StringComparer.OrdinalIgnoreCase)) list.Add(path);
        _p.Settings.Save();
        RefreshFolders();
    }

    private void RemoveMusicFolder_Click(object sender, RoutedEventArgs e) => RemoveFolder(_p.Settings.MusicFolders, (sender as FrameworkElement)?.Tag as string);
    private void RemoveVideoFolder_Click(object sender, RoutedEventArgs e) => RemoveFolder(_p.Settings.VideoFolders, (sender as FrameworkElement)?.Tag as string);

    private void RemoveFolder(List<string> list, string? path)
    {
        if (path == null) return;
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _p.Settings.Save();
        RefreshFolders();
    }

    private void OpenSpatial3D_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Spatial3DPath) { UseShellExecute = true }); }
        catch (Exception ex) { MainWindow.Current?.ShowError("Spatial3D : " + ex.Message); }
    }
}
