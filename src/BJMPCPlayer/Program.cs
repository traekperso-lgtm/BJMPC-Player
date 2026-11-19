using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace BJMPCPlayer;

/// <summary>Point d'entrée : une seule instance ; un fichier ouvert alors que le lecteur tourne lui est transmis.</summary>
public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var main = AppInstance.FindOrRegisterForKey("BJMPCPlayer-instance");
        if (!main.IsCurrent)
        {
            // une fenêtre existe déjà : on lui passe la demande puis on s'arrête
            Task.Run(async () => await main.RedirectActivationToAsync(activation)).Wait();
            return 0;
        }
        main.Activated += OnRedirected;
        Application.Start(p =>
        {
            var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(ctx);
            _ = new App();
        });
        return 0;
    }

    private static void OnRedirected(object? sender, AppActivationArguments e)
    {
        string cmd = e.Kind == ExtendedActivationKind.Launch && e.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs la
            ? la.Arguments : "";
        var paths = Services.Native.ParseCommandLine(cmd);
        App.Window?.DispatcherQueue.TryEnqueue(() => App.Window.OpenFromShell(paths));
    }
}
