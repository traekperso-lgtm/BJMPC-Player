using System.Reflection;
using BJMPCPlayer.Core.Mpv;
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
        var devices = new List<AudioOutputDevice> { new("auto", "Automatique (sortie par défaut de Windows)") };
        if (_p.Mpv != null) devices.AddRange(_p.Mpv.AudioDevices().Where(d => d.Name != "auto"));
        DeviceBox.ItemsSource = devices;
        DeviceBox.SelectedItem = devices.FirstOrDefault(d => d.Name == s.AudioDevice) ?? devices[0];
        ExclusiveSwitch.IsOn = s.ExclusiveAudio;
        NormalizeSwitch.IsOn = s.NormalizeVolume;
        HwdecSwitch.IsOn = s.HardwareDecoding;
        ResumeSwitch.IsOn = s.ResumePlayback;
        AudioLangBox.Text = s.AudioLanguages;
        SubLangBox.Text = s.SubtitleLanguages;
        ThemeChoice.SelectedIndex = s.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        RefreshFolders();
        Spatial3DCard.Visibility = File.Exists(Spatial3DPath) ? Visibility.Visible : Visibility.Collapsed;
        ExclusiveNote.Visibility = PlayerService.Spatial3DRunning ? Visibility.Visible : Visibility.Collapsed;
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

    private void DeviceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || DeviceBox.SelectedItem is not AudioOutputDevice d) return;
        _p.Settings.AudioDevice = d.Name;
        _p.ApplyAudioSettings();
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
