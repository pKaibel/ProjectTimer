using System.Runtime.InteropServices;

namespace ProjectTimer.WinUI;

/// <summary>
/// Native Windows notification-area icon. This avoids loading an additional WinUI XAML library.
/// </summary>
internal sealed class NativeTrayIcon : IDisposable
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint WM_APP = 0x8000;
    private const uint TrayCallbackMessage = WM_APP + 0x3C;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_GETICON = 0x007F;
    private const int ICON_SMALL2 = 2;
    private const int IMAGE_ICON = 1;
    private const int LR_SHARED = 0x8000;
    private const int IDI_APPLICATION = 32512;
    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;
    private static readonly UIntPtr SubclassId = (UIntPtr)0x5054;

    private readonly IntPtr _windowHandle;
    private readonly Action _restoreWindow;
    private readonly SubclassProc _subclassProc;
    private bool _isDisposed;

    public NativeTrayIcon(IntPtr windowHandle, Action restoreWindow)
    {
        _windowHandle = windowHandle;
        _restoreWindow = restoreWindow;
        _subclassProc = WindowSubclassProc;

        SetWindowSubclass(_windowHandle, _subclassProc, SubclassId, IntPtr.Zero);
        AddIcon();
    }

    private void AddIcon()
    {
        var icon = SendMessage(_windowHandle, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero);
        if (icon == IntPtr.Zero)
        {
            icon = LoadImage(IntPtr.Zero, (IntPtr)IDI_APPLICATION, IMAGE_ICON, 0, 0, LR_SHARED);
        }

        var data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = icon,
            szTip = "ProjectTimer – Zeiterfassung"
        };

        ShellNotifyIcon(NIM_ADD, ref data);
    }

    private IntPtr WindowSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (message == TrayCallbackMessage && (uint)lParam.ToInt64() == WM_LBUTTONDBLCLK)
        {
            _restoreWindow();
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        var data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1
        };
        ShellNotifyIcon(NIM_DELETE, ref data);
        RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
        _isDisposed = true;
    }

    internal static void HideWindow(IntPtr windowHandle) => ShowWindowNative(windowHandle, SW_HIDE);

    internal static void ShowWindow(IntPtr windowHandle) => ShowWindowNative(windowHandle, SW_RESTORE);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc callback, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc callback, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, IntPtr name, int type, int desiredWidth, int desiredHeight, int loadFlags);

    [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowNative(IntPtr windowHandle, int command);
}
