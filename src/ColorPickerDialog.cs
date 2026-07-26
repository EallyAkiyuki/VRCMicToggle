// ColorPickerDialog.cs — HSV 颜色选择器（含预设/最近色板）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VRCMicToggle
{
    // HSV 颜色选择器：色相条 + 饱和度/亮度面板 + 预设/最近色板
    internal sealed class ColorPickerDialog : Form
    {
        private const string RegKey = @"Software\VRCMicToggle\RecentColors";
        private const int SL_SIZE = 128;
        private const int HUE_H = 20;
        private const int SWATCH = 16;
        private const int SWATCH_GAP = 4;
        private const int SWATCH_STEP = SWATCH + SWATCH_GAP;
        private const int SWATCH_COLS = 8;
        private const int MAX_RECENT = 12;

        private Font _uiFont;
        private Font _uiFontBold;
        private Font _uiFontSmall;
        private Font _consFont;
        private Font _consFontSmall;

        private readonly List<Font> _ownedFonts = new List<Font>();

        private static readonly string[] PresetColors = new string[]
        {
            "#FF0000", "#FF8800", "#FFFF00", "#00FF00", "#00FFFF", "#0088FF", "#8800FF", "#FF00FF",
            "#FF4444", "#FFAA44", "#FFFF44", "#44FF44", "#44FFFF", "#44AAFF", "#AA44FF", "#FF44FF",
            "#880000", "#884400", "#888800", "#008800", "#008888", "#004488", "#440088", "#880088",
            "#440000", "#442200", "#444400", "#004400", "#004444", "#002244", "#220044", "#440044",
            "#FFFFFF", "#CCCCCC", "#888888", "#444444", "#000000", "#F48FB1", "#4FC3F7", "#81C784"
        };

        private static Color[] _presetColorsParsed;

        private double _hue, _saturation, _value;
        private double _lastRenderedHue = -1;
        private string _initialColorHex;

        private Panel _slPanel;
        private DbPanel _huePanel;
        private DbPanel _previewPanel;
        private TextBox _hexInput;
        private TextBox _rInput, _gInput, _bInput;
        private DbPanel _presetPanel;
        private DbPanel _recentPanel;
        private List<string> _recentColors;
        private Color[] _recentColorsParsed;

        private Bitmap _slBitmap;
        private Bitmap _hueBitmap;
        private Font _previewFont;
        private bool _darkMode;        private Color _fg, _bg, _subFg, _borderCol, _inputBg;

        private bool _slDragging, _hueDragging;

        public string SelectedColorHex { get; private set; }

        // ── 构造 / 主题 ─────────────────────────────────

        public ColorPickerDialog(string initialColor, bool darkMode)
        {
            _uiFont = TrackFont(new Font("Segoe UI", 9f));
            _uiFontBold = TrackFont(new Font("Segoe UI", 9f, FontStyle.Bold));
            _uiFontSmall = TrackFont(new Font("Segoe UI", 8f));
            _consFont = TrackFont(new Font("Consolas", 10f));
            _consFontSmall = TrackFont(new Font("Consolas", 9.5f));
            _previewFont = TrackFont(new Font("Segoe UI", 7.5f));

            _darkMode = darkMode;
            ApplyTheme();
            SelectedColorHex = initialColor;
            _initialColorHex = initialColor;
            HexToHsv(initialColor, out _hue, out _saturation, out _value);
            _recentColors = LoadRecentColors();
            _recentColorsParsed = ParseColors(_recentColors);
            if (_presetColorsParsed == null)
            {
                Color[] parsed = new Color[PresetColors.Length];
                for (int i = 0; i < PresetColors.Length; i++)
                    parsed[i] = ColorUtil.HexToColor(PresetColors[i]);
                System.Threading.Interlocked.CompareExchange(ref _presetColorsParsed, parsed, null);
            }
            BuildUI();
            UpdateFromHsv();
        }

        private static Color[] ParseColors(IList<string> hexColors)
        {
            Color[] colors = new Color[hexColors.Count];
            for (int i = 0; i < hexColors.Count; i++)
                colors[i] = ColorUtil.HexToColor(hexColors[i]);
            return colors;
        }

        private void ApplyTheme()
        {
            Theme t = Theme.Create(_darkMode);
            _bg = t.Bg;
            _fg = t.Fg;
            _borderCol = t.BorderCol;
            _subFg = t.SubFg;
            _inputBg = t.InputBg;
        }

        private void BuildUI()
        {
            Text = Lang.ColorPickerTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            BackColor = _bg;
            Font = _uiFont;

            int pad = 20;
            int leftW = SL_SIZE;
            int gap = 20;
            int rightW = 220;
            int totalW = pad + leftW + gap + rightW + pad;

            _slPanel = new DbPanel
            {
                Location = new Point(pad, pad),
                Size = new Size(SL_SIZE, SL_SIZE),
                Cursor = Cursors.Cross
            };
            _slPanel.Paint += SlPanelPaint;
            _slPanel.MouseDown += SlMouseDown;
            _slPanel.MouseMove += SlMouseMove;
            _slPanel.MouseUp += SlMouseUp;
            Controls.Add(_slPanel);

            int slBottom = pad + SL_SIZE;
            int hueLabelY = slBottom + 10;
            Label hueLabel = new Label
            {
                Text = Lang.HueLabel,
                ForeColor = _subFg,
                Location = new Point(pad, hueLabelY),
                AutoSize = true
            };
            Controls.Add(hueLabel);

            int hueBarY = hueLabelY + 22;
            _huePanel = new DbPanel
            {
                Location = new Point(pad, hueBarY),
                Size = new Size(SL_SIZE, HUE_H),
                Cursor = Cursors.Hand
            };
            _huePanel.Paint += HuePanelPaint;
            _huePanel.MouseDown += HueMouseDown;
            _huePanel.MouseMove += HueMouseMove;
            _huePanel.MouseUp += HueMouseUp;
            Controls.Add(_huePanel);

            int rx = pad + leftW + gap;
            int ry = pad;

            Label previewTitle = new Label
            {
                Text = Lang.PreviewLabel,
                Font = _uiFontBold,
                ForeColor = _fg,
                Location = new Point(rx, ry),
                AutoSize = true
            };
            Controls.Add(previewTitle);
            ry += 22;

            _previewPanel = new DbPanel
            {
                Location = new Point(rx, ry),
                Size = new Size(rightW, 54)
            };
            _previewPanel.Paint += PreviewPaint;
            Controls.Add(_previewPanel);
            ry += 54 + 18;

            _hexInput = new TextBox
            {
                Location = new Point(rx, ry),
                Size = new Size(rightW, 24),
                Font = _consFont,
                Text = SelectedColorHex,
                BackColor = _inputBg,
                ForeColor = _fg,
                BorderStyle = BorderStyle.FixedSingle,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 7
            };
            _hexInput.LostFocus += (s, e) => ApplyHexInput();
            _hexInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyHexInput(); };
            Controls.Add(_hexInput);
            ry += 30;

            Label rgbLabel = new Label
            {
                Text = "RGB",
                ForeColor = _subFg,
                Location = new Point(rx, ry + 4),
                Size = new Size(40, 20)
            };
            Controls.Add(rgbLabel);

            _rInput = MakeRgbInput(rx + 60, ry, "R");
            _gInput = MakeRgbInput(rx + 116, ry, "G");
            _bInput = MakeRgbInput(rx + 172, ry, "B");
            ry += 32;

            Label presetLabel = new Label
            {
                Text = Lang.PresetLabel,
                Font = _uiFontBold,
                ForeColor = _fg,
                Location = new Point(rx, ry),
                AutoSize = true
            };
            Controls.Add(presetLabel);
            ry += 20;

            int presetRows = (PresetColors.Length + SWATCH_COLS - 1) / SWATCH_COLS;
            _presetPanel = new DbPanel
            {
                Location = new Point(rx, ry),
                Size = new Size(rightW, presetRows * SWATCH_STEP + 4)
            };
            _presetPanel.Paint += PresetPanelPaint;
            _presetPanel.MouseDown += PresetPanelMouseDown;
            Controls.Add(_presetPanel);
            ry += _presetPanel.Height + 14;

            Label recentLabel = new Label
            {
                Text = Lang.RecentLabel,
                Font = _uiFontBold,
                ForeColor = _fg,
                Location = new Point(rx, ry),
                AutoSize = true
            };
            Controls.Add(recentLabel);
            ry += 20;

            int recentRows = Math.Max(1, (_recentColors.Count + SWATCH_COLS - 1) / SWATCH_COLS);
            if (_recentColors.Count == 0) recentRows = 1;
            _recentPanel = new DbPanel
            {
                Location = new Point(rx, ry),
                Size = new Size(rightW, recentRows * SWATCH_STEP + 4)
            };
            _recentPanel.Paint += RecentPanelPaint;
            _recentPanel.MouseDown += RecentPanelMouseDown;
            Controls.Add(_recentPanel);
            ry += _recentPanel.Height + 18;

            Button okBtn = MakeButton(Lang.BtnSave, OnOk, true);
            Button cancelBtn = MakeButton(Lang.BtnCancel, OnCancel, false);

            // 使用 TextRenderer 测量按钮实际宽度
            int okW, cancelW;
            using (Font btnFont = new Font("Segoe UI", 9.5f))
            {
                int btnPadding = 28;
                okW = Math.Max(72, TextRenderer.MeasureText(Lang.BtnSave, btnFont).Width + btnPadding);
                cancelW = Math.Max(72, TextRenderer.MeasureText(Lang.BtnCancel, btnFont).Width + btnPadding);
            }

            // 确保右侧面板足够宽以容纳两个按钮（间距 8px，右侧留白 8px）
            int minRightW = okW + 8 + cancelW + 8;
            if (rightW < minRightW)
            {
                int extra = minRightW - rightW;
                rightW += extra;
                totalW += extra;
            }

            // 右对齐布局：Cancel → OK（8px 间距，OK 右侧留白 8px）
            okBtn.Location = new Point(rx + rightW - okW - 8, ry);
            int cancelLeft = okBtn.Left - cancelW - 8;
            cancelBtn.Location = new Point(cancelLeft, ry);

            Controls.Add(okBtn);
            Controls.Add(cancelBtn);

            ry += 46;

            ClientSize = new Size(totalW, ry);
        }

        private TextBox MakeRgbInput(int x, int y, string label)
        {
            Label lbl = new Label
            {
                Text = label,
                Font = _uiFontSmall,
                ForeColor = _subFg,
                Location = new Point(x - 18, y + 4),
                Size = new Size(16, 18),
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(lbl);

            TextBox tb = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(34, 22),
                Font = _consFontSmall,
                MaxLength = 3,
                Text = "0",
                BackColor = _inputBg,
                ForeColor = _fg,
                BorderStyle = BorderStyle.FixedSingle
            };
            tb.LostFocus += (s, e) => ApplyRgbInput();
            tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyRgbInput(); };
            Controls.Add(tb);
            return tb;
        }

        private Button MakeButton(string text, EventHandler onClick, bool isPrimary)
        {
            return ColorUtil.MakeButton(text, onClick, isPrimary, _darkMode, _fg);
        }

        // ── 绘制（SL面板 / 色相条 / 预览 / 色板）────────

        private void SlPanelPaint(object sender, PaintEventArgs e)
        {
            EnsureSlBitmap();
            e.Graphics.DrawImageUnscaled(_slBitmap, 0, 0);

            float mx = (float)(_saturation * SL_SIZE);
            float my = (float)((1.0 - _value) * SL_SIZE);
            using (var pen = new Pen(Color.White, 2.5f))
                e.Graphics.DrawEllipse(pen, mx - 7, my - 7, 14, 14);
            using (var pen = new Pen(Color.FromArgb(80, 0, 0, 0), 1.5f))
                e.Graphics.DrawEllipse(pen, mx - 7, my - 7, 14, 14);
        }

        private void EnsureSlBitmap()
        {
            if (_slBitmap != null)
            {
                double diff = Math.Abs(_hue - _lastRenderedHue);
                if (diff > 180) diff = 360 - diff;
                if (diff <= 0.5) return;
            }
            _lastRenderedHue = _hue;

            if (_slBitmap == null || _slBitmap.Width != SL_SIZE || _slBitmap.Height != SL_SIZE)
            {
                if (_slBitmap != null) _slBitmap.Dispose();
                _slBitmap = new Bitmap(SL_SIZE, SL_SIZE, PixelFormat.Format24bppRgb);
            }

            Color hueColor = HsvToRgb(_hue, 1.0, 1.0);
            byte hr = hueColor.R, hg = hueColor.G, hb = hueColor.B;
            BitmapData bd = _slBitmap.LockBits(new Rectangle(0, 0, SL_SIZE, SL_SIZE), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                unsafe
                {
                    byte* scan0 = (byte*)bd.Scan0;
                    int stride = bd.Stride;
                    for (int py = 0; py < SL_SIZE; py++)
                    {
                        double lm = 1.0 - py / (double)(SL_SIZE - 1);
                        byte* row = scan0 + py * stride;
                        for (int px = 0; px < SL_SIZE; px++)
                        {
                            double s = px / (double)(SL_SIZE - 1);
                            double sm = 1.0 - s;
                            double r = (255.0 * sm + hr * s) * lm;
                            double g = (255.0 * sm + hg * s) * lm;
                            double b = (255.0 * sm + hb * s) * lm;
                            int off = px * 3;
                            row[off] = (byte)(b + 0.5);
                            row[off + 1] = (byte)(g + 0.5);
                            row[off + 2] = (byte)(r + 0.5);
                        }
                    }
                }
            }
            finally
            {
                _slBitmap.UnlockBits(bd);
            }
        }

        private static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

        private void SlMouseDown(object sender, MouseEventArgs e) { _slDragging = true; UpdateSlFromMouse(e.Location); }
        private void SlMouseMove(object sender, MouseEventArgs e) { if (_slDragging) UpdateSlFromMouse(e.Location); }
        private void SlMouseUp(object sender, MouseEventArgs e) { _slDragging = false; }

        private void UpdateSlFromMouse(Point p)
        {
            double x = Math.Max(0, Math.Min(SL_SIZE - 1, p.X));
            double y = Math.Max(0, Math.Min(SL_SIZE - 1, p.Y));
            _saturation = x / (double)(SL_SIZE - 1);
            _value = 1.0 - (y / (double)(SL_SIZE - 1));
            UpdateFromHsv();
        }

        private void HuePanelPaint(object sender, PaintEventArgs e)
        {
            EnsureHueBitmap();
            e.Graphics.DrawImageUnscaled(_hueBitmap, 0, 0);

            float mx = (float)((_hue / 360.0) * SL_SIZE);
            e.Graphics.FillRectangle(Brushes.White, mx - 2, -1, 4, HUE_H + 2);
            e.Graphics.FillRectangle(Brushes.Black, mx - 3, -2, 1, HUE_H + 4);
            e.Graphics.FillRectangle(Brushes.Black, mx + 2, -2, 1, HUE_H + 4);
        }

        private void EnsureHueBitmap()
        {
            if (_hueBitmap != null) return;
            _hueBitmap = new Bitmap(SL_SIZE, HUE_H, PixelFormat.Format24bppRgb);
            BitmapData bd = _hueBitmap.LockBits(new Rectangle(0, 0, SL_SIZE, HUE_H), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                unsafe
                {
                    byte* scan0 = (byte*)bd.Scan0;
                    int stride = bd.Stride;
                    for (int py = 0; py < HUE_H; py++)
                    {
                        byte* row = scan0 + py * stride;
                        for (int px = 0; px < SL_SIZE; px++)
                        {
                            double h = (px / (double)SL_SIZE) * 360.0;
                            Color c = HsvToRgb(h, 1.0, 1.0);
                            int off = px * 3;
                            row[off] = c.B;
                            row[off + 1] = c.G;
                            row[off + 2] = c.R;
                        }
                    }
                }
            }
            finally
            {
                _hueBitmap.UnlockBits(bd);
            }
        }

        private void HueMouseDown(object sender, MouseEventArgs e) { _hueDragging = true; UpdateHueFromMouse(e.Location); }
        private void HueMouseMove(object sender, MouseEventArgs e) { if (_hueDragging) UpdateHueFromMouse(e.Location); }
        private void HueMouseUp(object sender, MouseEventArgs e) { _hueDragging = false; }

        private void UpdateHueFromMouse(Point p)
        {
            double x = Math.Max(0, Math.Min(SL_SIZE - 1, p.X));
            _hue = (x / (double)(SL_SIZE - 1)) * 360.0;
            UpdateFromHsv();
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            int half = _previewPanel.Width / 2;
            Color current = HsvToRgb(_hue, _saturation, _value);
            Color initial = ColorUtil.HexToColor(_initialColorHex);
            using (var br = new SolidBrush(current))
                e.Graphics.FillRectangle(br, 0, 0, half - 1, _previewPanel.Height - 1);
            using (var br = new SolidBrush(initial))
                e.Graphics.FillRectangle(br, half, 0, _previewPanel.Width - half, _previewPanel.Height - 1);

            using (var pen = new Pen(_borderCol))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, _previewPanel.Width - 1, _previewPanel.Height - 1);
                e.Graphics.DrawLine(pen, half, 0, half, _previewPanel.Height - 1);
            }

            if (_previewFont == null) return; // defensive; should always be pre-created
            using (var br = new SolidBrush(TextColorFor(current)))
                e.Graphics.DrawString(Lang.CurrentLabel, _previewFont, br, 6, 36);
            using (var br = new SolidBrush(TextColorFor(initial)))
                e.Graphics.DrawString(Lang.OriginalLabel, _previewFont, br, half + 6, 36);
        }

        private static Color TextColorFor(Color c)
        {
            int y = (299 * c.R + 587 * c.G + 114 * c.B) / 1000;
            return y > 160 ? Color.FromArgb(180, 0, 0, 0) : Color.FromArgb(220, 255, 255, 255);
        }

        private void PresetPanelPaint(object sender, PaintEventArgs e)
        {
            int offsetX = (_presetPanel.Width - SWATCH_COLS * SWATCH_STEP + 4) / 2;
            PaintSwatches(e.Graphics, _presetColorsParsed, offsetX);
        }

        private void RecentPanelPaint(object sender, PaintEventArgs e)
        {
            if (_recentColors.Count == 0)
            {
                using (var br = new SolidBrush(_subFg))
                    e.Graphics.DrawString(Lang.NoRecentColors, Font, br, 0, 2);
                return;
            }
            int cols = Math.Min(SWATCH_COLS, _recentColors.Count);
            int offsetX = (_recentPanel.Width - cols * SWATCH_STEP + 4) / 2;
            PaintSwatches(e.Graphics, _recentColorsParsed, offsetX);
        }

        private void PaintSwatches(Graphics g, Color[] colors, int offsetX)
        {
            Color borderClr = _darkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(210, 210, 210);
            using (Pen borderPen = new Pen(borderClr))
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    int col = i % SWATCH_COLS;
                    int row = i / SWATCH_COLS;
                    int x = offsetX + col * SWATCH_STEP;
                    int y = row * SWATCH_STEP;
                    using (var br = new SolidBrush(colors[i]))
                        g.FillRectangle(br, x, y, SWATCH, SWATCH);
                    g.DrawRectangle(borderPen, x, y, SWATCH - 1, SWATCH - 1);
                }
            }
        }

        private void PresetPanelMouseDown(object sender, MouseEventArgs e)
        {
            int offsetX = (_presetPanel.Width - SWATCH_COLS * SWATCH_STEP + 4) / 2;
            int idx = SwatchAt(e.Location, PresetColors.Length, offsetX);
            if (idx >= 0)
            {
                HexToHsv(PresetColors[idx], out _hue, out _saturation, out _value);
                UpdateFromHsv();
            }
        }

        private void RecentPanelMouseDown(object sender, MouseEventArgs e)
        {
            if (_recentColors.Count == 0) return;
            int cols = Math.Min(SWATCH_COLS, _recentColors.Count);
            int offsetX = (_recentPanel.Width - cols * SWATCH_STEP + 4) / 2;
            int idx = SwatchAt(e.Location, _recentColors.Count, offsetX);
            if (idx >= 0)
            {
                HexToHsv(_recentColors[idx], out _hue, out _saturation, out _value);
                UpdateFromHsv();
            }
        }

        private int SwatchAt(Point p, int count, int offsetX)
        {
            int adjustedX = p.X - offsetX;
            if (adjustedX < 0) return -1;
            int col = adjustedX / SWATCH_STEP;
            int row = p.Y / SWATCH_STEP;
            if (col < 0 || col >= SWATCH_COLS) return -1;
            int idx = row * SWATCH_COLS + col;
            if (idx < 0 || idx >= count) return -1;
            return idx;
        }

        private void UpdateFromHsv()
        {
            Color c = HsvToRgb(_hue, _saturation, _value);
            string hex = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
            SelectedColorHex = hex;

            _slPanel.Invalidate();
            _huePanel.Invalidate();
            _previewPanel.Invalidate();

            _hexInput.Text = hex;
            _rInput.Text = c.R.ToString();
            _gInput.Text = c.G.ToString();
            _bInput.Text = c.B.ToString();
        }

        private void ApplyHexInput()
        {
            string hex = _hexInput.Text.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            try
            {
                Color c = ColorTranslator.FromHtml(hex);
                string normalized = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
                HexToHsv(normalized, out _hue, out _saturation, out _value);
                UpdateFromHsv();
            }
            catch (ArgumentException) { }
        }

        private void ApplyRgbInput()
        {
            byte r, g, b;
            if (byte.TryParse(_rInput.Text, out r) &&
                byte.TryParse(_gInput.Text, out g) &&
                byte.TryParse(_bInput.Text, out b))
            {
                string hex = string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
                HexToHsv(hex, out _hue, out _saturation, out _value);
                UpdateFromHsv();
            }
        }

        private void OnOk(object sender, EventArgs e)
        {
            SaveRecentColor(SelectedColorHex);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnCancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_slBitmap != null) { _slBitmap.Dispose(); _slBitmap = null; }
                if (_hueBitmap != null) { _hueBitmap.Dispose(); _hueBitmap = null; }

                for (int i = 0; i < _ownedFonts.Count; i++)
                {
                    try { _ownedFonts[i].Dispose(); } catch (Exception) { }
                }
                _ownedFonts.Clear();
            }
            base.Dispose(disposing);
        }

        private Font TrackFont(Font f)
        {
            _ownedFonts.Add(f);
            return f;
        }

        // ── HSV / RGB / Hex 转换 ────────────────────────

        private static Color HsvToRgb(double h, double s, double v)
        {
            h = h % 360;
            if (h < 0) h += 360;
            double c = v * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = v - c;

            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }

            return Color.FromArgb(
                Clamp((int)Math.Round((r + m) * 255)),
                Clamp((int)Math.Round((g + m) * 255)),
                Clamp((int)Math.Round((b + m) * 255)));
        }

        private static void HexToHsv(string hex, out double h, out double s, out double v)
        {
            try
            {
                Color c = ColorTranslator.FromHtml(hex);
                double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                double delta = max - min;

                v = max;

                if (delta == 0)
                {
                    h = 0; s = 0;
                }
                else
                {
                    s = max == 0 ? 0 : delta / max;
                    if (max == r) h = ((g - b) / delta + (g < b ? 6 : 0)) * 60;
                    else if (max == g) h = ((b - r) / delta + 2) * 60;
                    else h = ((r - g) / delta + 4) * 60;
                }
            }
            catch (ArgumentException)
            {
                h = 0; s = 0; v = 1.0;
            }
        }

        // ── 最近颜色持久化（注册表）──────────────────────

        private static List<string> LoadRecentColors()
        {
            List<string> list = new List<string>();
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (rk != null)
                    {
                        string[] names = rk.GetValueNames();
                        Array.Sort(names, (a, b) => {
                            int ai, bi;
                            if (int.TryParse(a, out ai) && int.TryParse(b, out bi)) return ai.CompareTo(bi);
                            return string.Compare(a, b, StringComparison.Ordinal);
                        });
                        foreach (string name in names)
                        {
                            string v = rk.GetValue(name) as string;
                            if (v != null && v.StartsWith("#")) list.Add(v);
                        }
                    }
                }
            }
            catch (System.Security.SecurityException) { }
            return list;
        }

        private static void SaveRecentColor(string hex)
        {
            try
            {
                List<string> list = LoadRecentColors();
                list.Remove(hex);
                list.Insert(0, hex);
                if (list.Count > MAX_RECENT) list = list.GetRange(0, MAX_RECENT);

                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    if (rk != null)
                    {
                        foreach (string name in rk.GetValueNames())
                            rk.DeleteValue(name, false);
                        for (int i = 0; i < list.Count; i++)
                            rk.SetValue(i.ToString(), list[i], RegistryValueKind.String);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
        }
    }
}
