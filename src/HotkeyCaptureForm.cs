// HotkeyCaptureForm.cs — 快捷键捕获对话框
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VRCMicToggle
{
    internal class HotkeyCaptureForm : Form
    {
        public uint Key;
        public uint Modifiers;

        // ── 字段 ──────────────────────────────────────────

        private readonly IntPtr _targetHandle;
        private uint _capturedMods;
        private Keys _capturedKey;
        private bool _hasMainKey;
        private Label _comboLabel;
        private readonly List<Font> _ownedFonts = new List<Font>();

        public HotkeyCaptureForm(IntPtr targetHandle)
        {
            _targetHandle = targetHandle;
            BuildUi();
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
            }
            base.Dispose(disposing);
        }

        // ── UI 构建 ────────────────────────────────────────

        private void BuildUi()
        {
            Text = Lang.HotkeyFormTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 170);
            KeyPreview = true;
            ShowInTaskbar = false;
            Font = TrackFont(new Font("Segoe UI", 9f));

            _comboLabel = new Label
            {
                Text = Lang.ComboWaiting,
                Font = TrackFont(new Font("Segoe UI", 11.5f, FontStyle.Bold)),
                Location = new Point(10, 14),
                Size = new Size(380, 36),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_comboLabel);

            var hint = new Label
            {
                Text = Lang.HotkeyHint,
                Location = new Point(10, 54),
                Size = new Size(380, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(128, 128, 128)
            };
            Controls.Add(hint);

            int btnY = 112;
            var clearBtn = ColorUtil.MakeButton(Lang.BtnClear, OnClear, false, false, Color.Black);
            var confirmBtn = ColorUtil.MakeButton(Lang.BtnConfirm, OnConfirm, true, false, Color.Black);
            var cancelBtn = ColorUtil.MakeButton(Lang.BtnCancel, OnCancel, false, false, Color.Black);

            int gap = 20;
            int totalW = clearBtn.Width + confirmBtn.Width + cancelBtn.Width + gap * 2;
            int x = (ClientSize.Width - totalW) / 2;
            clearBtn.Location = new Point(x, btnY);
            confirmBtn.Location = new Point(x + clearBtn.Width + gap, btnY);
            cancelBtn.Location = new Point(x + clearBtn.Width + gap + confirmBtn.Width + gap, btnY);

            Controls.Add(clearBtn);
            Controls.Add(confirmBtn);
            Controls.Add(cancelBtn);
        }

        // ── 按键捕获 ──────────────────────────────────────

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Enter)
            {
                if (_hasMainKey) OnConfirm(this, EventArgs.Empty);
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                if (_hasMainKey || _capturedMods != 0) ClearCapture();
                else { DialogResult = DialogResult.Cancel; Close(); }
                return;
            }

            uint mods = 0;
            if ((e.Modifiers & Keys.Control) == Keys.Control) mods |= AppContext.MOD_CONTROL;
            if ((e.Modifiers & Keys.Alt) == Keys.Alt) mods |= AppContext.MOD_ALT;
            if ((e.Modifiers & Keys.Shift) == Keys.Shift) mods |= AppContext.MOD_SHIFT;
            bool winHeld = (AppContext.GetAsyncKeyState(AppContext.VK_LWIN) & 0x8000) != 0
                        || (AppContext.GetAsyncKeyState(AppContext.VK_RWIN) & 0x8000) != 0;
            if (winHeld) mods |= AppContext.MOD_WIN;

            _capturedMods = mods;
            if (!IsModifierKey(e.KeyCode))
            {
                _capturedKey = e.KeyCode;
                _hasMainKey = true;
            }

            RefreshDisplay();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
        }

        // ── 显示刷新 ──────────────────────────────────────

        private void RefreshDisplay()
        {
            var sb = new StringBuilder();
            if ((_capturedMods & AppContext.MOD_CONTROL) != 0) sb.Append("Ctrl + ");
            if ((_capturedMods & AppContext.MOD_ALT) != 0) sb.Append("Alt + ");
            if ((_capturedMods & AppContext.MOD_SHIFT) != 0) sb.Append("Shift + ");
            if ((_capturedMods & AppContext.MOD_WIN) != 0) sb.Append("Win + ");
            sb.Append(_hasMainKey ? AppContext.KeyName((uint)_capturedKey) : Lang.WaitingMainKey);
            _comboLabel.Text = Lang.ComboPrefix + sb.ToString();
        }

        // ── 按钮事件 ──────────────────────────────────────

        private void OnConfirm(object sender, EventArgs e)
        {
            if (!_hasMainKey)
            {
                ShowMsg(Lang.NeedMainKeyMsg);
                return;
            }

            if (_capturedMods == 0)
            {
                var r = MessageBox.Show(this,
                    Lang.NoModWarningMsg,
                    Lang.WarningTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.No) return;
            }

            uint vk = (uint)_capturedKey;
            uint mods = _capturedMods;

            // 冲突预检：试注册 → 立即注销
            if (!AppContext.RegisterHotKey(_targetHandle, AppContext.HOTKEY_ID, mods | AppContext.MOD_NOREPEAT, vk))
            {
                int err = Marshal.GetLastWin32Error();
                ShowMsg(string.Format(Lang.HotkeyConflictMsg, err));
                return;
            }
            AppContext.UnregisterHotKey(_targetHandle, AppContext.HOTKEY_ID);

            Key = vk;
            Modifiers = mods;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnClear(object sender, EventArgs e) { ClearCapture(); }

        private void ClearCapture()
        {
            _capturedMods = 0;
            _capturedKey = default(Keys);
            _hasMainKey = false;
            RefreshDisplay();
        }

        private void OnCancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // ── 辅助 ──────────────────────────────────────────

        private void ShowMsg(string text)
        {
            MessageBox.Show(this, text, Lang.InfoTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static bool IsModifierKey(Keys keyCode)
        {
            switch (keyCode)
            {
                case Keys.ControlKey: case Keys.LControlKey: case Keys.RControlKey:
                case Keys.Menu: case Keys.LMenu: case Keys.RMenu:
                case Keys.ShiftKey: case Keys.LShiftKey: case Keys.RShiftKey:
                case Keys.LWin: case Keys.RWin:
                    return true;
                default:
                    return false;
            }
        }
    }
}
