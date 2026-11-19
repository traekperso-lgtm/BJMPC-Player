using BJMPCPlayer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BJMPCPlayer.Pages;

public sealed partial class NowPlayingPage : Page
{
    public PlayerService Player => PlayerService.Instance;

    public NowPlayingPage()
    {
        InitializeComponent();
        Loaded += (s, e) => MainWindow.Current?.SetVideoTarget(VideoArea);
        Unloaded += (s, e) => MainWindow.Current?.SetVideoTarget(null);
    }

    public Visibility Vis(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AudioVis(bool hasVideo, bool hasMedia) => !hasVideo && hasMedia ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVis(bool hasMedia) => hasMedia ? Visibility.Collapsed : Visibility.Visible;

    private async void Open_Click(object sender, RoutedEventArgs e) => await MainWindow.Current!.OpenFilesAsync();
}
