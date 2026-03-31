using System.Runtime.InteropServices;

namespace ScreenshotWebpSaver;

internal static class NativeMethods
{
    internal const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
