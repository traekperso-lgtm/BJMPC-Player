using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace BJMPCPlayer.Services;

/// <summary>
/// Fenêtre Win32 enfant dans laquelle libmpv dessine la vidéo (Direct3D 11, décodage GPU).
/// Elle est posée exactement sur la zone vidéo de la page « Lecture en cours ».
/// </summary>
public sealed class VideoHost : IDisposable
{
    private IntPtr _hwnd;
    private Rect _last = Rect.Empty;
    private bool _visible;

    public IntPtr Handle => _hwnd;

    public VideoHost(IntPtr parent)
    {
        _hwnd = Native.CreateWindowExW(0, "Static", "", Native.WS_CHILD | Native.WS_CLIPSIBLINGS | Native.WS_CLIPCHILDREN | Native.SS_BLACKRECT,
                                       0, 0, 1, 1, parent, IntPtr.Zero, Native.GetModuleHandleW(null), IntPtr.Zero);
    }

    /// <summary>Place la vidéo sur <paramref name="target"/> (en pixels physiques) ou la masque.</summary>
    public void Place(FrameworkElement? target, bool show)
    {
        if (_hwnd == IntPtr.Zero) return;
        if (!show || target == null || target.XamlRoot == null || target.ActualWidth < 2 || target.ActualHeight < 2)
        {
            if (_visible) { Native.ShowWindow(_hwnd, Native.SW_HIDE); _visible = false; _last = Rect.Empty; }
            return;
        }
        double s = target.XamlRoot.RasterizationScale;
        var p = target.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var r = new Rect(Math.Round(p.X * s), Math.Round(p.Y * s), Math.Round(target.ActualWidth * s), Math.Round(target.ActualHeight * s));
        if (_visible && r == _last) return;
        Native.SetWindowPos(_hwnd, Native.HWND_TOP, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height,
                            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        _last = r;
        _visible = true;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero) { Native.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
    }
}
