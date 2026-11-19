using Microsoft.UI.Xaml;

namespace BJMPCPlayer;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            e.Handled = true;
            Window?.ShowError("Erreur inattendue : " + e.Message);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        Window.Activate();
        var files = Environment.GetCommandLineArgs().Skip(1).Where(a => !a.StartsWith("--")).ToList();
        if (files.Count > 0) Window.OpenFromShell(files);
    }
}
