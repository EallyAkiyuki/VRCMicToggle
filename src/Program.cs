// Program.cs — 入口 + AppContext（托盘/热键/OSC）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace VRCMicToggle
{
    // 程序入口：DPI 感知 + 单实例互斥
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            AppLogger.Info("=== VRCMicToggle starting ===");
            if (Environment.OSVersion.Version >= new Version(6, 0, 0))
            {
                try { SetProcessDPIAware(); }
                catch (Exception ex) { AppLogger.Log("SetProcessDPIAware", ex); }
            }
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\VRCMicToggleSingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    AppLogger.Warn("Another instance is already running");
                    MessageBox.Show("VRCMic已在运行，新实例即将退出~", "VRCMic", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContext());
            }
        }
    }

    // 核心上下文：系统托盘、全局热键、OSC 通信、状态轮询
    internal sealed class AppContext : ApplicationContext
    {
        // ── P/Invoke ──────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int vKey);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, int ulReserved);

        // ── 常量 ──────────────────────────────────────────

        // 共享常量（HotkeyCaptureForm / HotkeyWindow 也需要）
        internal const int VK_LWIN = 0x5B;
        internal const int VK_RWIN = 0x5C;

        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const uint MOD_WIN = 0x0008;
        internal const uint MOD_NOREPEAT = 0x4000;
        internal const int HOTKEY_ID = 1;

        // AppContext 私有常量
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
        private const int OscPollIntervalMs = 3000;
        private const int TCP_TABLE_OWNER_PID_LISTEN = 3;
        private const int AF_INET = 2;

        // ── 字段 ──────────────────────────────────────────

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
        private int _cachedOscQueryPort;
        private int _lastUdpUpdateTick;

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
        private IconState _currentIconState = (IconState)(-1);

        // ════════════════════════════════════════════════════
        //  构造 / 初始化
        // ════════════════════════════════════════════════════

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
            IntPtr dummy = _window.Handle; // force handle creation

            BuildIconCache();
            InvalidateHotkeyDisplay();
            BuildTray();
            ApplyHotkey();
            StartListener();

            if (!Config.Exists())
            {
                SetHotkeyDialog();
            }

            ShowTip("VRC麦克风切换工具已启动。快捷键：" + _cachedHotkeyDisplay);

            _oscPollTimer = new System.Threading.Timer(_ => PollOscState(), null, 2000, OscPollIntervalMs);
            lock (_pendingTimers) { _pendingTimers.Add(_oscPollTimer); }
            AppLogger.Info("AppContext initialized successfully");
        }

        // ════════════════════════════════════════════════════
        //  OSCQuery 轮询（每 3 秒检测麦克风状态）
        // ════════════════════════════════════════════════════

        private void PollOscState()
        {
            if (_disposed) return;
            if (Interlocked.CompareExchange(ref _pollBusy, 1, 0) != 0) return;
            bool firstPoll = _firstPoll;
            _firstPoll = false;
            try
            {
                HashSet<int> vrcPids = GetVrcPids();
                bool? mic = QueryMicStateUnified(vrcPids);
                if (mic.HasValue)
                {
                    _oscNoResponseCount = 0;
                    bool m = mic.Value;
                    // 如果最近 5 秒内有 UDP 更新，不覆盖（UDP 更及时）
                    int udpElapsed = Environment.TickCount - _lastUdpUpdateTick;
                    bool udpRecent = _lastUdpUpdateTick != 0 && udpElapsed > 0 && udpElapsed < 5000;
                    if (!udpRecent)
                    {
                        try { _window.BeginInvoke((MethodInvoker)delegate { UpdateMute(m); }); } catch (InvalidOperationException) { }
                    }
                    return;
                }

                bool vrcRunning = vrcPids.Count > 0;
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
                                "请检查：\n" +
                                "1. VRChat 正常运行\n" +
                                "2. 在 VRChat 菜单中打开OSC（圆盘菜单>选项>OSC>开启）",
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
            catch (Exception ex)
            {
                AppLogger.Log("PollOscState", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _pollBusy, 0);
            }
        }

        // 统一的麦克风状态查询：优先缓存端口，失败则枚举 VRChat TCP 端口逐一尝试
        private bool? QueryMicStateUnified(HashSet<int> vrcPids)
        {
            int cached = _cachedOscQueryPort;
            if (cached > 0)
            {
                bool? result = TryQueryMicOnPort(cached);
                if (result.HasValue) return result;
                _cachedOscQueryPort = 0;
            }

            List<int> ports = GetVrcTcpPorts(vrcPids);
            if (ports.Count == 0) return null;
            foreach (int port in ports)
            {
                if (port == cached) continue;
                bool? result = TryQueryMicOnPort(port);
                if (result.HasValue)
                {
                    _cachedOscQueryPort = port;
                    return result;
                }
            }
            return null;
        }

        // 通过单次 TCP 连接查询 VRChat OSCQuery 的 /avatar/parameters/MuteSelf
        // 返回：true=已静音，false=已开麦，null=非 OSCQuery 或连接失败
        private static bool? TryQueryMicOnPort(int port)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                IAsyncResult ar = client.BeginConnect(IPAddress.Loopback, port, null, null);
                bool connected = ar.AsyncWaitHandle.WaitOne(400, true);
                if (!connected)
                {
                    try { client.Close(); } catch (Exception) { }
                    return null;
                }
                client.EndConnect(ar);
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 600;
                stream.WriteTimeout = 400;
                byte[] req = Encoding.ASCII.GetBytes("GET /avatar/parameters/MuteSelf HTTP/1.0\r\n\r\n");
                stream.Write(req, 0, req.Length);
                stream.Flush();

                byte[] buf = new byte[2048];
                int totalRead = 0;
                while (totalRead < buf.Length)
                {
                    int n = stream.Read(buf, totalRead, buf.Length - totalRead);
                    if (n <= 0) break;
                    totalRead += n;
                    string partial = Encoding.ASCII.GetString(buf, 0, totalRead);
                    if (partial.IndexOf("\r\n\r\n") >= 0 &&
                        partial.IndexOf("VALUE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        partial.IndexOf(']') >= 0)
                        break;
                }
                if (totalRead == 0) return null;

                string resp = Encoding.ASCII.GetString(buf, 0, totalRead);
                int valIdx = resp.IndexOf("VALUE", StringComparison.OrdinalIgnoreCase);
                if (valIdx < 0) return null;

                string valPart = resp.Substring(valIdx);
                if (valPart.IndexOf("[true]", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (valPart.IndexOf("[false]", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Debug("TryQueryMicOnPort(" + port + "): " + ex.Message);
                return null;
            }
            finally
            {
                if (client != null) { try { client.Close(); } catch (Exception) { } }
            }
        }

        // 枚举 VRChat 进程的所有 TCP LISTENING 端口
        private static List<int> GetVrcTcpPorts(HashSet<int> vrcPids)
        {
            List<int> ports = new List<int>();
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
                    if (vrcPids.Contains(ownerPid))
                    {
                        ports.Add(localPort);
                        AppLogger.Debug("GetVrcTcpPorts: VRChat port " + localPort + " (PID " + ownerPid + ")");
                    }
                }
            }
            catch (Exception ex) { AppLogger.Log("GetVrcTcpPorts", ex); }
            finally { if (table != IntPtr.Zero) Marshal.FreeHGlobal(table); }
            return ports;
        }

        private static HashSet<int> GetVrcPids()
        {
            HashSet<int> pids = new HashSet<int>();
            try
            {
                Process[] procs = Process.GetProcessesByName("VRChat");
                try { foreach (Process p in procs) { try { pids.Add(p.Id); } catch (Exception) { } } }
                finally { for (int i = 0; i < procs.Length; i++) procs[i].Dispose(); }
            }
            catch (Exception) { }
            return pids;
        }

        // ════════════════════════════════════════════════════
        //  资源释放
        // ════════════════════════════════════════════════════

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
                AppLogger.Info("AppContext disposed");
            }
            base.Dispose(disposing);
        }

        // ════════════════════════════════════════════════════
        //  系统托盘 / 右键菜单
        // ════════════════════════════════════════════════════

        private void BuildTray()
        {
            _notify = new NotifyIcon();
            _notify.Text = "VRCMic";
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

            ToolStripMenuItem setHk = new ToolStripMenuItem("快捷键设置");
            setHk.Click += (s, e) => SetHotkeyDialog();

            ToolStripMenuItem setColor = new ToolStripMenuItem("图标颜色设置");
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
                "VRC 麦克风切换工具\n" +
                "Version：" + AppVersion.Version + "\n\n" +
                "使用全局快捷键 通过OSC切换VRChat麦克风状态\n\n" +
                "Tips:\n" +
                "1. 在 VRChat 菜单中打开OSC（圆盘菜单>选项>OSC>开启）。\n" +
                "2. VRChat 设置中\"麦克风工作模式\"为\"按下切换\"。\n\n" +
                "当前快捷键：" + HotkeyDisplay() + "\n\n" +
                "双击任务栏图标可快速切换麦克风状态~",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════════════
        //  快捷键管理
        // ════════════════════════════════════════════════════

        private void SetHotkeyDialog()
        {
            UnregisterHotKey(_window.Handle, HOTKEY_ID);
            using (HotkeyCaptureForm dlg = new HotkeyCaptureForm(_window.Handle))
            {
                DialogResult r = dlg.ShowDialog(_window);
                if (r == DialogResult.OK)
                {
                    _config.HotkeyKey = dlg.Key;
                    _config.HotkeyMods = dlg.Modifiers;
                    _config.Save();
                    InvalidateHotkeyDisplay();
                    ShowTip("快捷键设置为：" + _cachedHotkeyDisplay);
                    AppLogger.Info("Hotkey changed to: " + _cachedHotkeyDisplay);
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
                    AppLogger.Info("Color settings updated");
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
            uint mods = _config.HotkeyMods | MOD_NOREPEAT;
            bool ok = RegisterHotKey(_window.Handle, HOTKEY_ID, mods, vk);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                string msg = "注册快捷键失败：" + _cachedHotkeyDisplay + " (错误码 " + err + ")\n 快捷键可能被占用";
                ShowTip(msg);
                MessageBox.Show(msg, "快捷键注册失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                AppLogger.Info("Hotkey registered: " + _cachedHotkeyDisplay);
            }
            UpdateStatusText();
        }

        private string HotkeyDisplay()
        {
            return _cachedHotkeyDisplay ?? string.Empty;
        }

        internal static string KeyName(uint vk)
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

        // ════════════════════════════════════════════════════
        //  OSC 发送 / 编码
        // ════════════════════════════════════════════════════

        private void OnHotkeyPressed()
        {
            UdpClient sender = _sender;
            byte[] onMsg = _oscOnMsg;
            byte[] offMsg = _oscOffMsg;
            if (sender != null && onMsg != null && offMsg != null)
            {
                try
                {
                    sender.Send(onMsg, onMsg.Length);
                }
                catch (SocketException ex) { AppLogger.Log("OnHotkeyPressed:OSC_ON", ex); }
                catch (ObjectDisposedException) { }

                System.Threading.Timer offTimer = null;
                offTimer = new System.Threading.Timer(_ =>
                {
                    try { sender.Send(offMsg, offMsg.Length); }
                    catch (SocketException ex) { AppLogger.Log("OnHotkeyPressed:OSC_OFF", ex); }
                    catch (ObjectDisposedException) { }
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

        // ════════════════════════════════════════════════════
        //  UDP 监听（接收 VRChat 的 MuteSelf 广播）
        // ════════════════════════════════════════════════════

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
                    AppLogger.Info("UDP listener started on port " + port);
                    UpdateStatusText();
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    AppLogger.Debug("StartListener: port " + port + " failed: " + ex.Message);
                    try { if (client != null) client.Close(); } catch (ObjectDisposedException) { }
                }
            }
            lock (_listenerLock) { _tracking = false; }
            ShowTip("无法监听 " + ListenPort + "-" + (ListenPort + ListenPortRetryCount - 1) + " 端口：" + (lastEx != null ? lastEx.Message : "未知错误") + "\n（端口可能被其他 OSC 工具占用，切换功能不受影响）");
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
            MethodInvoker uiUpdate = delegate
            {
                SetIcon(IconState.Unknown);
                UpdateStatusText();
            };
            if (_window != null && !_window.IsDisposed && _window.IsHandleCreated)
            {
                try { _window.BeginInvoke(uiUpdate); }
                catch (InvalidOperationException) { uiUpdate(); }
            }
            else
            {
                uiUpdate();
            }
            AppLogger.Info("UDP listener stopped");
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
                                _muted = null;
                                SetIcon(IconState.Unknown);
                                UpdateStatusText();
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

        // ════════════════════════════════════════════════════
        //  OSC 数据包解析
        // ════════════════════════════════════════════════════

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
                        AppLogger.Info("Mic state from UDP: " + (m ? "Muted" : "Unmuted"));
                        _lastUdpUpdateTick = Environment.TickCount;
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
            // OSC 数据为大端序，x86/x64 为小端序，需字节翻转后按 float 读取
            // 直接写入临时栈上 4 字节再强转 float，避免 BitConverter.GetBytes 的堆分配
            byte* buf = stackalloc byte[4];
            buf[0] = data[i + 3]; // 小端：低字节在前
            buf[1] = data[i + 2];
            buf[2] = data[i + 1];
            buf[3] = data[i + 0]; // 大端：高字节在前
            i += 4;
            return *(float*)buf;
        }

        // ════════════════════════════════════════════════════
        //  UI 状态更新
        // ════════════════════════════════════════════════════

        private void UpdateMute(bool muted)
        {
            if (_muted.HasValue && _muted.Value == muted) return;
            bool? prev = _muted;
            _muted = muted;
            AppLogger.Info("Mic state changed: " + (prev.HasValue ? (prev.Value ? "Muted" : "Unmuted") : "Unknown") + " -> " + (muted ? "Muted" : "Unmuted"));
            SetIcon(muted ? IconState.Muted : IconState.Unmuted);
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            string state = (_muted.HasValue ? (_muted.Value ? "关闭" : "开启") : "未知");
            string newText = "VRC麦克风：" + state;
            if (newText != _cachedStatusText)
            {
                _cachedStatusText = newText;
                _statusItem.Text = newText;
            }
            string tip = "VRCMic：" + state + " | " + HotkeyDisplay();
            if (tip.Length > MaxTooltipLength)
            {
                tip = "Mic:" + state;
                if (tip.Length > MaxTooltipLength) tip = tip.Substring(0, MaxTooltipLength);
            }
            if (tip != _cachedTipText)
            {
                _cachedTipText = tip;
                _notify.Text = tip;
            }
        }

        // ════════════════════════════════════════════════════
        //  图标渲染 / 缓存
        // ════════════════════════════════════════════════════

        private void BuildIconCache()
        {
            Icon oldUnknown = _cachedUnknown;
            Icon oldMuted = _cachedMuted;
            Icon oldUnmuted = _cachedUnmuted;
            IntPtr hOldUnknown = _hUnknown;
            IntPtr hOldMuted = _hMuted;
            IntPtr hOldUnmuted = _hUnmuted;

            Icon newUnknown = null, newMuted = null, newUnmuted = null;
            IntPtr hNewUnknown = IntPtr.Zero, hNewMuted = IntPtr.Zero, hNewUnmuted = IntPtr.Zero;
            try
            {
                Color unknownCol = ColorUtil.HexToColor(_config.UnknownColor);
                Color mutedCol = ColorUtil.HexToColor(_config.MutedColor);
                Color unmutedCol = ColorUtil.HexToColor(_config.UnmutedColor);
                Color slashCol = ColorUtil.HexToColor(_config.SlashColor);

                Bitmap bmp;
                bmp = CreateMicIcon(unknownCol, slashCol, false);
                hNewUnknown = bmp.GetHicon();
                newUnknown = Icon.FromHandle(hNewUnknown);
                bmp.Dispose();

                bmp = CreateMicIcon(mutedCol, slashCol, true);
                hNewMuted = bmp.GetHicon();
                newMuted = Icon.FromHandle(hNewMuted);
                bmp.Dispose();

                bmp = CreateMicIcon(unmutedCol, slashCol, false);
                hNewUnmuted = bmp.GetHicon();
                newUnmuted = Icon.FromHandle(hNewUnmuted);
                bmp.Dispose();

                _cachedUnknown = newUnknown;
                _cachedMuted = newMuted;
                _cachedUnmuted = newUnmuted;
                _hUnknown = hNewUnknown;
                _hMuted = hNewMuted;
                _hUnmuted = hNewUnmuted;

                IconState st = IconState.Unknown;
                if (_muted.HasValue) st = _muted.Value ? IconState.Muted : IconState.Unmuted;
                _currentIconState = (IconState)(-1);
                SetIcon(st);
            }
            catch
            {
                if (newUnknown != null) { try { newUnknown.Dispose(); } catch (InvalidOperationException) { } }
                if (newMuted != null) { try { newMuted.Dispose(); } catch (InvalidOperationException) { } }
                if (newUnmuted != null) { try { newUnmuted.Dispose(); } catch (InvalidOperationException) { } }
                if (hNewUnknown != IntPtr.Zero) DestroyIcon(hNewUnknown);
                if (hNewMuted != IntPtr.Zero) DestroyIcon(hNewMuted);
                if (hNewUnmuted != IntPtr.Zero) DestroyIcon(hNewUnmuted);
                throw;
            }

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
            try { _notify.ShowBalloonTip(BalloonTipDurationMs, "VRCMic", msg, ToolTipIcon.Info); } catch (InvalidOperationException) { }
        }

        // ════════════════════════════════════════════════════
        //  开机自启 / 退出
        // ════════════════════════════════════════════════════

        private void SetStartup(bool enable)
        {
            try
            {
                const string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key, true))
                {
                    if (rk == null) return;
                    if (enable) rk.SetValue("VRCMicToggle", Application.ExecutablePath);
                    else rk.DeleteValue("VRCMicToggle", false);
                    AppLogger.Info("Startup " + (enable ? "enabled" : "disabled"));
                }
            }
            catch (UnauthorizedAccessException ex) { AppLogger.Log("SetStartup", ex); }
            catch (System.Security.SecurityException ex) { AppLogger.Log("SetStartup", ex); }
        }

        private void ExitApp()
        {
            AppLogger.Info("=== VRCMicToggle exiting ===");
            Dispose(true);
            Application.Exit();
        }
    }
}
