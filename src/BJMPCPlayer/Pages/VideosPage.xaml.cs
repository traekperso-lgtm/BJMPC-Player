using BJMPCPlayer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BJMPCPlayer.Pages;

public sealed partial class VideosPage : Page
{
    private readonly LibraryService _lib = LibraryService.Instance;

    public VideosPage() { InitializeComponent(); }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!_lib.VideosScanned) await ScanAsync(); else Show();
    }

    private async Task ScanAsync()
    {
        Busy.IsActive = true;
        Status.Text = "Recherche des vidéos…";
        try { await _lib.ScanVideosAsync(PlayerService.Instance.Settings); }
        catch (Exception ex) { Status.Text = "Analyse impossible : " + ex.Message; }
        Busy.IsActive = false;
        Show();
    }

    private void Show()
    {
        VideoGrid.ItemsSource = _lib.Videos;
        Status.Text = _lib.Videos.Count == 0
            ? "Aucune vidéo trouvée. Ajoutez des dossiers dans Paramètres."
            : $"{_lib.Videos.Count} vidéos";
    }

    private void VideoGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is VideoFile v) PlayerService.Instance.Play(new[] { v.Path });
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await ScanAsync();
}
