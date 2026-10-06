using System;
using System.IO;

namespace ShadowRdpGUI
{
    public class AppSettings
    {
        public bool EnableDebug { get; set; }
        public bool EnableLog { get; set; }
        public bool EnableWindowDump { get; set; }
        public bool DarkTheme { get; set; }

        private static string ConfigDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ShadowRdpGUI");

        public static string ConfigPath => Path.Combine(ConfigDir, "config.conf");

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!File.Exists(ConfigPath)) return s;

                foreach (var raw in File.ReadAllLines(ConfigPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    int idx = line.IndexOf('=');
                    if (idx <= 0) continue;

                    string key = line.Substring(0, idx).Trim();
                    string val = line.Substring(idx + 1).Trim();
                    bool b = val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1";

                    switch (key)
                    {
                        case "EnableDebug": s.EnableDebug = b; break;
                        case "EnableLog": s.EnableLog = b; break;
                        case "EnableWindowDump": s.EnableWindowDump = b; break;
                        case "DarkTheme": s.DarkTheme = b; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllLines(ConfigPath, new[]
                {
                "# ShadowRdpGUI configuration",
                "# Auto-generated file.",
                "",
                "EnableDebug=" + EnableDebug.ToString().ToLower(),
                "EnableLog=" + EnableLog.ToString().ToLower(),
                "EnableWindowDump=" + EnableWindowDump.ToString().ToLower(),
                "DarkTheme=" + DarkTheme.ToString().ToLower(),
            });
            }
            catch { }
        }
    }
}
