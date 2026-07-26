// SettingsWindow.cs — 图标颜色自定义设置窗口
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VRCMicToggle
{
    // 颜色设置窗口：未知/静音/开麦/斜杠四色自定义，含图标预览
    internal sealed class SettingsWindow : Form
    {
        private const int Pad = 24;
        private const int FormWidth = 448;
        private const int BottomPanelHeight = 56;
        private const int ColorCount = 4;

        // 索引常量，对应 _colors / _previews / _hexLabels 数组
        private const int IdxUnknown = 0;
        private const int IdxMuted = 1;
        private const int IdxUnmuted = 2;
        private const int IdxSlash = 3;

        private Config _cfg;
        private readonly List<Font> _ownedFonts = new List<Font>();

        private string[] _colors = new string[ColorCount];
        private DbPanel[] _previews = new DbPanel[ColorCount];
        private Label[] _hexLabels = new Label[ColorCount];
        private PictureBox[] _iconPreviews = new PictureBox[3]; // unknown, muted, unmuted

        private bool _darkMode;
        private Color _fg, _bg, _borderCol, _subFg;

        // ── 构造 / 主题 / 颜色加载 ──────────────────────

        public SettingsWindow(Config config)
        {
            _cfg = config;
            _darkMode = Theme.DetectDarkMode();
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

        private void LoadColors()
        {
            _colors[IdxUnknown] = _cfg.UnknownColor;
            _colors[IdxMuted] = _cfg.MutedColor;
            _colors[IdxUnmuted] = _cfg.UnmutedColor;
            _colors[IdxSlash] = _cfg.SlashColor;
        }

        // ── UI 构建 ─────────────────────────────────────

        private void BuildUI()
        {
            Text = Lang.SettingsTitle;
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
                Text = Lang.SettingsSubtitle,
                Font = TrackFont(new Font("Segoe UI", 15f, FontStyle.Bold)),
                ForeColor = _fg,
                Location = new Point(pad, y),
                AutoSize = true
            };
            Controls.Add(title);
            y += title.Height + 6;

            Label hint = new Label
            {
                Text = Lang.SettingsHint,
                Font = TrackFont(new Font("Segoe UI", 9f)),
                ForeColor = _subFg,
                Location = new Point(pad, y),
                AutoSize = true
            };
            Controls.Add(hint);
            y += hint.Height + 22;

            string[] labels = Lang.ColorLabels;

            for (int i = 0; i < ColorCount; i++)
            {
                int idx = i; // 闭包捕获
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

                _previews[i] = new DbPanel
                {
                    Size = new Size(36, 36),
                    Location = new Point(pad + 90, y),
                    Cursor = Cursors.Hand,
                    BackColor = ColorUtil.HexToColor(_colors[i])
                };
                _previews[i].Paint += ColorSwatchPaint;
                _previews[i].Click += (s, e) => PickColor(idx);
                Controls.Add(_previews[i]);

                _hexLabels[i] = new Label
                {
                    Text = _colors[i].ToUpperInvariant(),
                    Font = TrackFont(new Font("Consolas", 9.5f)),
                    ForeColor = _subFg,
                    Location = new Point(pad + 90 + 36 + 14, y + 9),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Controls.Add(_hexLabels[i]);

                y += 46;
            }

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
                Text = Lang.IconPreviewTitle,
                Font = TrackFont(new Font("Segoe UI", 10f, FontStyle.Bold)),
                ForeColor = _fg,
                Location = new Point(pad, y),
                AutoSize = true
            };
            Controls.Add(previewTitle);
            y += previewTitle.Height + 16;

            int px = pad + 36;
            string[] iconLabels = Lang.IconPreviewLabels;
            for (int i = 0; i < 3; i++)
            {
                _iconPreviews[i] = MakePreviewBox(ref px, y, iconLabels[i]);
            }

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

            Button defaultBtn = MakeButton(Lang.BtnRestoreDefaults, OnRestoreDefaults, false);
            defaultBtn.Location = new Point(pad, 12);
            bottomPanel.Controls.Add(defaultBtn);

            int bx = FormWidth - pad;
            Button saveBtn = MakeButton(Lang.BtnSave, OnSave, true);
            saveBtn.Location = new Point(bx - saveBtn.Width, 12);
            bx -= saveBtn.Width + 8;
            bottomPanel.Controls.Add(saveBtn);

            Button cancelBtn = MakeButton(Lang.BtnCancel, OnCancel, false);
            cancelBtn.Location = new Point(bx - cancelBtn.Width, 12);
            bx -= cancelBtn.Width + 8;
            bottomPanel.Controls.Add(cancelBtn);

            ClientSize = new Size(FormWidth, y + BottomPanelHeight);
        }

        // ── 颜色选择 / 预览 / 事件处理 ──────────────────

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
            for (int i = 0; i < ColorCount; i++)
            {
                _previews[i].BackColor = ColorUtil.HexToColor(_colors[i]);
                _previews[i].Invalidate();
                _hexLabels[i].Text = _colors[i].ToUpperInvariant();
            }

            Color slashCol = ColorUtil.HexToColor(_colors[IdxSlash]);

            UpdateIconPreview(0, _colors[IdxUnknown], slashCol, false);
            UpdateIconPreview(1, _colors[IdxMuted], slashCol, true);
            UpdateIconPreview(2, _colors[IdxUnmuted], slashCol, false);
        }

        private void UpdateIconPreview(int index, string micColorHex, Color slashCol, bool showSlash)
        {
            Image old = _iconPreviews[index].Image;
            _iconPreviews[index].Image = AppContext.CreateMicIcon(ColorUtil.HexToColor(micColorHex), slashCol, showSlash);
            if (old != null) old.Dispose();
        }

        private void PickColor(int index)
        {
            using (ColorPickerDialog dlg = new ColorPickerDialog(_colors[index], _darkMode))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _colors[index] = dlg.SelectedColorHex;
                    UpdatePreviews();
                }
            }
        }

        private void OnRestoreDefaults(object sender, EventArgs e)
        {
            _colors[IdxUnknown] = "#888888";
            _colors[IdxMuted] = "#F48FB1";
            _colors[IdxUnmuted] = "#4FC3F7";
            _colors[IdxSlash] = "#ECECEC";
            UpdatePreviews();
        }

        private void OnCancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void OnSave(object sender, EventArgs e)
        {
            _cfg.UnknownColor = _colors[IdxUnknown];
            _cfg.MutedColor = _colors[IdxMuted];
            _cfg.UnmutedColor = _colors[IdxUnmuted];
            _cfg.SlashColor = _colors[IdxSlash];
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

                // 先脱离 PictureBox 的 Image 引用再 dispose，防止 base.Dispose 二次释放
                for (int i = 0; i < _iconPreviews.Length; i++)
                {
                    if (_iconPreviews[i] != null)
                    {
                        Image img = _iconPreviews[i].Image;
                        _iconPreviews[i].Image = null;
                        if (img != null) { try { img.Dispose(); } catch (Exception) { } }
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
