using BJMPCPlayer.Core;
using BJMPCPlayer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace BJMPCPlayer.Pages;

public sealed partial class MusicPage : Page
{
    private readonly LibraryService _lib = LibraryService.Instance;
    private List<Song> _shown = new();

    public MusicPage() { InitializeComponent(); }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!_lib.MusicScanned) await ScanAsync(); else Refresh();
    }

    private async Task ScanAsync()
    {
        Busy.IsActive = true;
        Status.Text = "Analyse de la bibliothèque…";
        var progress = new Progress<int>(n => Status.Text = $"Analyse de la bibliothèque… {n} nouveaux titres");
        try { await _lib.ScanMusicAsync(PlayerService.Instance.Settings, progress); }
        catch (Exception ex) { Status.Text = "Analyse impossible : " + ex.Message; }
        Busy.IsActive = false;
        Refresh();
    }

    private void Refresh()
    {
        var q = Search.Text?.Trim() ?? "";
        _shown = q.Length == 0 ? _lib.Songs : _lib.Songs.Where(s =>
            s.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
            s.Artist.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
            s.Album.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        SongList.ItemsSource = _shown;
        AlbumGrid.ItemsSource = q.Length == 0 ? _lib.Albums : _lib.Albums.Where(a =>
            a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || a.Artist.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        var folders = string.Join(", ", LibraryService.MusicFolders(PlayerService.Instance.Settings));
        Status.Text = _lib.Songs.Count == 0
            ? $"Aucun titre trouvé dans {folders}. Ajoutez des dossiers dans Paramètres."
            : $"{_lib.Songs.Count} titres · {_lib.Albums.Count} albums";
    }

    private static IEnumerable<QueueItem> Items(IEnumerable<Song> songs) =>
        songs.Select(s => new QueueItem(s.Path, s.Title, s.Artist) { Duration = s.Duration });

    private void SongList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Song s) PlayerService.Instance.PlayItems(Items(_shown), _shown.IndexOf(s));
    }

    private void AlbumGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Album a) PlayerService.Instance.PlayItems(Items(a.Songs));
    }

    private void ShuffleAll_Click(object sender, RoutedEventArgs e)
    {
        if (_shown.Count == 0) return;
        var p = PlayerService.Instance;
        if (!p.Shuffle) p.ToggleShuffle();
        p.PlayItems(Items(_shown), Random.Shared.Next(_shown.Count));
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await ScanAsync();
    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => Refresh();

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        bool albums = sender.SelectedItem == TabAlbums;
        SongList.Visibility = albums ? Visibility.Collapsed : Visibility.Visible;
        AlbumGrid.Visibility = albums ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SongList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not Song s) return;
        var menu = new MenuFlyout();
        void Add(string text, string glyph, Action a)
        {
            var it = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
            it.Click += (_, _) => a();
            menu.Items.Add(it);
        }
        Add("Lire", "", () => PlayerService.Instance.PlayItems(Items(_shown), _shown.IndexOf(s)));
        Add("Lire ensuite", "", () => PlayerService.Instance.Enqueue(new[] { s.Path }, playNext: true));
        Add("Ajouter à la file d'attente", "", () => PlayerService.Instance.Enqueue(new[] { s.Path }));
        Add("Ouvrir le dossier", "", () => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{s.Path}\""));
        menu.ShowAt((FrameworkElement)e.OriginalSource, e.GetPosition((UIElement)e.OriginalSource));
    }
}
