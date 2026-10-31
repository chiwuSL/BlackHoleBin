using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlackHoleBin
{
    internal static class WindowStateDetector
    {
        private const int SwShowMaximized = 3;
        private const uint MonitorDefaultToNearest = 2;

        public static bool IsForegroundMaximizedOrFullscreen(IntPtr ownWindow)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == ownWindow || !IsWindowVisible(foreground))
            {
                return false;
            }

            uint foregroundProcess;
            GetWindowThreadProcessId(foreground, out foregroundProcess);
            if (foregroundProcess == (uint)Process.GetCurrentProcess().Id)
            {
                return false;
            }

            WindowPlacement placement = new WindowPlacement();
            placement.Length = Marshal.SizeOf(typeof(WindowPlacement));
            if (GetWindowPlacement(foreground, ref placement) && placement.ShowCommand == SwShowMaximized)
            {
                return true;
            }

            Rect windowRect;
            if (!GetWindowRect(foreground, out windowRect))
            {
                return false;
            }

            IntPtr monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
            MonitorInfo monitorInfo = new MonitorInfo();
            monitorInfo.Size = Marshal.SizeOf(typeof(MonitorInfo));
            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            const int tolerance = 4;
            return windowRect.Left <= monitorInfo.Monitor.Left + tolerance
                && windowRect.Top <= monitorInfo.Monitor.Top + tolerance
                && windowRect.Right >= monitorInfo.Monitor.Right - tolerance
                && windowRect.Bottom >= monitorInfo.Monitor.Bottom - tolerance;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointNative
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPlacement
        {
            public int Length;
            public int Flags;
            public int ShowCommand;
            public PointNative MinimumPosition;
            public PointNative MaximumPosition;
            public Rect NormalPosition;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr window, ref WindowPlacement placement);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);
    }
}
