using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace DesktopTodo;

// Shell integration is deliberately isolated from calendar/data code.
public static class DesktopHost
{
    public const int GwlExStyle = -20;
    public const int GwlStyle = -16;
    public const long WsExToolWindow = 0x00000080L;
    public const long WsExAppWindow = 0x00040000L;
    public const long WsChild = 0x40000000L;
    public const long WsPopup = 0x80000000L;
    public const uint GwHwndNext = 2;
    public const uint GwHwndPrev = 3;
    public static readonly IntPtr HwndBottom = new(1);
    public delegate bool EnumProc(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    public static IntPtr FindDesktop()
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero) return true;
            var name = new StringBuilder(256);
            GetClassName(hwnd, name, name.Capacity);
            if (name.ToString() is "Progman" or "WorkerW") { result = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return result;
    }
    public static bool PlaceOnDesktop(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        HideFromTaskbar(window);
        var desktop = FindDesktop();
        if (desktop == IntPtr.Zero) return false;
        long style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        long desktopStyle = (style & ~WsChild) | WsPopup;
        if (desktopStyle != style) SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(desktopStyle));
        if (GetParent(hwnd) != IntPtr.Zero) return false;
        if (IsIconic(hwnd)) ShowWindow(hwnd, 9);
        ShowWindow(hwnd, 4);
        // hWndInsertAfter inserts below the supplied window. Use Explorer's
        // previous sibling so the calendar lands immediately above Explorer.
        // Passing Explorer itself can put the calendar behind the wallpaper.
        var aboveDesktop = GetWindow(desktop, GwHwndPrev);
        if (aboveDesktop == hwnd) return true;
        return SetWindowPos(hwnd, aboveDesktop, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
    }
    public static void HideFromTaskbar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        long exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        long hiddenStyle = (exStyle | WsExToolWindow) & ~WsExAppWindow;
        if (hiddenStyle != exStyle) SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(hiddenStyle));
    }
    public static void BringToFront(Window window)
    {
        HideFromTaskbar(window);
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (IsIconic(hwnd)) ShowWindow(hwnd, 9);
        ShowWindow(hwnd, 4);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
        SetForegroundWindow(hwnd);
    }
    public static void MoveScreen(IntPtr hwnd, int x, int y)
    {
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }
}
