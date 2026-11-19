using System.Runtime.InteropServices;

namespace BJMPCPlayer.Services;

internal static partial class Native
{
    public const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_CLIPSIBLINGS = 0x04000000, WS_CLIPCHILDREN = 0x02000000;
    public const int SS_BLACKRECT = 0x4;
    public const int SW_HIDE = 0, SW_SHOW = 5;
    public const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040, SWP_NOZORDER = 0x0004;
    public static readonly IntPtr HWND_TOP = IntPtr.Zero;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(int exStyle, string className, string windowName, int style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandleW(string? name);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string cmdLine, out int argc);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr mem);

    /// <summary>Extrait les fichiers, dossiers et URL d'une ligne de commande (l'exe lui-même est ignoré).</summary>
    public static List<string> ParseCommandLine(string cmd)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(cmd)) return result;
        var argv = CommandLineToArgvW(cmd, out int argc);
        if (argv == IntPtr.Zero) return result;
        try
        {
            for (int i = 0; i < argc; i++)
            {
                var a = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? "";
                if (a.Length == 0 || a.StartsWith("--") || a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (Core.MediaFormats.IsUrl(a) || File.Exists(a) || Directory.Exists(a)) result.Add(a);
            }
        }
        finally { LocalFree(argv); }
        return result;
    }
}
