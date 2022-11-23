using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace XIVLauncher.Xaml;

public static class PreserveWindowPosition
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point minPosition;
        public Point maxPosition;
        public Rect normalPosition;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, out WindowPlacement lpwndpl);

    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;

    public static void RestorePosition(Window window)
    {
        if (App.Settings.MainWindowPlacement == null)
            return;

        var placement = App.Settings.MainWindowPlacement.Value;
        placement.length = Marshal.SizeOf(typeof(WindowPlacement));
        placement.flags = 0;
        placement.showCmd = (placement.showCmd == SW_SHOWMINIMIZED ? SW_SHOWNORMAL : placement.showCmd);

        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowPlacement(hwnd, ref placement);
    }

    public static void SaveWindowPosition(Window window)
    {
        WindowPlacement wp;
        var hwnd = new WindowInteropHelper(window).Handle;
        GetWindowPlacement(hwnd, out wp);
        App.Settings.MainWindowPlacement = wp;
    }
}
