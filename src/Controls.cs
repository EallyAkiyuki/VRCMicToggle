// Controls.cs — ColorUtil（颜色/图形工具）、DbPanel（双缓冲 Panel）、HotkeyWindow（隐藏消息窗口）
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VRCMicToggle
{
    // ── 颜色/图形工具 ─────────────────────────────────────

    internal static class ColorUtil
    {
        internal const int PrimaryButtonR = 0;
        internal const int PrimaryButtonG = 120;
        internal const int PrimaryButtonB = 212;

        internal static Color HexToColor(string hex)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch (ArgumentException) { return Color.Gray; }
        }

        internal static GraphicsPath CreateRoundedRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static Button MakeButton(string text, EventHandler onClick, bool isPrimary, bool darkMode, Color foreColor)
        {
            Color back = isPrimary
                ? Color.FromArgb(PrimaryButtonR, PrimaryButtonG, PrimaryButtonB)
                : (darkMode ? Color.FromArgb(62, 62, 62) : Color.FromArgb(240, 240, 240));
            Color fore = isPrimary ? Color.White : foreColor;
            Button btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(72, 30),
                Padding = new Padding(14, 4, 14, 4),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = isPrimary ? Color.FromArgb(0, 110, 200) : (darkMode ? Color.FromArgb(72, 72, 72) : Color.FromArgb(220, 220, 220));
            btn.FlatAppearance.MouseDownBackColor = isPrimary ? Color.FromArgb(0, 90, 170) : (darkMode ? Color.FromArgb(52, 52, 52) : Color.FromArgb(200, 200, 200));
            btn.Click += onClick;
            return btn;
        }
    }

    // ── 双缓冲 Panel ──────────────────────────────────────

    internal class DbPanel : Panel
    {
        public DbPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

    // ── 隐藏消息窗口（接收 WM_HOTKEY 消息）────────────────

    internal class HotkeyWindow : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public event Action HotkeyPressed;

        public HotkeyWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Text = "VRCMicToggleWindow";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // 工具窗口：不显示在任务栏和 Alt-Tab
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                // 不激活：窗口不会抢夺焦点
                cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == AppContext.HOTKEY_ID)
            {
                Action h = HotkeyPressed;
                if (h != null) h();
            }
            base.WndProc(ref m);
        }
    }
}
