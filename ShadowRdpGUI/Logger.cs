using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ShadowRdpGUI
{
    public static class Logger
    {
        public static bool DebugEnabled { get; set; }
        public static bool LogEnabled { get; set; }
        public static bool DumpEnabled { get; set; }

        private static readonly object _sync = new object();

        private static string BaseDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ShadowRdpGUI");

        public static string LogPath => Path.Combine(BaseDir, "shadowrdp.log");
        public static string DumpPath => Path.Combine(BaseDir, "windows_dump.txt");

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hWnd);

        public static void Write(string message)
        {
            if (!LogEnabled) return;
            try
            {
                lock (_sync)
                {
                    Directory.CreateDirectory(BaseDir);
                    File.AppendAllText(LogPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }

        public static void Debug(string message)
        {
            if (!DebugEnabled) return;
            Write("[DEBUG] " + message);
        }

        public static void DumpWindows(string tag)
        {
            if (!DumpEnabled) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== {tag} | {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

                EnumWindows((hWnd, lParam) =>
                {
                    var cls = new StringBuilder(256);
                    GetClassName(hWnd, cls, cls.Capacity);
                    var title = new StringBuilder(256);
                    GetWindowText(hWnd, title, title.Capacity);

                    sb.AppendLine(
                        $"HWND=0x{hWnd.ToInt64():X8} | Vis={IsWindowVisible(hWnd)} | " +
                        $"Par=0x{GetParent(hWnd).ToInt64():X8} | '{cls}' | '{title}'");
                    return true;
                }, IntPtr.Zero);

                sb.AppendLine();

                lock (_sync)
                {
                    Directory.CreateDirectory(BaseDir);
                    File.AppendAllText(DumpPath, sb.ToString());
                }
            }
            catch { }
        }
    }
}
