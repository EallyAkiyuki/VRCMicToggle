using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VRCMicToggle
{
    internal sealed class SettingsWindow : Form
    {
        private const int Pad = 24;
        private const int FormWidth = 448;
        private const int BottomPanelHeight = 56;

        private Config _cfg;
        private readonly List<Font> _ownedFonts = new List<Font>();

        private string _unknownColor, _mutedColor, _unmutedColor, _slashColor;
        private DbPanel _unknownPreview, _mutedPreview, _unmutedPreview, _slashPreview;
        private Label _unknownHex, _mutedHex, _unmutedHex, _slashHex;
        private PictureBox _iconUnknown, _iconMuted, _iconUnmuted;
        private bool _darkMode;
        private Color _fg, _bg, _borderCol, _subFg;

        public SettingsWindow(Config config)
        {
            _cfg = config;
            _darkMode = DetectDarkMode();
            ApplyTheme();
            LoadColors();
            BuildUI();
            UpdatePreviews();
        }

        private void ApplyTheme()
        {
            Theme t = Theme.Create(_darkMode);
            _bg = t.Bg;
            _fg = t.Fg;
            _borderCol = t.BorderCol;
            _subFg = t.SubFg;
        }

        private static bool DetectDarkMode()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
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

        private void LoadColors()
        {
            _unknownColor = _cfg.UnknownColor;
            _mutedColor = _cfg.MutedColor;
            _unmutedColor = _cfg.UnmutedColor;
            _slashColor = _cfg.SlashColor;
        }

        private void BuildUI()
        {
            Text = "颜色设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = _bg;
            Font = TrackFont(new Font("Segoe UI", 9f));
            Padding = new Padding(Pad);

            int pad = Pad;
            int y = pad;

            Label title = new Label
            {
                Text = "自定义麦克风颜色",
                Font = TrackFont(new Font("Segoe UI", 15f, FontStyle.Bold)),
                ForeColor = _fg,
                Location = new Point(pad, y),
                AutoSize = true
            };
            Controls.Add(title);
            y += title.Height + 6;

            Label hint = new Label
            {
                Text = "点击色块打开颜色选择器喵",
                Font = TrackFont(new Font("Segoe UI", 9f)),
                ForeColor = _subFg,
                Location = new Point(pad, y),
                AutoSize = true
            };
            Controls.Add(hint);
            y += hint.Height + 22;

            string[] labels = { "未知状态", "已静音", "已开麦", "斜杠颜色" };
            string[] colors = { _unknownColor, _mutedColor, _unmutedColor, _slashColor };
            DbPanel[] previews = new DbPanel[4];
            Label[] hexLabels = new Label[4];
            EventHandler[] actions = { PickUnknown, PickMuted, PickUnmuted, PickSlash };

            for (int i = 0; i < 4; i++)
            {
                Label lbl = new Label
                {
                    Text = labels[i],
                    Font = TrackFont(new Font("Segoe UI", 10.5f)),
                    ForeColor = _fg,
                    Location = new Point(pad, y + 7),
                    Size = new Size(80, 26),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Controls.Add(lbl);

                previews[i] = new DbPanel
                {
                    Size = new Size(36, 36),
                    Location = new Point(pad + 90, y),
                    Cursor = Cursors.Hand,
                    BackColor = ColorUtil.HexToColor(colors[i])
                };
                previews[i].Paint += ColorSwatchPaint;
                previews[i].Click += actions[i];
                Controls.Add(previews[i]);

                hexLabels[i] = new Label
                {
                    Text = colors[i].ToUpperInvariant(),
                    Font = TrackFont(new Font("Consolas", 9.5f)),
                    ForeColor = _subFg,
                    Location = new Point(pad + 90 + 36 + 14, y + 9),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Controls.Add(hexLabels[i]);

                y += 46;
            }

            _unknownPreview = previews[0];
            _mutedPreview = previews[1];
            _unmutedPreview = previews[2];
            _slashPreview = previews[3];
            _unknownHex = hexLabels[0];
            _mutedHex = hexLabels[1];
            _unmutedHex = hexLabels[2];
            _slashHex = hexLabels[3];

            y += 12;
            DbPanel divider = new DbPanel
            {
                Location = new Point(pad, y),
                Size = new Size(400, 1),
                BackColor = _borderCol
            };
            Controls.Add(divider);
            y += 20;

            Label previewTitle = new Label
            {
                Text = "图标预览",
                Font = TrackFont(new Font("Segoe UI", 10f, FontStyle.Bold)),
                ForeColor = _fg,
                Location = new Point(pad, y),
                AutoSize = true
            };
            Controls.Add(previewTitle);
            y += previewTitle.Height + 16;

            int px = pad + 36;
            _iconUnknown = MakePreviewBox(ref px, y, "未知");
            _iconMuted = MakePreviewBox(ref px, y, "静音");
            _iconUnmuted = MakePreviewBox(ref px, y, "开麦");

            y += 64;

            DbPanel bottomPanel = new DbPanel
            {
                Location = new Point(0, y),
                Size = new Size(FormWidth, BottomPanelHeight),
                BackColor = _darkMode ? Color.FromArgb(38, 38, 38) : Color.FromArgb(242, 242, 242)
            };
            bottomPanel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(_borderCol))
                    e.Graphics.DrawLine(p, 0, 0, bottomPanel.Width, 0);
            };
            Controls.Add(bottomPanel);

            Button defaultBtn = MakeButton("恢复默认", OnRestoreDefaults, false);
            defaultBtn.Location = new Point(pad, 12);
            bottomPanel.Controls.Add(defaultBtn);

            int bx = FormWidth - pad;
            Button saveBtn = MakeButton("保存", OnSave, true);
            saveBtn.Location = new Point(bx - saveBtn.Width, 12);
            bx -= saveBtn.Width + 8;
            bottomPanel.Controls.Add(saveBtn);

            Button cancelBtn = MakeButton("取消", OnCancel, false);
            cancelBtn.Location = new Point(bx - cancelBtn.Width, 12);
            bx -= cancelBtn.Width + 8;
            bottomPanel.Controls.Add(cancelBtn);

            ClientSize = new Size(FormWidth, y + BottomPanelHeight);
        }

        private void ColorSwatchPaint(object sender, PaintEventArgs e)
        {
            DbPanel p = (DbPanel)sender;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(_bg);
            Rectangle r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
            using (GraphicsPath gp = ColorUtil.CreateRoundedRect(r.X, r.Y, r.Width, r.Height, 8))
            using (SolidBrush br = new SolidBrush(p.BackColor))
                e.Graphics.FillPath(br, gp);
            using (GraphicsPath gp = ColorUtil.CreateRoundedRect(r.X, r.Y, r.Width, r.Height, 8))
            using (Pen pen = new Pen(_borderCol))
                e.Graphics.DrawPath(pen, gp);
        }

        private PictureBox MakePreviewBox(ref int x, int y, string label)
        {
            PictureBox pb = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(x, y),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent
            };
            Controls.Add(pb);

            Label lbl = new Label
            {
                Text = label,
                Font = TrackFont(new Font("Segoe UI", 9f)),
                ForeColor = _subFg,
                Location = new Point(x - 4, y + 42),
                Size = new Size(48, 18),
                TextAlign = ContentAlignment.TopCenter
            };
            Controls.Add(lbl);

            x += 62;
            return pb;
        }

        private Button MakeButton(string text, EventHandler onClick, bool isPrimary)
        {
            return ColorUtil.MakeButton(text, onClick, isPrimary, _darkMode, _fg);
        }

        private void UpdatePreviews()
        {
            _unknownPreview.BackColor = ColorUtil.HexToColor(_unknownColor);
            _mutedPreview.BackColor = ColorUtil.HexToColor(_mutedColor);
            _unmutedPreview.BackColor = ColorUtil.HexToColor(_unmutedColor);
            _slashPreview.BackColor = ColorUtil.HexToColor(_slashColor);
            _unknownPreview.Invalidate();
            _mutedPreview.Invalidate();
            _unmutedPreview.Invalidate();
            _slashPreview.Invalidate();

            _unknownHex.Text = _unknownColor.ToUpperInvariant();
            _mutedHex.Text = _mutedColor.ToUpperInvariant();
            _unmutedHex.Text = _unmutedColor.ToUpperInvariant();
            _slashHex.Text = _slashColor.ToUpperInvariant();

            Bitmap bmp;

            Image old = _iconUnknown.Image;
            bmp = AppContext.CreateMicIcon(ColorUtil.HexToColor(_unknownColor), ColorUtil.HexToColor(_slashColor), false);
            _iconUnknown.Image = bmp;
            if (old != null) old.Dispose();

            old = _iconMuted.Image;
            bmp = AppContext.CreateMicIcon(ColorUtil.HexToColor(_mutedColor), ColorUtil.HexToColor(_slashColor), true);
            _iconMuted.Image = bmp;
            if (old != null) old.Dispose();

            old = _iconUnmuted.Image;
            bmp = AppContext.CreateMicIcon(ColorUtil.HexToColor(_unmutedColor), ColorUtil.HexToColor(_slashColor), false);
            _iconUnmuted.Image = bmp;
            if (old != null) old.Dispose();
        }

        private void PickUnknown(object sender, EventArgs e) { PickColor(ref _unknownColor); }
        private void PickMuted(object sender, EventArgs e) { PickColor(ref _mutedColor); }
        private void PickUnmuted(object sender, EventArgs e) { PickColor(ref _unmutedColor); }
        private void PickSlash(object sender, EventArgs e) { PickColor(ref _slashColor); }

        private void PickColor(ref string colorHex)
        {
            using (ColorPickerDialog dlg = new ColorPickerDialog(colorHex, _darkMode))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    colorHex = dlg.SelectedColorHex;
                    UpdatePreviews();
                }
            }
        }

        private void OnRestoreDefaults(object sender, EventArgs e)
        {
            _unknownColor = "#888888";
            _mutedColor = "#F48FB1";
            _unmutedColor = "#4FC3F7";
            _slashColor = "#ECECEC";
            UpdatePreviews();
        }

        private void OnCancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void OnSave(object sender, EventArgs e)
        {
            _cfg.UnknownColor = _unknownColor;
            _cfg.MutedColor = _mutedColor;
            _cfg.UnmutedColor = _unmutedColor;
            _cfg.SlashColor = _slashColor;
            _cfg.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private Font TrackFont(Font f)
        {
            _ownedFonts.Add(f);
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int i = 0; i < _ownedFonts.Count; i++)
                {
                    try { _ownedFonts[i].Dispose(); } catch (Exception) { }
                }
                _ownedFonts.Clear();

                Image[] imgs = { _iconUnknown != null ? _iconUnknown.Image : null,
                                  _iconMuted != null ? _iconMuted.Image : null,
                                  _iconUnmuted != null ? _iconUnmuted.Image : null };
                for (int i = 0; i < imgs.Length; i++)
                {
                    try { if (imgs[i] != null) imgs[i].Dispose(); } catch (Exception) { }
                }
            }
            base.Dispose(disposing);
        }
    }
}
