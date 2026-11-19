using BJMPCPlayer.Core;
using BJMPCPlayer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BJMPCPlayer.Pages;

public sealed class RecentEntry
{
    public string Path { get; init; } = "";
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string Glyph => MediaFormats.IsVideo(Path) ? "" : Directory.Exists(Path) ? "" : "";
}

public sealed partial class HomePage : Page
{
    public HomePage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var items = PlayerService.Instance.Settings.Recent.Where(p => File.Exists(p) || Directory.Exists(p))
                                                         .Take(15).Select(p => new RecentEntry { Path = p }).ToList();
        RecentList.ItemsSource = items;
        EmptyRecent.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RecentList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentEntry r) PlayerService.Instance.Play(new[] { r.Path });
    }

    private async void OpenFiles_Click(object sender, RoutedEventArgs e) => await MainWindow.Current!.OpenFilesAsync();
    private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await MainWindow.Current!.OpenFolderAsync();
    private async void OpenUrl_Click(object sender, RoutedEventArgs e) => await MainWindow.Current!.OpenUrlAsync();
}
