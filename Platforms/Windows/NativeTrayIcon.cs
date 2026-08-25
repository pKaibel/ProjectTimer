using System.Runtime.InteropServices;
using ProjectTimer.Services;

namespace ProjectTimer.WinUI;

/// <summary>
/// Native Windows notification-area icon. This avoids loading an additional WinUI XAML library.
/// </summary>
internal sealed class NativeTrayIcon : IDisposable
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint WM_APP = 0x8000;
    private const uint TrayCallbackMessage = WM_APP + 0x3C;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;
    private static readonly UIntPtr SubclassId = (UIntPtr)0x5054;

    private readonly IntPtr _windowHandle;
    private readonly Action _restoreWindow;
    private readonly SubclassProc _subclassProc;
    private IntPtr _windowIcon;
    private IntPtr _statusIcon;
    private TimeTrackingStatus _status = TimeTrackingStatus.Idle;
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
        _windowIcon = CreateStatusIcon(TimeTrackingStatus.Idle);
        ApplyTitleBarIcon(_windowIcon);
        _statusIcon = CreateStatusIcon(_status);
        ApplyTaskbarIcon(_statusIcon);
        var data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _statusIcon,
            szTip = GetToolTip(_status)
        };

        ShellNotifyIcon(NIM_ADD, ref data);
    }

    public void SetStatus(TimeTrackingStatus status)
    {
        if (_isDisposed || _status == status)
        {
            return;
        }

        var updatedIcon = CreateStatusIcon(status);
        var data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NIF_ICON | NIF_TIP,
            hIcon = updatedIcon,
            szTip = GetToolTip(status)
        };

        if (ShellNotifyIcon(NIM_MODIFY, ref data))
        {
            ApplyTaskbarIcon(updatedIcon);
            DestroyIcon(_statusIcon);
            _statusIcon = updatedIcon;
            _status = status;
        }
        else
        {
            DestroyIcon(updatedIcon);
        }
    }

    public void RefreshWindowIcons()
    {
        if (_isDisposed)
        {
            return;
        }

        ApplyTitleBarIcon(_windowIcon);
        ApplyTaskbarIcon(_statusIcon);
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
        DestroyIcon(_windowIcon);
        _windowIcon = IntPtr.Zero;
        DestroyIcon(_statusIcon);
        _statusIcon = IntPtr.Zero;
        _isDisposed = true;
    }

    internal static void HideWindow(IntPtr windowHandle) => ShowWindowNative(windowHandle, SW_HIDE);

    internal static void ShowWindow(IntPtr windowHandle) => ShowWindowNative(windowHandle, SW_RESTORE);

    private static string GetToolTip(TimeTrackingStatus status) => status switch
    {
        TimeTrackingStatus.Running => "ProjectTimer – Zeiterfassung läuft",
        TimeTrackingStatus.Paused => "ProjectTimer – Zeiterfassung pausiert",
        _ => "ProjectTimer – Keine aktive Zeiterfassung"
    };

    private static IntPtr CreateStatusIcon(TimeTrackingStatus status)
    {
        const int size = 32;
        const int maskStride = 4;
        var pixels = new byte[size * size * 4];
        var mask = new byte[size * maskStride];
        var (red, green, blue) = status switch
        {
            TimeTrackingStatus.Running => (46, 125, 50),
            TimeTrackingStatus.Paused => (249, 168, 37),
            _ => (37, 99, 235)
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!IsInsideRoundedRectangle(x, y, size, 6))
                {
                    mask[(y * maskStride) + (x / 8)] |= (byte)(0x80 >> (x % 8));
                    continue;
                }

                var index = ((y * size) + x) * 4;
                pixels[index] = (byte)blue;
                pixels[index + 1] = (byte)green;
                pixels[index + 2] = (byte)red;
                pixels[index + 3] = 255;
            }
        }

        // Match the app icon: a rounded color tile with the white stopwatch outline.
        DrawCircleOutline(pixels, size, 16, 17, 8.5, 2.25, 255, 255, 255);
        DrawLine(pixels, size, 16, 11.25, 16, 17.5, 2.25, 255, 255, 255);
        DrawLine(pixels, size, 16, 17.5, 20.25, 20, 2.25, 255, 255, 255);
        DrawLine(pixels, size, 12.25, 5.5, 19.75, 5.5, 2.25, 255, 255, 255);

        var bitmapInfo = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = size,
                Height = -size,
                Planes = 1,
                BitCount = 32
            }
        };
        var colorBitmap = CreateDIBSection(IntPtr.Zero, ref bitmapInfo, 0, out var bits, IntPtr.Zero, 0);
        if (colorBitmap == IntPtr.Zero || bits == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        Marshal.Copy(pixels, 0, bits, pixels.Length);
        var maskBitmap = CreateBitmap(size, size, 1, 1, mask);
        var iconInfo = new IconInfo { IsIcon = true, ColorBitmap = colorBitmap, MaskBitmap = maskBitmap };
        var icon = CreateIconIndirect(ref iconInfo);
        DeleteObject(colorBitmap);
        DeleteObject(maskBitmap);
        return icon;
    }

    private static bool IsInsideRoundedRectangle(int x, int y, int size, double radius)
    {
        var pointX = x + 0.5;
        var pointY = y + 0.5;
        var nearestX = Math.Clamp(pointX, radius, size - radius);
        var nearestY = Math.Clamp(pointY, radius, size - radius);
        var distanceX = pointX - nearestX;
        var distanceY = pointY - nearestY;
        return (distanceX * distanceX) + (distanceY * distanceY) <= radius * radius;
    }

    private static void DrawCircleOutline(byte[] pixels, int size, double centerX, double centerY, double radius, double thickness, byte red, byte green, byte blue)
    {
        var outerRadiusSquared = Math.Pow(radius + (thickness / 2), 2);
        var innerRadiusSquared = Math.Pow(radius - (thickness / 2), 2);
        var start = (int)Math.Floor(centerX - radius - thickness);
        var end = (int)Math.Ceiling(centerX + radius + thickness);
        for (var y = start; y <= end; y++)
        {
            for (var x = start; x <= end; x++)
            {
                var distanceX = x - centerX + 0.5;
                var distanceY = y - centerY + 0.5;
                var distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);
                if (distanceSquared >= innerRadiusSquared && distanceSquared <= outerRadiusSquared)
                {
                    SetPixel(pixels, size, x, y, red, green, blue);
                }
            }
        }
    }

    private static void DrawLine(byte[] pixels, int size, double startX, double startY, double endX, double endY, double thickness, byte red, byte green, byte blue)
    {
        var radius = thickness / 2;
        var minimumX = (int)Math.Floor(Math.Min(startX, endX) - radius);
        var maximumX = (int)Math.Ceiling(Math.Max(startX, endX) + radius);
        var minimumY = (int)Math.Floor(Math.Min(startY, endY) - radius);
        var maximumY = (int)Math.Ceiling(Math.Max(startY, endY) + radius);
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var lengthSquared = (deltaX * deltaX) + (deltaY * deltaY);

        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                var offsetX = x + 0.5 - startX;
                var offsetY = y + 0.5 - startY;
                var factor = lengthSquared == 0 ? 0 : Math.Clamp(((offsetX * deltaX) + (offsetY * deltaY)) / lengthSquared, 0, 1);
                var distanceX = (x + 0.5) - (startX + (factor * deltaX));
                var distanceY = (y + 0.5) - (startY + (factor * deltaY));
                if ((distanceX * distanceX) + (distanceY * distanceY) <= radius * radius)
                {
                    SetPixel(pixels, size, x, y, red, green, blue);
                }
            }
        }
    }

    private static void SetPixel(byte[] pixels, int size, int x, int y, byte red, byte green, byte blue)
    {
        if (x < 0 || x >= size || y < 0 || y >= size)
        {
            return;
        }

        var index = ((y * size) + x) * 4;
        pixels[index] = blue;
        pixels[index + 1] = green;
        pixels[index + 2] = red;
        pixels[index + 3] = 255;
    }

    private void ApplyTitleBarIcon(IntPtr icon)
    {
        SendMessage(_windowHandle, WM_SETICON, (IntPtr)ICON_SMALL, icon);
    }

    private void ApplyTaskbarIcon(IntPtr icon)
    {
        SendMessage(_windowHandle, WM_SETICON, (IntPtr)ICON_BIG, icon);
    }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public uint XHotspot;
        public uint YHotspot;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
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

    [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowNative(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo bitmapInfo, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, byte[] bits);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);
}
