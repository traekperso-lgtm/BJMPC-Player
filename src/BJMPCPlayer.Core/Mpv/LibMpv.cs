// Liaison avec l'API cliente de libmpv (mpv/client.h, API 2.x)
using System.Reflection;
using System.Runtime.InteropServices;

namespace BJMPCPlayer.Core.Mpv;

public enum MpvFormat
{
    None = 0, String = 1, OsdString = 2, Flag = 3, Int64 = 4, Double = 5, Node = 6,
}

public enum MpvEventId
{
    None = 0, Shutdown = 1, LogMessage = 2, GetPropertyReply = 3, SetPropertyReply = 4, CommandReply = 5,
    StartFile = 6, EndFile = 7, FileLoaded = 8, ClientMessage = 16, VideoReconfig = 17, AudioReconfig = 18,
    Seek = 20, PlaybackRestart = 21, PropertyChange = 22, QueueOverflow = 24, Hook = 25,
}

public enum MpvEndReason { Eof = 0, Stop = 2, Quit = 3, Error = 4, Redirect = 5 }

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEvent
{
    public MpvEventId EventId;
    public int Error;
    public ulong ReplyUserData;
    public IntPtr Data;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventProperty
{
    public IntPtr Name;
    public MpvFormat Format;
    public IntPtr Data;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventEndFile
{
    public MpvEndReason Reason;
    public int Error;
    public long PlaylistEntryId;
    public long PlaylistInsertId;
    public int PlaylistInsertNumEntries;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventClientMessage
{
    public int NumArgs;
    public IntPtr Args;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvEventLogMessage
{
    public IntPtr Prefix;
    public IntPtr Level;
    public IntPtr Text;
    public int LogLevel;
}

internal static class LibMpv
{
    private const string Lib = "mpv";

    static LibMpv()
    {
        NativeLibrary.SetDllImportResolver(typeof(LibMpv).Assembly, Resolve);
    }

    /// <summary>Indique si libmpv est présente (libmpv-2.dll sous Windows, libmpv.so.2 sous Linux).</summary>
    public static bool IsAvailable()
    {
        try { mpv_client_api_version(); return true; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    private static IntPtr Resolve(string name, Assembly asm, DllImportSearchPath? path)
    {
        if (name != Lib) return IntPtr.Zero;
        string[] candidates = OperatingSystem.IsWindows()
            ? new[] { "libmpv-2.dll", "mpv-2.dll", "mpv-1.dll" }
            : OperatingSystem.IsMacOS()
                ? new[] { "libmpv.2.dylib", "libmpv.dylib" }
                : new[] { "libmpv.so.2", "libmpv.so" };
        foreach (var c in candidates)
        {
            var local = Path.Combine(AppContext.BaseDirectory, c);
            if (File.Exists(local) && NativeLibrary.TryLoad(local, out var h1)) return h1;
            if (NativeLibrary.TryLoad(c, asm, path, out var h2)) return h2;
        }
        return IntPtr.Zero;
    }

    [DllImport(Lib)] public static extern ulong mpv_client_api_version();
    [DllImport(Lib)] public static extern IntPtr mpv_create();
    [DllImport(Lib)] public static extern int mpv_initialize(IntPtr ctx);
    [DllImport(Lib)] public static extern void mpv_terminate_destroy(IntPtr ctx);
    [DllImport(Lib)] public static extern void mpv_wakeup(IntPtr ctx);
    [DllImport(Lib)] public static extern void mpv_free(IntPtr data);
    [DllImport(Lib)] public static extern IntPtr mpv_error_string(int error);
    [DllImport(Lib)] public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(Lib)]
    public static extern int mpv_set_option_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(Lib)]
    public static extern int mpv_request_log_messages(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string minLevel);

    [DllImport(Lib)] public static extern int mpv_command(IntPtr ctx, IntPtr[] args);

    [DllImport(Lib)]
    public static extern int mpv_set_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(Lib)]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Lib)]
    public static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format, out double data);

    [DllImport(Lib, EntryPoint = "mpv_get_property")]
    public static extern int mpv_get_property_long(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format, out long data);

    [DllImport(Lib, EntryPoint = "mpv_get_property")]
    public static extern int mpv_get_property_int(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format, out int data);

    [DllImport(Lib)]
    public static extern int mpv_set_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format, ref double data);

    [DllImport(Lib, EntryPoint = "mpv_set_property")]
    public static extern int mpv_set_property_int(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format, ref int data);

    [DllImport(Lib)]
    public static extern int mpv_observe_property(IntPtr ctx, ulong replyUserData,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format);

    public static string? PtrToUtf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

    public static string ErrorString(int code) => PtrToUtf8(mpv_error_string(code)) ?? code.ToString();
}
