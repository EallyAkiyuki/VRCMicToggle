using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
namespace VRCMicToggle
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            if (Environment.OSVersion.Version >= new Version(6, 0, 0))
            {
                try { SetProcessDPIAware(); } catch (Exception) { }
            }
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\VRCMicToggleSingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("VRCMic已在运行，新实例即将退出~", "VRCMic", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContext());
            }
        }
    }

    internal sealed class AppContext : ApplicationContext
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 1;

        private const string VrcAddress = "/input/Voice";
        private const int VrcPort = 9000;
        private const int ListenPort = 9001;
        private const int ListenPortRetryCount = 3;
        private const string MuteSelfAddress = "/avatar/parameters/MuteSelf";
        private const int OscToggleDelayMs = 60;
        private const int BalloonTipDurationMs = 2000;
        private const int MaxTooltipLength = 63;
        private const int IconBaseSize = 24;
        private const float IconScaleFactor = 1.375f;
        private const int ParseMaxDepth = 8;

        private NotifyIcon _notify;
        private ToolStripMenuItem _statusItem;
        private ToolStripMenuItem _startupItem;
        private HotkeyWindow _window;
        private UdpClient _sender;
        private UdpClient _listener;
        private readonly object _listenerLock = new object();
        private volatile bool _tracking;
        private int _activeListenPort;
        private readonly List<System.Threading.Timer> _pendingTimers = new List<System.Threading.Timer>();
        private bool? _muted;
        private Config _config;
        private bool _disposed;
        private System.Threading.Timer _oscPollTimer;
        private int _oscNoResponseCount;
        private bool _firstPoll = true;
        private int _pollBusy;

        private Icon _cachedUnknown;
        private Icon _cachedMuted;
        private Icon _cachedUnmuted;
        private IntPtr _hUnknown;
        private IntPtr _hMuted;
        private IntPtr _hUnmuted;

        private string _cachedHotkeyDisplay;
        private string _cachedStatusText;
        private string _cachedTipText;
        private byte[] _oscOnMsg;
        private byte[] _oscOffMsg;
        private static readonly KeysConverter SharedKeysConverter = new KeysConverter();

        private enum IconState { Unknown, Muted, Unmuted }

        public AppContext()
        {
            _config = Config.Load();
            _muted = null;

            _sender = new UdpClient();
            _sender.Connect(IPAddress.Loopback, VrcPort);

            _oscOnMsg = OscEncode(VrcAddress, 1);
            _oscOffMsg = OscEncode(VrcAddress, 0);

            _window = new HotkeyWindow();
            _window.HotkeyPressed += OnHotkeyPressed;
            IntPtr dummy = _window.Handle;

            BuildIconCache();
            InvalidateHotkeyDisplay();
            BuildTray();
            ApplyHotkey();
            StartListener();

            if (!Config.Exists())
            {
                SetHotkeyDialog();
            }

            ShowTip("VRC 麦克风切换已启动。快捷键：" + _cachedHotkeyDisplay);

            _oscPollTimer = new System.Threading.Timer(_ => PollOscState(), null, 2000, 5000);
            lock (_pendingTimers) { _pendingTimers.Add(_oscPollTimer); }
        }

        // 每 5 秒主动检测一次 OSC / 麦克风状态：
        // - 只有首次启动的那一次轮询才尝试弹窗（且需检测到 VRChat 进程），
        //   之后的轮询只更新状态，绝不弹窗；
        // - VRChat 连续两次未响应，则把图标更新为灰色（未知状态）。
        private void PollOscState()
        {
            if (_disposed) return;
            if (Interlocked.CompareExchange(ref _pollBusy, 1, 0) != 0) return;
            bool firstPoll = _firstPoll;
            _firstPoll = false;
            try
            {
                bool oscActive = CheckOscQuery();
                if (oscActive)
                {
                    bool? mic = QueryMicState();
                    if (mic.HasValue)
                    {
                        _oscNoResponseCount = 0;
                        bool m = mic.Value;
                        try { _window.BeginInvoke((MethodInvoker)delegate { UpdateMute(m); }); } catch (InvalidOperationException) { }
                        return;
                    }
                }

                // 未连接 / VRChat 未响应
                bool vrcRunning = false;
                try { Process[] procs = Process.GetProcessesByName("VRChat"); vrcRunning = procs.Length > 0; } catch (Exception) { }

                _oscNoResponseCount++;
                int noResp = _oscNoResponseCount;
                try
                {
                    _window.BeginInvoke((MethodInvoker)delegate
                    {
                        if (firstPoll && vrcRunning)
                        {
                            MessageBox.Show(
                                "VRChat已启动 但OSC未开启或VRC未响应\n\n" +
                                "请确保：\n" +
                                "1. VRChat 已启动\n" +
                                "2. 在 VRChat 动作菜单中开启 OSC（Osc > Enabled）",
                                "OSC 未连接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        if (noResp >= 2)
                        {
                            _muted = null;
                            SetIcon(IconState.Unknown);
                            UpdateStatusText();
                        }
                    });
                }
                catch (InvalidOperationException) { }
            }
            finally
            {
                Interlocked.Exchange(ref _pollBusy, 0);
            }
        }

        private static bool CheckOscQuery()
        {
            List<int> ports = GetVrcTcpPorts();
            if (ports.Count == 0) return false;
            foreach (int port in ports)
            {
                if (TryOscQueryProbe(port)) return true;
            }
            return false;
        }

        private static bool TryOscQueryProbe(int port)
        {
            using (var client = new TcpClient())
            {
                var ar = client.BeginConnect(IPAddress.Loopback, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(300)) return false;
                client.EndConnect(ar);
                var stream = client.GetStream();
                stream.ReadTimeout = 500;
                byte[] req = Encoding.ASCII.GetBytes("GET / HTTP/1.0\r\n\r\n");
                stream.Write(req, 0, req.Length);
                byte[] buf = new byte[512];
                int n = stream.Read(buf, 0, buf.Length);
                string resp = Encoding.ASCII.GetString(buf, 0, n);
                return resp.IndexOf("FULL_PATH") >= 0 || resp.IndexOf("full_path") >= 0;
            }
        }

        // 返回值：true=已静音，false=已开麦，null=VRChat 未响应
        private static bool? QueryMicState()
        {
            List<int> ports = GetVrcTcpPorts();
            if (ports.Count == 0) return null;
            int port = ports[0];
            using (var client = new TcpClient())
            {
                try
                {
                    var ar = client.BeginConnect(IPAddress.Loopback, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(300)) return null;
                    client.EndConnect(ar);
                    var stream = client.GetStream();
                    stream.ReadTimeout = 500;
                    byte[] req = Encoding.ASCII.GetBytes("GET /input/Voice HTTP/1.0\r\n\r\n");
                    stream.Write(req, 0, req.Length);
                    byte[] buf = new byte[512];
                    int n = stream.Read(buf, 0, buf.Length);
                    string resp = Encoding.ASCII.GetString(buf, 0, n);
                    int valIdx = resp.IndexOf("\"VALUE\"");
                    if (valIdx < 0) return null;
                    string valPart = resp.Substring(valIdx);
                    return valPart.IndexOf("[false]") >= 0;
                }
                catch (Exception) { return null; }
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, int ulReserved);

        private const int TCP_TABLE_OWNER_PID_LISTEN = 3;
        private const int AF_INET = 2;

        private static List<int> GetVrcTcpPorts()
        {
            List<int> ports = new List<int>();
            HashSet<int> vrcPids = new HashSet<int>();
            try { foreach (Process p in Process.GetProcessesByName("VRChat")) { try { vrcPids.Add(p.Id); } catch (Exception) { } } } catch (Exception) { }
            if (vrcPids.Count == 0) return ports;
            int size = 0;
            uint ret = GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_LISTEN, 0);
            if (ret != 0 && ret != 122) return ports;
            IntPtr table = IntPtr.Zero;
            try
            {
                table = Marshal.AllocHGlobal(size);
                ret = GetExtendedTcpTable(table, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_LISTEN, 0);
                if (ret != 0) return ports;
                int count = Marshal.ReadInt32(table);
                for (int i = 0; i < count; i++)
                {
                    IntPtr entry = new IntPtr(table.ToInt64() + 4 + i * 24);
                    int localPort = (Marshal.ReadByte(entry, 8) << 8) | Marshal.ReadByte(entry, 9);
                    int ownerPid = Marshal.ReadInt32(entry, 20);
                    if (vrcPids.Contains(ownerPid)) ports.Add(localPort);
                }
            }
            catch (Exception) { }
            finally { if (table != IntPtr.Zero) Marshal.FreeHGlobal(table); }
            return ports;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                try { StopListener(); } catch (ObjectDisposedException) { }
                try { UnregisterHotKey(_window.Handle, HOTKEY_ID); } catch (InvalidOperationException) { }
                try { if (_notify != null) _notify.Visible = false; } catch (InvalidOperationException) { }
                if (_notify != null)
                {
                    ContextMenuStrip menu = _notify.ContextMenuStrip;
                    _notify.Dispose();
                    _notify = null;
                    if (menu != null) menu.Dispose();
                }
                DisposeIcons();
                System.Threading.Timer[] timers;
                lock (_pendingTimers) { timers = _pendingTimers.ToArray(); _pendingTimers.Clear(); }
                for (int i = 0; i < timers.Length; i++)
                {
                    try { if (timers[i] != null) timers[i].Dispose(); } catch (ObjectDisposedException) { }
                }
                try { if (_sender != null) { _sender.Close(); _sender.Dispose(); } } catch (ObjectDisposedException) { }
                _sender = null;
                if (_window != null) { _window.Dispose(); _window = null; }
            }
            base.Dispose(disposing);
        }

        private void BuildTray()
        {
            _notify = new NotifyIcon();
            _notify.Text = "VRC 麦克风切换";
            _currentIconState = (IconState)(-1);
            SetIcon(IconState.Unknown);
            _notify.Visible = true;
            _notify.DoubleClick += (s, e) => OnHotkeyPressed();
            _notify.ContextMenuStrip = BuildMenu();
            UpdateStatusText();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            _statusItem = new ToolStripMenuItem();
            _statusItem.Click += (s, e) => OnHotkeyPressed();

            ToolStripMenuItem setHk = new ToolStripMenuItem("设置快捷键...");
            setHk.Click += (s, e) => SetHotkeyDialog();

            ToolStripMenuItem setColor = new ToolStripMenuItem("颜色设置...");
            setColor.Click += (s, e) => OpenColorSettings();

            _startupItem = new ToolStripMenuItem("开机自启");
            _startupItem.CheckOnClick = true;
            _startupItem.Checked = _config.RunOnStartup;
            _startupItem.CheckedChanged += (s, e) =>
            {
                _config.RunOnStartup = _startupItem.Checked;
                _config.Save();
                SetStartup(_config.RunOnStartup);
            };

            ToolStripMenuItem about = new ToolStripMenuItem("关于 / 帮助");
            about.Click += (s, e) => ShowAboutDialog();

            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += (s, e) => ExitApp();

            menu.Items.AddRange(new ToolStripItem[] {
                _statusItem, new ToolStripSeparator(),
                setHk, setColor, new ToolStripSeparator(),
                _startupItem, new ToolStripSeparator(),
                about, exit
            });
            return menu;
        }

        private void ShowAboutDialog()
        {
            MessageBox.Show(
                "VRC 麦克风切换工具\n\n" +
                "通过全局快捷键向 VRChat 发送 OSC 指令，切换开/静音状态。\n\n" +
                "使用前提：\n" +
                "1. 在 VRChat 动作菜单中开启 OSC（Osc > Enabled）。\n" +
                "2. VRChat 设置中保持 \"Toggle Voice\" 开启（默认）。\n" +
                "3. 工具向 127.0.0.1:" + VrcPort + " 发送 /input/Voice（1 -> 0）实现切换。\n" +
                "4. 勾选 \"显示麦克风状态\" 可监听 " + ListenPort + " 显示当前状态（可选）。\n\n" +
                "当前快捷键：" + HotkeyDisplay() + "\n\n" +
                "双击任务栏图标可快速切换麦克风状态~",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetHotkeyDialog()
        {
            UnregisterHotKey(_window.Handle, HOTKEY_ID);
            using (HotkeyCaptureForm dlg = new HotkeyCaptureForm())
            {
                DialogResult r = dlg.ShowDialog(_window);
                if (r == DialogResult.OK)
                {
                    _config.HotkeyKey = dlg.Key;
                    _config.HotkeyMods = dlg.Modifiers;
                    _config.Save();
                    InvalidateHotkeyDisplay();
                    ShowTip("快捷键已设为：" + _cachedHotkeyDisplay);
                }
            }
            ApplyHotkey();
        }

        private void OpenColorSettings()
        {
            using (SettingsWindow w = new SettingsWindow(_config))
            {
                if (w.ShowDialog(_window) == DialogResult.OK)
                {
                    BuildIconCache();
                    IconState st = IconState.Unknown;
                    if (_muted.HasValue) st = _muted.Value ? IconState.Muted : IconState.Unmuted;
                    SetIcon(st);
                }
            }
        }

        private void InvalidateHotkeyDisplay()
        {
            StringBuilder sb = new StringBuilder();
            if ((_config.HotkeyMods & MOD_CONTROL) != 0) sb.Append("Ctrl + ");
            if ((_config.HotkeyMods & MOD_ALT) != 0) sb.Append("Alt + ");
            if ((_config.HotkeyMods & MOD_SHIFT) != 0) sb.Append("Shift + ");
            if ((_config.HotkeyMods & MOD_WIN) != 0) sb.Append("Win + ");
            sb.Append(KeyName(_config.HotkeyKey));
            _cachedHotkeyDisplay = sb.ToString();
        }

        private void ApplyHotkey()
        {
            UnregisterHotKey(_window.Handle, HOTKEY_ID);
            uint vk = _config.HotkeyKey;
            if (vk == 0) { UpdateStatusText(); return; }
            bool ok = RegisterHotKey(_window.Handle, HOTKEY_ID, _config.HotkeyMods | MOD_NOREPEAT, vk);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                string msg = "注册快捷键失败：" + _cachedHotkeyDisplay + " (错误码 " + err + ")\n可能被其他程序占用，请另设一个。";
                ShowTip(msg);
                MessageBox.Show(msg, "快捷键注册失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateStatusText();
        }

        private string HotkeyDisplay()
        {
            return _cachedHotkeyDisplay ?? string.Empty;
        }

        private static string KeyName(uint vk)
        {
            try
            {
                string s = SharedKeysConverter.ConvertToString((Keys)vk);
                return string.IsNullOrEmpty(s) ? ("Key" + vk) : s;
            }
            catch (ArgumentException)
            {
                return "Key" + vk;
            }
        }

        private void OnHotkeyPressed()
        {
            UdpClient sender = _sender;
            byte[] onMsg = _oscOnMsg;
            byte[] offMsg = _oscOffMsg;
            if (sender != null && onMsg != null && offMsg != null)
            {
                try { sender.Send(onMsg, onMsg.Length); } catch (SocketException) { } catch (ObjectDisposedException) { }
                System.Threading.Timer offTimer = null;
                offTimer = new System.Threading.Timer(_ =>
                {
                    try { sender.Send(offMsg, offMsg.Length); } catch (SocketException) { } catch (ObjectDisposedException) { }
                    lock (_pendingTimers) { _pendingTimers.Remove(offTimer); }
                    offTimer.Dispose();
                }, null, OscToggleDelayMs, Timeout.Infinite);
                lock (_pendingTimers) { _pendingTimers.Add(offTimer); }
            }
        }

        private static byte[] OscEncode(string address, int value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteOscString(ms, address);
                WriteOscString(ms, ",i");
                ms.WriteByte((byte)(value >> 24));
                ms.WriteByte((byte)(value >> 16));
                ms.WriteByte((byte)(value >> 8));
                ms.WriteByte((byte)(value));
                return ms.ToArray();
            }
        }

        private static void WriteOscString(MemoryStream ms, string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            ms.Write(b, 0, b.Length);
            ms.WriteByte(0);
            int pad = (4 - ((b.Length + 1) % 4)) % 4;
            for (int i = 0; i < pad; i++) ms.WriteByte(0);
        }

        private void StartListener()
        {
            lock (_listenerLock)
            {
                if (_tracking) return;
                _tracking = true;
            }
            int[] portsToTry = new int[ListenPortRetryCount];
            for (int p = 0; p < ListenPortRetryCount; p++) portsToTry[p] = ListenPort + p;
            Exception lastEx = null;
            foreach (int port in portsToTry)
            {
                UdpClient client = null;
                try
                {
                    client = new UdpClient();
                    client.ExclusiveAddressUse = false;
                    client.Client.ReceiveBufferSize = 4096;
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    client.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    lock (_listenerLock)
                    {
                        if (!_tracking) { try { client.Close(); } catch (ObjectDisposedException) { } return; }
                        _listener = client;
                        _activeListenPort = port;
                    }
                    client.BeginReceive(ListenerReceive, null);
                    UpdateStatusText();
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    try { if (client != null) client.Close(); } catch (ObjectDisposedException) { }
                }
            }
            lock (_listenerLock) { _tracking = false; }
            ShowTip("无法监听 " + ListenPort + "-" + (ListenPort + ListenPortRetryCount - 1) + " 端口：" + (lastEx != null ? lastEx.Message : "未知错误") + "\n（可能被其他 OSC 工具占用；切换功能不受影响）");
        }

        private void StopListener()
        {
            UdpClient toClose = null;
            lock (_listenerLock)
            {
                if (!_tracking) return;
                _tracking = false;
                toClose = _listener;
                _listener = null;
                _activeListenPort = 0;
            }
            try { if (toClose != null) toClose.Close(); } catch (ObjectDisposedException) { }
            _muted = null;
            SetIcon(IconState.Unknown);
            UpdateStatusText();
        }

        private void ListenerReceive(IAsyncResult ar)
        {
            UdpClient client;
            lock (_listenerLock)
            {
                if (!_tracking) return;
                client = _listener;
            }
            byte[] data = null;
            bool recvOk = false;
            try
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 0);
                data = client.EndReceive(ar, ref ep);
                recvOk = true;
            }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { }
            finally
            {
                bool stillTracking;
                lock (_listenerLock) { stillTracking = _tracking; }
                if (stillTracking)
                {
                    bool requeued = false;
                    try { client.BeginReceive(ListenerReceive, null); requeued = true; }
                    catch (ObjectDisposedException) { }
                    catch (SocketException) { }
                    if (!requeued)
                    {
                        lock (_listenerLock) { _tracking = false; _listener = null; _activeListenPort = 0; }
                        try
                        {
                            _window.BeginInvoke((MethodInvoker)delegate
                            {
                                SetIcon(IconState.Unknown);
                                UpdateStatusText();
                                ShowTip("监听器意外停止，可在菜单中重新勾选\"显示麦克风状态\"以恢复。");
                            });
                        }
                        catch (InvalidOperationException) { }
                    }
                }
            }
            if (recvOk && data != null && data.Length > 0)
            {
                try { ParsePacket(data, 0, 0, data.Length); } catch (ArgumentException) { }
            }
        }

        private unsafe void ParsePacket(byte[] data, int base0, int start, int end)
        {
            const int MAX_DEPTH = ParseMaxDepth;
            int* sStart = stackalloc int[MAX_DEPTH];
            int* sEnd = stackalloc int[MAX_DEPTH];
            int* sBase = stackalloc int[MAX_DEPTH];
            int sp = 0;
            sStart[0] = start; sEnd[0] = end; sBase[0] = base0;
            while (sp >= 0)
            {
                int curStart = sStart[sp];
                int curEnd = sEnd[sp];
                int curBase = sBase[sp];
                sp--;
                int i = curStart;
                string address;
                if (!TryReadOscString(data, curBase, ref i, curEnd, out address)) continue;
                if (address.Length > 0 && address[0] == '#')
                {
                    if (i + 8 > curEnd) continue;
                    i += 8;
                    while (i + 4 <= curEnd)
                    {
                        int size = ReadInt32(data, ref i, curEnd);
                        if (size <= 0 || size > curEnd - i) break;
                        int elStart = i;
                        if (sp + 1 >= MAX_DEPTH) break;
                        sp++;
                        sStart[sp] = elStart;
                        sEnd[sp] = elStart + size;
                        sBase[sp] = elStart;
                        i += size;
                    }
                    continue;
                }
                string typeTag;
                if (!TryReadOscString(data, curBase, ref i, curEnd, out typeTag)) continue;
                if (typeTag.Length == 0 || typeTag[0] != ',') continue;
                if (address == MuteSelfAddress)
                {
                    bool muted = false;
                    bool ok = false;
                    if (typeTag.Length > 1)
                    {
                        char c = typeTag[1];
                        if (c == 'T') { muted = true; ok = true; }
                        else if (c == 'F') { muted = false; ok = true; }
                        else if (c == 'i') { i = Align4(curBase, i); muted = ReadInt32(data, ref i, curEnd) != 0; ok = true; }
                        else if (c == 'f') { i = Align4(curBase, i); muted = ReadFloat32(data, ref i, curEnd) != 0f; ok = true; }
                    }
                    if (ok)
                    {
                        bool m = muted;
                        try { _window.BeginInvoke((MethodInvoker)delegate { UpdateMute(m); }); } catch (InvalidOperationException) { }
                    }
                }
            }
        }

        private static bool TryReadOscString(byte[] data, int base0, ref int i, int end, out string s)
        {
            s = null;
            int startStr = i;
            while (i < end && data[i] != 0) i++;
            if (i >= end) return false;
            s = Encoding.UTF8.GetString(data, startStr, i - startStr);
            i++;
            i = Align4(base0, i);
            return true;
        }

        private static int Align4(int base0, int i)
        {
            int r = i - base0;
            r = ((r + 3) >> 2) << 2;
            return base0 + r;
        }

        private static int ReadInt32(byte[] data, ref int i, int end)
        {
            if (i + 4 > end) { i = end; return 0; }
            int v = (data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3];
            i += 4;
            return v;
        }

        private static unsafe float ReadFloat32(byte[] data, ref int i, int end)
        {
            if (i + 4 > end) { i = end; return 0f; }
            int bits;
            fixed (byte* p = &data[i])
            {
                if (BitConverter.IsLittleEndian)
                    bits = (p[0] << 24) | (p[1] << 16) | (p[2] << 8) | p[3];
                else
                    bits = (p[3] << 24) | (p[2] << 16) | (p[1] << 8) | p[0];
            }
            i += 4;
            return *(float*)&bits;
        }

        private void UpdateMute(bool muted)
        {
            if (_muted.HasValue && _muted.Value == muted) return;
            _muted = muted;
            SetIcon(muted ? IconState.Muted : IconState.Unmuted);
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            string state = (_muted.HasValue ? (_muted.Value ? "已静音" : "已开麦") : "状态未知");
            string newText = "麦克风：" + state;
            if (newText != _cachedStatusText)
            {
                _cachedStatusText = newText;
                _statusItem.Text = newText;
            }
            string tip = "VRC 麦克风 - " + state + " | " + HotkeyDisplay();
            if (tip.Length > MaxTooltipLength)
            {
                tip = "麦克风:" + state;
                if (tip.Length > MaxTooltipLength) tip = tip.Substring(0, MaxTooltipLength);
            }
            if (tip != _cachedTipText)
            {
                _cachedTipText = tip;
                _notify.Text = tip;
            }
        }

        private void BuildIconCache()
        {
            Icon oldUnknown = _cachedUnknown;
            Icon oldMuted = _cachedMuted;
            Icon oldUnmuted = _cachedUnmuted;
            IntPtr hOldUnknown = _hUnknown;
            IntPtr hOldMuted = _hMuted;
            IntPtr hOldUnmuted = _hUnmuted;
            _cachedUnknown = null;
            _cachedMuted = null;
            _cachedUnmuted = null;
            _hUnknown = IntPtr.Zero;
            _hMuted = IntPtr.Zero;
            _hUnmuted = IntPtr.Zero;

            Color unknownCol = ColorUtil.HexToColor(_config.UnknownColor);
            Color mutedCol = ColorUtil.HexToColor(_config.MutedColor);
            Color unmutedCol = ColorUtil.HexToColor(_config.UnmutedColor);
            Color slashCol = ColorUtil.HexToColor(_config.SlashColor);

            Bitmap bmp;
            bmp = CreateMicIcon(unknownCol, slashCol, false);
            _hUnknown = bmp.GetHicon();
            _cachedUnknown = Icon.FromHandle(_hUnknown);
            bmp.Dispose();

            bmp = CreateMicIcon(mutedCol, slashCol, true);
            _hMuted = bmp.GetHicon();
            _cachedMuted = Icon.FromHandle(_hMuted);
            bmp.Dispose();

            bmp = CreateMicIcon(unmutedCol, slashCol, false);
            _hUnmuted = bmp.GetHicon();
            _cachedUnmuted = Icon.FromHandle(_hUnmuted);
            bmp.Dispose();

            IconState st = IconState.Unknown;
            if (_muted.HasValue) st = _muted.Value ? IconState.Muted : IconState.Unmuted;
            _currentIconState = (IconState)(-1);
            SetIcon(st);

            if (oldUnknown != null) { try { oldUnknown.Dispose(); } catch (InvalidOperationException) { } }
            if (oldMuted != null) { try { oldMuted.Dispose(); } catch (InvalidOperationException) { } }
            if (oldUnmuted != null) { try { oldUnmuted.Dispose(); } catch (InvalidOperationException) { } }
            if (hOldUnknown != IntPtr.Zero) DestroyIcon(hOldUnknown);
            if (hOldMuted != IntPtr.Zero) DestroyIcon(hOldMuted);
            if (hOldUnmuted != IntPtr.Zero) DestroyIcon(hOldUnmuted);
        }

        private void DisposeIcons()
        {
            if (_cachedUnknown != null) { try { _cachedUnknown.Dispose(); } catch (InvalidOperationException) { } _cachedUnknown = null; }
            if (_cachedMuted != null) { try { _cachedMuted.Dispose(); } catch (InvalidOperationException) { } _cachedMuted = null; }
            if (_cachedUnmuted != null) { try { _cachedUnmuted.Dispose(); } catch (InvalidOperationException) { } _cachedUnmuted = null; }
            if (_hUnknown != IntPtr.Zero) { DestroyIcon(_hUnknown); _hUnknown = IntPtr.Zero; }
            if (_hMuted != IntPtr.Zero) { DestroyIcon(_hMuted); _hMuted = IntPtr.Zero; }
            if (_hUnmuted != IntPtr.Zero) { DestroyIcon(_hUnmuted); _hUnmuted = IntPtr.Zero; }
        }

        private IconState _currentIconState = (IconState)(-1);

        private void SetIcon(IconState state)
        {
            if (state == _currentIconState) return;
            _currentIconState = state;
            if (_notify == null) return;
            switch (state)
            {
                case IconState.Muted: _notify.Icon = _cachedMuted; break;
                case IconState.Unmuted: _notify.Icon = _cachedUnmuted; break;
                default: _notify.Icon = _cachedUnknown; break;
            }
        }

        public static Bitmap CreateMicIcon(Color micColor, Color slashColor, bool showSlash)
        {
            const int size = IconBaseSize;
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(size / (float)IconBaseSize, size / (float)IconBaseSize);
                g.TranslateTransform(12f, 12f);
                g.ScaleTransform(IconScaleFactor, IconScaleFactor);
                g.TranslateTransform(-12f, -12f);

                using (GraphicsPath head = ColorUtil.CreateRoundedRect(8f, 3.5f, 8f, 11f, 3f))
                {
                    using (SolidBrush br = new SolidBrush(micColor)) g.FillPath(br, head);
                    using (Pen pen = new Pen(micColor, 1.5f)) g.DrawPath(pen, head);
                }

                using (GraphicsPath arc = new GraphicsPath())
                {
                    arc.AddBezier(6f, 11.5f, 6f, 15f, 8.5f, 17f, 12f, 17f);
                    arc.AddBezier(12f, 17f, 15.5f, 17f, 18f, 15f, 18f, 11.5f);
                    using (Pen pen = new Pen(micColor, 1.5f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawPath(pen, arc);
                    }
                }

                using (Pen pen = new Pen(micColor, 1.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 12f, 18f, 12f, 20f);
                    g.DrawLine(pen, 9f, 20f, 15f, 20f);
                }

                if (showSlash)
                {
                    using (Pen pen = new Pen(slashColor, 2.5f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 4f, 4f, 20f, 20f);
                    }
                }
            }
            return bmp;
        }

        private void ShowTip(string msg)
        {
            try { _notify.ShowBalloonTip(BalloonTipDurationMs, "VRC 麦克风切换", msg, ToolTipIcon.Info); } catch (InvalidOperationException) { }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                const string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(key, true))
                {
                    if (rk == null) return;
                    if (enable) rk.SetValue("VRCMicToggle", Application.ExecutablePath);
                    else rk.DeleteValue("VRCMicToggle", false);
                }
            }
            catch (UnauthorizedAccessException ex) { AppLogger.Log("SetStartup", ex); }
            catch (System.Security.SecurityException ex) { AppLogger.Log("SetStartup", ex); }
        }

        private void ExitApp()
        {
            Dispose(true);
            Application.Exit();
        }

        private class HotkeyWindow : Form
        {
            public event Action HotkeyPressed;

            public HotkeyWindow()
            {
                ShowInTaskbar = false;
                FormBorderStyle = FormBorderStyle.None;
                Opacity = 0;
                Text = "VRCMicToggleWindow";
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    if ((int)m.WParam == HOTKEY_ID)
                    {
                        Action h = HotkeyPressed;
                        if (h != null) h();
                    }
                }
                base.WndProc(ref m);
            }
        }

        private class HotkeyCaptureForm : Form
        {
            public uint Key;
            public uint Modifiers;
            private Label _label;

            public HotkeyCaptureForm()
            {
                Text = "设置快捷键";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(380, 140);
                KeyPreview = true;
                ShowInTaskbar = false;

                _label = new Label();
                _label.Text = "请按下要设置的快捷键组合\n（仅支持键盘，按 Esc 取消）\n\n默认使用Ins键~";
                _label.Location = new Point(20, 14);
                _label.Size = new Size(340, 112);
                _label.TextAlign = ContentAlignment.MiddleCenter;
                Controls.Add(_label);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu ||
                    e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LWin ||
                    e.KeyCode == Keys.RWin)
                {
                    string modText = e.Modifiers != Keys.None ? e.Modifiers.ToString() : "";
                    if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0)
                        modText = string.IsNullOrEmpty(modText) ? "Windows" : modText + ", Windows";
                    _label.Text = "修饰键：Ctrl / Alt / Shift / Win + ..." +
                        (!string.IsNullOrEmpty(modText) ? ("\n当前：" + modText) : "");
                    return;
                }
                if (e.Modifiers == Keys.None)
                {
                    _label.Text = "请至少包含一个修饰键（Ctrl/Alt/Shift/Win）\n\n单独按 Esc 可取消";
                    return;
                }
                uint mods = 0;
                if ((e.Modifiers & Keys.Shift) == Keys.Shift) mods |= MOD_SHIFT;
                if ((e.Modifiers & Keys.Control) == Keys.Control) mods |= MOD_CONTROL;
                if ((e.Modifiers & Keys.Alt) == Keys.Alt) mods |= MOD_ALT;
                if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0) mods |= MOD_WIN;
                Key = (uint)e.KeyCode;
                Modifiers = mods;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }

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
            try
            {
                string d = Dir;
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                File.WriteAllText(FilePath,
                    "HotkeyMods=" + HotkeyMods.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "HotkeyKey=" + HotkeyKey.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "RunOnStartup=" + RunOnStartup + "\r\n" +
                    "UnknownColor=" + UnknownColor + "\r\n" +
                    "MutedColor=" + MutedColor + "\r\n" +
                    "UnmutedColor=" + UnmutedColor + "\r\n" +
                    "SlashColor=" + SlashColor + "\r\n");
            }
            catch (IOException ex) { AppLogger.Log("Config.Save", ex); }
            catch (UnauthorizedAccessException ex) { AppLogger.Log("Config.Save", ex); }
        }
    }

    internal static class AppLogger
    {
        private static readonly string LogPath;

        static AppLogger()
        {
            LogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VRCMicToggle", "error.log");
        }

        public static void Log(string context, Exception ex)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                    " [" + context + "] " + ex.ToString() + Environment.NewLine);
            }
            catch (Exception) { }
        }
    }

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
    }

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

    internal class DbPanel : Panel
    {
        public DbPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }
}