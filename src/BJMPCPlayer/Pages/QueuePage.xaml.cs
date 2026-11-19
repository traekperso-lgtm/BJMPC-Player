using BJMPCPlayer.Core;
using BJMPCPlayer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BJMPCPlayer.Pages;

public sealed partial class QueuePage : Page
{
    public PlayerService Player => PlayerService.Instance;

    public QueuePage() { InitializeComponent(); }

    private void List_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is QueueItem q) Player.PlayIndex(Player.QueueItems.IndexOf(q));
    }

    private void List_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) => Player.QueueReordered();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is QueueItem q) Player.RemoveFromQueue(Player.QueueItems.IndexOf(q));
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => Player.ClearQueue();
    private async void Add_Click(object sender, RoutedEventArgs e) => await MainWindow.Current!.OpenFilesAsync(enqueue: true);
}
