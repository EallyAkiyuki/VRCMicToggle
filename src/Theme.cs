// Theme.cs — UI 主题：亮/暗模式颜色常量
using System.Drawing;

namespace VRCMicToggle
{
    internal sealed class Theme
    {
        public Color Bg, Fg, BorderCol, SubFg, InputBg;

        public static Theme Create(bool dark)
        {
            Theme t = new Theme();
            if (dark)
            {
                t.Bg = Color.FromArgb(32, 32, 32);
                t.Fg = Color.FromArgb(240, 240, 240);
                t.BorderCol = Color.FromArgb(60, 60, 60);
                t.SubFg = Color.FromArgb(160, 160, 160);
                t.InputBg = Color.FromArgb(48, 48, 48);
            }
            else
            {
                t.Bg = Color.FromArgb(250, 250, 250);
                t.Fg = Color.FromArgb(32, 32, 32);
                t.BorderCol = Color.FromArgb(220, 220, 220);
                t.SubFg = Color.FromArgb(120, 120, 120);
                t.InputBg = Color.FromArgb(255, 255, 255);
            }
            return t;
        }

        // 检测系统是否使用深色模式（注册表）
        public static bool DetectDarkMode()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (rk != null)
                    {
                        object v = rk.GetValue("AppsUseLightTheme");
                        if (v is int && (int)v == 0) return true;
                    }
                }
            }
            catch (System.Security.SecurityException) { }
            return false;
        }
    }
}
