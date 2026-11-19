using BJMPCPlayer.Core;
using BJMPCPlayer.Pages;
using BJMPCPlayer.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace BJMPCPlayer;

public sealed partial class MainWindow : Window
{
    public PlayerService Player => PlayerService.Instance;
    public static MainWindow? Current { get; private set; }

    private readonly IntPtr _hwnd;
    private readonly VideoHost _video;
    private FrameworkElement? _videoTarget;
    private bool _fullScreen, _mini, _modal, _overTransport, _seekDragging, _updatingSeek;
    private SizeInt32 _restoreSize;
    private readonly DispatcherQueueTimer _hideTimer;

    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "BJMPCPlayer.ico"));
        AppWindow.Resize(new SizeInt32(1320, 860));

        _video = new VideoHost(_hwnd);
        Player.Error += ShowError;
        Player.VideoMessage += OnVideoMessage;
        Player.MediaChanged += OnMediaChanged;
        Player.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PlayerService.Position)) UpdateSeek();
            if (e.PropertyName == nameof(PlayerService.HasVideo)) PlaceVideo();
        };
        Player.Initialize(_video.Handle, DispatcherQueue);

        _hideTimer = DispatcherQueue.CreateTimer();
        _hideTimer.Interval = TimeSpan.FromSeconds(2.5);
        _hideTimer.Tick += (s, e) =>
        {
            _hideTimer.Stop();
            if ((_fullScreen || _mini) && !_overTransport && Player.IsPlaying && Player.HasVideo) ShowTransport(false);
        };

        Root.LayoutUpdated += (s, e) => PlaceVideo();
        SeekSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((s, e) => _seekDragging = true), true);
        SeekSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(SeekRelease), true);
        SeekSlider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(SeekRelease), true);

        ApplyTheme(Player.Settings.Theme);
        Root.ActualThemeChanged += (s, e) => UpdateCaptionButtons();
        Closed += (s, e) => { Player.Shutdown(); _video.Dispose(); };

        Nav.SelectedItem = Nav.MenuItems[0];
    }

    // =========================================================================================
    //  Navigation et vidéo
    // =========================================================================================
    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected) { ContentFrame.Navigate(typeof(SettingsPage)); return; }
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        var page = tag switch
        {
            "now" => typeof(NowPlayingPage),
            "music" => typeof(MusicPage),
            "videos" => typeof(VideosPage),
            "queue" => typeof(QueuePage),
            _ => typeof(HomePage),
        };
        if (ContentFrame.CurrentSourcePageType != page) ContentFrame.Navigate(page);
        PlaceVideo();
    }

    public void Navigate(string tag)
    {
        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
            if ((item.Tag as string) == tag) { Nav.SelectedItem = item; return; }
    }

    /// <summary>La page « Lecture en cours » indique où dessiner la vidéo.</summary>
    public void SetVideoTarget(FrameworkElement? target) { _videoTarget = target; PlaceVideo(); }

    private void PlaceVideo()
    {
        bool show = Player.HasVideo && !_modal && _videoTarget != null && ContentFrame.Content is NowPlayingPage;
        _video.Place(_videoTarget, show);
    }

    private void OnMediaChanged()
    {
        // une vidéo qui démarre s'affiche directement
        if (Player.HasVideo && ContentFrame.Content is not NowPlayingPage) Navigate("now");
        PlaceVideo();
    }

    // =========================================================================================
    //  Ouverture de fichiers
    // =========================================================================================
    public void OpenFromShell(IList<string> paths)
    {
        if (paths.Count > 0) Player.Play(paths);
        if (AppWindow.Presenter is OverlappedPresenter op && op.State == OverlappedPresenterState.Minimized) op.Restore();
        Activate();
        Native.SetForegroundWindow(_hwnd);
    }

    public async Task OpenFilesAsync(bool enqueue = false)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary, ViewMode = PickerViewMode.List };
        foreach (var ext in MediaFormats.AllExtensions()) picker.FileTypeFilter.Add(ext);
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
        var files = await picker.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;
        var paths = files.Select(f => f.Path).OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (enqueue) Player.Enqueue(paths); else Player.Play(paths);
    }

    public async Task OpenFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) Player.Play(new[] { folder.Path });
    }

    public async Task<string?> PickFolderPathAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickSingleFileAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        foreach (var e in extensions) picker.FileTypeFilter.Add(e);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task OpenUrlAsync()
    {
        var box = new TextBox { PlaceholderText = "https://… (radio, flux, vidéo en ligne)", MinWidth = 420 };
        var dialog = new ContentDialog
        {
            Title = "Ouvrir une URL",
            Content = box,
            PrimaryButtonText = "Lire",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot,
        };
        if (await ShowModalAsync(dialog) == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text))
            Player.Play(new[] { box.Text.Trim() });
    }

    /// <summary>Les boîtes de dialogue XAML passeraient sous la vidéo : on masque celle-ci le temps du dialogue.</summary>
    public async Task<ContentDialogResult> ShowModalAsync(ContentDialog dialog)
    {
        _modal = true; PlaceVideo();
        try { return await dialog.ShowAsync(); }
        finally { _modal = false; PlaceVideo(); }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Lire avec BJMPC Player";
        }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
        var subs = paths.Where(MediaFormats.IsSubtitle).ToList();
        if (subs.Count > 0 && Player.HasMedia) { foreach (var s in subs) Player.AddSubtitle(s); paths = paths.Except(subs).ToList(); }
        if (paths.Count > 0) Player.Play(paths);
    }

    // =========================================================================================
    //  Barre de lecture
    // =========================================================================================
    private void Play_Click(object sender, RoutedEventArgs e) => Player.TogglePlay();
    private void Next_Click(object sender, RoutedEventArgs e) => Player.Next();
    private void Previous_Click(object sender, RoutedEventArgs e) => Player.Previous();
    private void Shuffle_Click(object sender, RoutedEventArgs e) => Player.ToggleShuffle();
    private void Repeat_Click(object sender, RoutedEventArgs e) => Player.CycleRepeat();
    private void Mute_Click(object sender, RoutedEventArgs e) => Player.ToggleMute();
    private void Stop_Click(object sender, RoutedEventArgs e) => Player.Stop();
    private void NowPlayingButton_Click(object sender, RoutedEventArgs e) => Navigate("now");
    private void FullScreen_Click(object sender, RoutedEventArgs e) => SetFullScreen(!_fullScreen);
    private void Mini_Click(object sender, RoutedEventArgs e) => SetMini(!_mini);
    private async void OpenFiles_Click(object sender, RoutedEventArgs e) => await OpenFilesAsync();
    private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await OpenFolderAsync();
    private async void OpenUrl_Click(object sender, RoutedEventArgs e) => await OpenUrlAsync();

    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string t } && double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            Player.SetSpeed(v);
    }

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (Math.Abs(e.NewValue - Player.Volume) > 0.5) Player.SetVolume(e.NewValue);
    }

    private void UpdateSeek()
    {
        if (_seekDragging) return;
        _updatingSeek = true;
        SeekSlider.Value = Math.Min(Player.Position, SeekSlider.Maximum);
        _updatingSeek = false;
    }

    private void SeekSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // clavier ou clic sur la piste : on suit ; glissement : on attend le relâchement
        if (_updatingSeek || _seekDragging) return;
        if (Math.Abs(e.NewValue - Player.Position) > 1) Player.Seek(e.NewValue);
    }

    private void SeekRelease(object sender, PointerRoutedEventArgs e)
    {
        if (!_seekDragging) return;
        _seekDragging = false;
        Player.Seek(SeekSlider.Value);
    }

    private void TracksMenu_Opening(object sender, object e)
    {
        TracksMenu.Items.Clear();
        void Section(string title, string type, bool allowNone)
        {
            var tracks = Player.Tracks.Where(t => t.Type == type && !t.AlbumArt).ToList();
            var sub = new MenuFlyoutSubItem { Text = title };
            if (allowNone)
            {
                var none = new RadioMenuFlyoutItem { Text = "Désactivés", GroupName = type, IsChecked = !tracks.Any(t => t.Selected) };
                none.Click += (s, a) => Player.SelectTrack(type, null);
                sub.Items.Add(none);
            }
            foreach (var t in tracks)
            {
                var it = new RadioMenuFlyoutItem { Text = t.Label, GroupName = type, IsChecked = t.Selected };
                var id = t.Id;
                it.Click += (s, a) => Player.SelectTrack(type, id);
                sub.Items.Add(it);
            }
            if (sub.Items.Count == 0) sub.Items.Add(new MenuFlyoutItem { Text = "Aucune piste", IsEnabled = false });
            TracksMenu.Items.Add(sub);
        }
        Section("Piste audio", "audio", false);
        Section("Sous-titres", "sub", true);
        var add = new MenuFlyoutItem { Text = "Ajouter un fichier de sous-titres…", Icon = new FontIcon { Glyph = "" } };
        add.Click += async (s, a) =>
        {
            var f = await PickSingleFileAsync(MediaFormats.Subtitles.ToArray());
            if (f != null) Player.AddSubtitle(f);
        };
        TracksMenu.Items.Add(new MenuFlyoutSeparator());
        TracksMenu.Items.Add(add);
    }

    // =========================================================================================
    //  Plein écran, mini-lecteur, masquage automatique
    // =========================================================================================
    public void SetFullScreen(bool on)
    {
        if (on && _mini) SetMini(false);
        _fullScreen = on;
        AppWindow.SetPresenter(on ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped);
        TitleRow.Height = new GridLength(on ? 0 : 48);
        AppTitleBar.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        Nav.IsPaneVisible = !on;
        FullScreenIcon.Glyph = on ? "" : "";
        if (on) { Navigate("now"); PokeTransport(); } else ShowTransport(true);
    }

    public void SetMini(bool on)
    {
        if (on && _fullScreen) SetFullScreen(false);
        _mini = on;
        if (on)
        {
            _restoreSize = AppWindow.Size;
            AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
            var s = Root.XamlRoot?.RasterizationScale ?? 1.0;
            AppWindow.Resize(new SizeInt32((int)(520 * s), (int)(360 * s)));
            Navigate("now");
        }
        else
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (_restoreSize.Width > 0) AppWindow.Resize(_restoreSize);
        }
        Nav.IsPaneVisible = !on;
        AppTitle.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        LeftCol.Width = on ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        NowPlayingButton.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        if (on) PokeTransport(); else ShowTransport(true);
    }

    private void ShowTransport(bool show) => Transport.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

    private void PokeTransport()
    {
        ShowTransport(true);
        if (_fullScreen || _mini) { _hideTimer.Stop(); _hideTimer.Start(); }
    }

    private void Root_PointerMoved(object sender, PointerRoutedEventArgs e) { if (_fullScreen || _mini) PokeTransport(); }
    private void Transport_PointerEntered(object sender, PointerRoutedEventArgs e) => _overTransport = true;
    private void Transport_PointerExited(object sender, PointerRoutedEventArgs e) => _overTransport = false;

    // =========================================================================================
    //  Clavier (fenêtre) et messages de la fenêtre vidéo
    // =========================================================================================
    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement(Root.XamlRoot);
        if (focused is TextBox or AutoSuggestBox or PasswordBox) return;
        bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        bool onVideoPage = ContentFrame.Content is NowPlayingPage;
        switch (e.Key)
        {
            case VirtualKey.Space when focused is not ButtonBase: Player.TogglePlay(); break;
            case VirtualKey.MediaPlayPause: Player.TogglePlay(); break;
            case VirtualKey.MediaNextTrack: case VirtualKey.N when !ctrl: Player.Next(); break;
            case VirtualKey.MediaPreviousTrack: case VirtualKey.P when !ctrl: Player.Previous(); break;
            case VirtualKey.MediaStop: Player.Stop(); break;
            case VirtualKey.Left when focused is not Slider: Player.SeekRelative(ctrl ? -60 : -10); break;
            case VirtualKey.Right when focused is not Slider: Player.SeekRelative(ctrl ? 60 : 10); break;
            case VirtualKey.Up when onVideoPage: Player.SetVolume(Player.Volume + 5); break;
            case VirtualKey.Down when onVideoPage: Player.SetVolume(Player.Volume - 5); break;
            case VirtualKey.M: Player.ToggleMute(); break;
            case VirtualKey.F11: case VirtualKey.F: SetFullScreen(!_fullScreen); break;
            case VirtualKey.Escape when _fullScreen: SetFullScreen(false); break;
            case VirtualKey.Escape when _mini: SetMini(false); break;
            case VirtualKey.O when ctrl: _ = OpenFilesAsync(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnVideoMessage(string m)
    {
        switch (m)
        {
            case "pause": case "click": Player.TogglePlay(); break;
            case "fullscreen": SetFullScreen(!_fullScreen); break;
            case "escape": if (_fullScreen) SetFullScreen(false); else if (_mini) SetMini(false); break;
            case "back": Player.SeekRelative(-10); break;
            case "forward": Player.SeekRelative(10); break;
            case "volup": Player.SetVolume(Player.Volume + 5); break;
            case "voldown": Player.SetVolume(Player.Volume - 5); break;
            case "mute": Player.ToggleMute(); break;
            case "next": Player.Next(); break;
            case "previous": Player.Previous(); break;
            case "stop": Player.Stop(); break;
            case "mousemove": if (_fullScreen || _mini) PokeTransport(); break;
        }
    }

    // =========================================================================================
    //  Thème, erreurs, liaisons
    // =========================================================================================
    public void ApplyTheme(string theme)
    {
        Root.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        UpdateCaptionButtons();
    }

    private void UpdateCaptionButtons()
    {
        var dark = Root.ActualTheme == ElementTheme.Dark;
        var tb = AppWindow.TitleBar;
        tb.ButtonBackgroundColor = Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Colors.Transparent;
        tb.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        tb.ButtonHoverBackgroundColor = dark ? Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF) : Windows.UI.Color.FromArgb(0x20, 0, 0, 0);
        tb.ButtonHoverForegroundColor = dark ? Colors.White : Colors.Black;
    }

    public void ShowError(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Info.Message = message;
            Info.Severity = InfoBarSeverity.Error;
            Info.IsOpen = true;
        });
    }

    public static string Clock(double s) => TimeFormat.Clock(s);
    public static double SafeMax(double d) => d > 0 ? d : 1;
    public static string PlayGlyph(bool playing) => playing ? "" : "";
    public static string Percent(double v) => $"{Math.Round(v)} %";
    public static bool? RepeatOn(RepeatMode r) => r != RepeatMode.Off;
    public static string RepeatGlyph(RepeatMode r) => r == RepeatMode.One ? "" : "";
    public static string VolumeGlyph(double v, bool muted) =>
        muted || v <= 0 ? "" : v < 33 ? "" : v < 66 ? "" : "";
}
