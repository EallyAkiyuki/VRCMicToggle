// Config.cs — 配置持久化：快捷键、颜色、自启（AppData/VRCMicToggle/config.txt）
using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace VRCMicToggle
{
    internal class Config
    {
        public uint HotkeyMods = 0;
        public uint HotkeyKey = (uint)Keys.Insert;
        public bool RunOnStartup = false;
        public string UnknownColor = "#888888";
        public string MutedColor = "#F48FB1";
        public string UnmutedColor = "#4FC3F7";
        public string SlashColor = "#ECECEC";

        private static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCMicToggle"); }
        }

        private static string FilePath
        {
            get { return Path.Combine(Dir, "config.txt"); }
        }

        public static bool Exists()
        {
            return File.Exists(FilePath);
        }

        public static Config Load()
        {
            Config c = new Config();
            AppLogger.Debug("Config.Load: loading from " + FilePath);
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        switch (k)
                        {
                            case "HotkeyMods": uint.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out c.HotkeyMods); break;
                            case "HotkeyKey": uint.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out c.HotkeyKey); break;
                            case "RunOnStartup": bool.TryParse(v, out c.RunOnStartup); break;
                            case "UnknownColor": c.UnknownColor = v; break;
                            case "MutedColor": c.MutedColor = v; break;
                            case "UnmutedColor": c.UnmutedColor = v; break;
                            case "SlashColor": c.SlashColor = v; break;
                        }
                    }
                    AppLogger.Debug("Config loaded: HotkeyMods=" + c.HotkeyMods + " HotkeyKey=" + c.HotkeyKey + " RunOnStartup=" + c.RunOnStartup);
                }
            }
            catch (IOException ex) { AppLogger.Log("Config.Load", ex); }
            catch (UnauthorizedAccessException ex) { AppLogger.Log("Config.Load", ex); }
            return c;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(UnknownColor)) UnknownColor = "#888888";
            if (string.IsNullOrEmpty(MutedColor)) MutedColor = "#F48FB1";
            if (string.IsNullOrEmpty(UnmutedColor)) UnmutedColor = "#4FC3F7";
            if (string.IsNullOrEmpty(SlashColor)) SlashColor = "#ECECEC";
            AppLogger.Debug("Config.Save: saving to " + FilePath);
            try
            {
                string d = Dir;
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);

                string content =
                    "HotkeyMods=" + HotkeyMods.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "HotkeyKey=" + HotkeyKey.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "RunOnStartup=" + RunOnStartup + "\r\n" +
                    "UnknownColor=" + UnknownColor + "\r\n" +
                    "MutedColor=" + MutedColor + "\r\n" +
                    "UnmutedColor=" + UnmutedColor + "\r\n" +
                    "SlashColor=" + SlashColor + "\r\n";

                // 先写临时文件再原子替换，防止写入中断导致配置丢失
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, content);
                if (File.Exists(FilePath))
                {
                    string bak = FilePath + ".bak";
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Replace(tmp, FilePath, bak);
                }
                else
                {
                    File.Move(tmp, FilePath);
                }
            }
            catch (IOException ex) { AppLogger.Log("Config.Save", ex); }
            catch (UnauthorizedAccessException ex) { AppLogger.Log("Config.Save", ex); }
        }
    }
}
