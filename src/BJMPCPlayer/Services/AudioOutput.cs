using System.Runtime.InteropServices;

namespace BJMPCPlayer.Services;

/// <summary>
/// Sortie audio de Windows vue par le lecteur : le lecteur ne choisit pas de périphérique, il joue
/// toujours sur la sortie par défaut de Windows (et la suit si elle change). Ce module dit laquelle
/// c'est et quelles autres applications y jouent déjà, pour ne proposer le mode exclusif WASAPI que
/// sur une carte libre.
/// </summary>
public static class AudioOutput
{
    public sealed record State(string DeviceName, bool IsVirtualCable, IReadOnlyList<string> OtherApps);

    /// <summary>État de la sortie par défaut (null si aucune sortie ou API indisponible). Appel COM : quelques ms.</summary>
    public static State? Query()
    {
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            try
            {
                if (en.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var dev) != 0 || dev == null) return null;
                try
                {
                    var name = FriendlyName(dev) ?? "Sortie par défaut";
                    bool cable = name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                                 name.StartsWith("CABLE Input", StringComparison.OrdinalIgnoreCase);
                    return new State(name, cable, AppsPlaying(dev));
                }
                finally { Marshal.ReleaseComObject(dev); }
            }
            finally { Marshal.ReleaseComObject(en); }
        }
        catch { return null; }
    }

    private static string? FriendlyName(IMMDevice dev)
    {
        if (dev.OpenPropertyStore(0 /*STGM_READ*/, out var props) != 0 || props == null) return null;
        try
        {
            var key = new PropertyKey { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
            if (props.GetValue(ref key, out var v) != 0) return null;
            try { return v.vt == 31 /*VT_LPWSTR*/ ? Marshal.PtrToStringUni(v.p) : null; }
            finally { PropVariantClear(ref v); }
        }
        finally { Marshal.ReleaseComObject(props); }
    }

    /// <summary>Applications (hors ce lecteur et sons système) dont un flux est en cours sur ce périphérique.</summary>
    private static List<string> AppsPlaying(IMMDevice dev)
    {
        var result = new List<string>();
        var iid = typeof(IAudioSessionManager2).GUID;
        if (dev.Activate(ref iid, 0x17 /*CLSCTX_ALL*/, IntPtr.Zero, out var o) != 0 || o is not IAudioSessionManager2 mgr) return result;
        try
        {
            if (mgr.GetSessionEnumerator(out var sessions) != 0 || sessions == null) return result;
            try
            {
                sessions.GetCount(out int n);
                int self = Environment.ProcessId;
                for (int i = 0; i < n; i++)
                {
                    if (sessions.GetSession(i, out var so) != 0 || so is not IAudioSessionControl2 s) continue;
                    try
                    {
                        if (s.GetState(out int state) != 0 || state != 1 /*AudioSessionStateActive*/) continue;
                        if (s.IsSystemSoundsSession() == 0) continue;          // « ding » de Windows : passager
                        if (s.GetProcessId(out uint pid) < 0 || pid == 0 || pid == self) continue;
                        var app = ProcessName((int)pid);
                        if (!result.Contains(app, StringComparer.OrdinalIgnoreCase)) result.Add(app);
                    }
                    finally { Marshal.ReleaseComObject(s); }
                }
            }
            finally { Marshal.ReleaseComObject(sessions); }
        }
        finally { Marshal.ReleaseComObject(mgr); }
        return result;
    }

    private static string ProcessName(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch { return $"processus {pid}"; }
    }

    // ------------------------------------------------------------------ COM (mmdeviceapi.h, audiopolicy.h)
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant pv);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid fmtid; public int pid; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr p;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
        // IAudioSessionManager2
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr notification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr notification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr notification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr eventContext);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingParam);
        [PreserveSig] int SetGroupingParam(ref Guid groupingParam, IntPtr eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference(bool optOut);
    }
}
