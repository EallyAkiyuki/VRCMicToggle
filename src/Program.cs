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
            AppLogger.Info("=== VRCMicToggle starting ===");
            AppLogger.Debug("OS: " + Environment.OSVersion + ", CLR: " + Environment.Version + ", PID: " + Process.GetCurrentProcess().Id);
            if (Environment.OSVersion.Version >= new Version(6, 0, 0))
            {
                try
                {
                    SetProcessDPIAware();
                    AppLogger.Debug("SetProcessDPIAware called");
                }
                catch (Exception ex) { AppLogger.Log("SetProcessDPIAware", ex); }
            }
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\VRCMicToggleSingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    AppLogger.Warn("Another instance is already running, exiting");
                    MessageBox.Show("VRCMic已在运行，新实例即将退出~", "VRCMic", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                AppLogger.Debug("Single instance mutex acquired");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppLogger.Debug("Running AppContext main loop");
                Application.Run(new AppContext());
                AppLogger.Info("Application.Run returned");
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
        private const int OscPollIntervalMs = 3000;
        // OscPostToggleDelayMs removed: sync timer after toggle was removed because
        // querying /input/Voice via OSCQuery returns the pulse value (0 after release),
        // not the actual mic state, causing the icon to incorrectly revert.

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
        private int _lastUdpUpdateTick; // Environment.TickCount when UDP MuteSelf was last received

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
            AppLogger.Debug("AppContext constructor begin");
            _config = Config.Load();
            AppLogger.Debug("Config loaded: HotkeyMods=" + _config.HotkeyMods + " HotkeyKey=" + _config.HotkeyKey + " RunOnStartup=" + _config.RunOnStartup);
            _muted = null;

            _sender = new UdpClient();
            _sender.Connect(IPAddress.Loopback, VrcPort);
            AppLogger.Debug("UDP sender connected to 127.0.0.1:" + VrcPort);

            _oscOnMsg = OscEncode(VrcAddress, 1);
            _oscOffMsg = OscEncode(VrcAddress, 0);
            AppLogger.Debug("OSC toggle messages pre-encoded: ON=" + _oscOnMsg.Length + "B OFF=" + _oscOffMsg.Length + "B");

            _window = new HotkeyWindow();
            _window.HotkeyPressed += OnHotkeyPressed;
            IntPtr dummy = _window.Handle;
            AppLogger.Debug("HotkeyWindow created, handle=" + dummy);

            BuildIconCache();
            InvalidateHotkeyDisplay();
            BuildTray();
            ApplyHotkey();
            StartListener();

            if (!Config.Exists())
            {
                AppLogger.Debug("First run detected (no config file), opening hotkey dialog");
                SetHotkeyDialog();
            }

            ShowTip("VRC麦克风切换工具已启动。快捷键：" + _cachedHotkeyDisplay);

            _oscPollTimer = new System.Threading.Timer(_ => PollOscState(), null, 2000, OscPollIntervalMs);
            lock (_pendingTimers) { _pendingTimers.Add(_oscPollTimer); }
            AppLogger.Debug("OSC poll timer started (interval=" + OscPollIntervalMs + "ms, initialDelay=2000ms)");
            AppLogger.Info("AppContext initialized successfully");
        }

        // 每 3 秒主动检测一次 OSC / 麦克风状态：
        // - 只有首次启动的那一次轮询才尝试弹窗（且需检测到 VRChat 进程），
        //   之后的轮询只更新状态，绝不弹窗；
        // - VRChat 连续两次未响应，则把图标更新为灰色（未知状态）；
        // - 使用单次 TCP 连接同时完成 OSCQuery 探测和麦克风状态查询，
        //   并缓存已发现的 OSCQuery 端口，避免每次轮询都重新探测。
        private void PollOscState()
        {
            if (_disposed) return;
            if (Interlocked.CompareExchange(ref _pollBusy, 1, 0) != 0)
            {
                AppLogger.Debug("PollOscState skipped (previous poll still running)");
                return;
            }
            bool firstPoll = _firstPoll;
            _firstPoll = false;
            AppLogger.Debug("PollOscState begin (first=" + firstPoll + ", cachedPort=" + _cachedOscQueryPort + ")");
            try
            {
                bool? mic = QueryMicStateUnified();
                if (mic.HasValue)
                {
                    _oscNoResponseCount = 0;
                    bool m = mic.Value;
                    AppLogger.Debug("PollOscState: mic state = " + (m ? "muted" : "unmuted"));
                    int udpElapsed = Environment.TickCount - _lastUdpUpdateTick;
                    bool udpRecent = _lastUdpUpdateTick != 0 && udpElapsed > 0 && udpElapsed < 5000;
                    if (!udpRecent)
                    {
                        AppLogger.Debug("PollOscState: UDP not recent, updating state on UI thread");
                        try { _window.BeginInvoke((MethodInvoker)delegate { UpdateMute(m); }); } catch (InvalidOperationException) { }
                    }
                    else
                    {
                        AppLogger.Debug("PollOscState: UDP recent (" + udpElapsed + "ms ago), skipping override");
                    }
                    return;
                }

                AppLogger.Debug("PollOscState: mic query returned null (VRChat not responding)");
                bool vrcRunning = false;
                try { Process[] procs = Process.GetProcessesByName("VRChat"); try { vrcRunning = procs.Length > 0; } finally { for (int pi = 0; pi < procs.Length; pi++) procs[pi].Dispose(); } } catch (Exception) { }

                _oscNoResponseCount++;
                int noResp = _oscNoResponseCount;
                AppLogger.Debug("PollOscState: noResponseCount=" + noResp + ", vrcRunning=" + vrcRunning);
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
                            AppLogger.Debug("PollOscState: >=2 no-response, setting Unknown state");
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

        // 统一的麦克风状态查询：
        // 优先使用缓存的 OSCQuery 端口直接查询 /avatar/parameters/MuteSelf，
        // 如果缓存端口失败则清除缓存，枚举 VRChat 所有 TCP 监听端口逐一尝试。
        // 找到有效响应后缓存该端口供后续轮询使用。
        private bool? QueryMicStateUnified()
        {
            int cached = _cachedOscQueryPort;
            if (cached > 0)
            {
                AppLogger.Debug("QueryMicStateUnified: trying cached port " + cached);
                bool? result = TryQueryMicOnPort(cached);
                if (result.HasValue)
                {
                    AppLogger.Debug("QueryMicStateUnified: cached port " + cached + " returned " + result.Value);
                    return result;
                }
                AppLogger.Debug("QueryMicStateUnified: cached port " + cached + " failed, clearing cache");
                _cachedOscQueryPort = 0;
            }

            List<int> ports = GetVrcTcpPorts();
            AppLogger.Debug("QueryMicStateUnified: found " + ports.Count + " VRChat TCP listen ports");
            if (ports.Count == 0) return null;
            foreach (int port in ports)
            {
                if (port == cached) continue;
                AppLogger.Debug("QueryMicStateUnified: trying port " + port);
                bool? result = TryQueryMicOnPort(port);
                if (result.HasValue)
                {
                    _cachedOscQueryPort = port;
                    AppLogger.Debug("QueryMicStateUnified: port " + port + " succeeded, caching it");
                    return result;
                }
            }
            AppLogger.Debug("QueryMicStateUnified: no port returned a valid result");
            return null;
        }

        // 通过单次 TCP 连接查询 VRChat OSCQuery 服务器的 /avatar/parameters/MuteSelf 状态。
        //
        // 实测 VRChat OSCQuery 响应（ACCESS:1=只读，TYPE:"T"=OSC bool）：
        //   {"FULL_PATH":"/avatar/parameters/MuteSelf","ACCESS":1,"TYPE":"T","VALUE":[true]}   ← 已静音
        //   {"FULL_PATH":"/avatar/parameters/MuteSelf","ACCESS":1,"TYPE":"T","VALUE":[false]}  ← 已开麦
        //
        // 注意：不能查询 /input/Voice（ACCESS:2=只写），它是瞬时输入参数，
        // 发送脉冲 1→0 后 VALUE 始终为 [false]，与麦克风状态无关。
        //
        // 同时承担 OSCQuery 探测职责：如果响应中包含 "VALUE" 则认为是有效的 OSCQuery 服务器。
        // 返回：true=已静音，false=已开麦，null=该端口非 OSCQuery 或连接失败。
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
                    AppLogger.Debug("TryQueryMicOnPort(" + port + "): connect timeout");
                    try { client.Close(); } catch (Exception) { }
                    return null;
                }
                client.EndConnect(ar);
                AppLogger.Debug("TryQueryMicOnPort(" + port + "): connected");
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
                if (totalRead == 0)
                {
                    AppLogger.Debug("TryQueryMicOnPort(" + port + "): empty response");
                    return null;
                }
                string resp = Encoding.ASCII.GetString(buf, 0, totalRead);
                AppLogger.Debug("TryQueryMicOnPort(" + port + "): response=" + totalRead + "B");

                int valIdx = resp.IndexOf("VALUE", StringComparison.OrdinalIgnoreCase);
                if (valIdx < 0)
                {
                    AppLogger.Debug("TryQueryMicOnPort(" + port + "): no VALUE in response, not OSCQuery");
                    return null;
                }

                string valPart = resp.Substring(valIdx);
                if (valPart.IndexOf("[true]", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AppLogger.Debug("TryQueryMicOnPort(" + port + "): MuteSelf=true (muted)");
                    return true;
                }
                if (valPart.IndexOf("[false]", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AppLogger.Debug("TryQueryMicOnPort(" + port + "): MuteSelf=false (unmuted)");
                    return false;
                }
                AppLogger.Debug("TryQueryMicOnPort(" + port + "): VALUE found but unrecognized");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Debug("TryQueryMicOnPort(" + port + "): exception: " + ex.Message);
                return null;
            }
            finally
            {
                if (client != null)
                {
                    try { client.Close(); } catch (Exception) { }
                }
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
            try { Process[] procs = Process.GetProcessesByName("VRChat"); try { foreach (Process p in procs) { try { vrcPids.Add(p.Id); } catch (Exception) { } } } finally { for (int pi = 0; pi < procs.Length; pi++) procs[pi].Dispose(); } } catch (Exception) { }
            if (vrcPids.Count == 0)
            {
                AppLogger.Debug("GetVrcTcpPorts: VRChat process not found");
                return ports;
            }
            AppLogger.Debug("GetVrcTcpPorts: VRChat PIDs: " + string.Join(",", new List<int>(vrcPids).ConvertAll(p => p.ToString()).ToArray()));
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
                AppLogger.Debug("GetVrcTcpPorts: " + count + " TCP listen entries");
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                AppLogger.Debug("AppContext.Dispose begin");
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
                AppLogger.Debug("AppContext.Dispose complete");
            }
            base.Dispose(disposing);
        }

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

        private void SetHotkeyDialog()
        {
            AppLogger.Debug("SetHotkeyDialog: opening hotkey capture dialog");
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
                else
                {
                    AppLogger.Debug("SetHotkeyDialog: dialog cancelled");
                }
            }
            ApplyHotkey();
        }

        private void OpenColorSettings()
        {
            AppLogger.Debug("OpenColorSettings: opening color settings window");
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
                else
                {
                    AppLogger.Debug("OpenColorSettings: dialog cancelled");
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
            AppLogger.Debug("ApplyHotkey: unregistering existing hotkey");
            UnregisterHotKey(_window.Handle, HOTKEY_ID);
            uint vk = _config.HotkeyKey;
            if (vk == 0) { AppLogger.Warn("ApplyHotkey: vk=0, skipping registration"); UpdateStatusText(); return; }
            uint mods = _config.HotkeyMods | MOD_NOREPEAT;
            AppLogger.Debug("ApplyHotkey: registering vk=" + vk + " mods=0x" + mods.ToString("X"));
            bool ok = RegisterHotKey(_window.Handle, HOTKEY_ID, mods, vk);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                AppLogger.Warn("ApplyHotkey: RegisterHotKey failed, error=" + err);
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
            AppLogger.Debug("OnHotkeyPressed: sending OSC toggle to 127.0.0.1:" + VrcPort);
            UdpClient sender = _sender;
            byte[] onMsg = _oscOnMsg;
            byte[] offMsg = _oscOffMsg;
            if (sender != null && onMsg != null && offMsg != null)
            {
                try
                {
                    sender.Send(onMsg, onMsg.Length);
                    AppLogger.Debug("OnHotkeyPressed: OSC ON sent (" + onMsg.Length + "B)");
                }
                catch (SocketException ex) { AppLogger.Log("OnHotkeyPressed:OSC_ON", ex); }
                catch (ObjectDisposedException) { }
                System.Threading.Timer offTimer = null;
                offTimer = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        sender.Send(offMsg, offMsg.Length);
                        AppLogger.Debug("OnHotkeyPressed: OSC OFF sent (" + offMsg.Length + "B) after " + OscToggleDelayMs + "ms delay");
                    }
                    catch (SocketException ex) { AppLogger.Log("OnHotkeyPressed:OSC_OFF", ex); }
                    catch (ObjectDisposedException) { }
                    lock (_pendingTimers) { _pendingTimers.Remove(offTimer); }
                    offTimer.Dispose();
                }, null, OscToggleDelayMs, Timeout.Infinite);
                lock (_pendingTimers) { _pendingTimers.Add(offTimer); }
            }
            else
            {
                AppLogger.Warn("OnHotkeyPressed: sender or messages not available, skipping send");
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
                if (_tracking)
                {
                    AppLogger.Debug("StartListener: already tracking on port " + _activeListenPort);
                    return;
                }
                _tracking = true;
            }
            AppLogger.Debug("StartListener: attempting ports " + ListenPort + "-" + (ListenPort + ListenPortRetryCount - 1));
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
            AppLogger.Warn("StartListener: all ports failed. Last error: " + (lastEx != null ? lastEx.Message : "unknown"));
            ShowTip("无法监听 " + ListenPort + "-" + (ListenPort + ListenPortRetryCount - 1) + " 端口：" + (lastEx != null ? lastEx.Message : "未知错误") + "\n（端口可能被其他 OSC 工具占用，切换功能不受影响）");
        }

        private void StopListener()
        {
            AppLogger.Debug("StopListener called");
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
                AppLogger.Debug("ListenerReceive: " + data.Length + "B from " + ep);
            }
            catch (ObjectDisposedException) { return; }
            catch (SocketException ex) { AppLogger.Debug("ListenerReceive: SocketException: " + ex.Message); }
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
                        AppLogger.Warn("ListenerReceive: failed to requeue, listener stopped");
                        lock (_listenerLock) { _tracking = false; _listener = null; _activeListenPort = 0; }
                        try
                        {
                            _window.BeginInvoke((MethodInvoker)delegate
                            {
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
                        AppLogger.Debug("ParsePacket: MuteSelf received, value=" + (m ? "true(muted)" : "false(unmuted)") + " typeTag='" + typeTag + "'");
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

        private void BuildIconCache()
        {
            AppLogger.Debug("BuildIconCache: rebuilding icons (unknown=" + _config.UnknownColor + " muted=" + _config.MutedColor + " unmuted=" + _config.UnmutedColor + " slash=" + _config.SlashColor + ")");
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
            AppLogger.Debug("SetIcon: " + _currentIconState + " -> " + state);
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

        private void SetStartup(bool enable)
        {
            AppLogger.Debug("SetStartup: " + enable);
            try
            {
                const string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(key, true))
                {
                    if (rk == null) { AppLogger.Warn("SetStartup: registry key not found"); return; }
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
            AppLogger.Info("ExitApp: shutting down");
            Dispose(true);
            Application.Exit();
            AppLogger.Info("=== VRCMicToggle exited ===");
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

            private readonly IntPtr _targetHandle;
            private uint _capturedMods;
            private Keys _capturedKey;
            private bool _hasMainKey;

            private Label _comboLabel;

            public HotkeyCaptureForm(IntPtr targetHandle)
            {
                _targetHandle = targetHandle;
                BuildUi();
            }

            // ── UI construction ──────────────────────────────

            private void BuildUi()
            {
                Text = "设置你的快捷键喵";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(400, 170);
                KeyPreview = true;
                ShowInTaskbar = false;
                Font = new Font("Segoe UI", 9f);

                // Real-time combo display
                _comboLabel = new Label
                {
                    Text = "当前组合：(等待输入)",
                    Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                    Location = new Point(10, 14),
                    Size = new Size(380, 36),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Controls.Add(_comboLabel);

                // Hint text
                var hint = new Label
                {
                    Text = "按下组合后松开即可锁定\n按 Enter 确认 / Esc 清除",
                    Location = new Point(10, 54),
                    Size = new Size(380, 42),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.FromArgb(128, 128, 128)
                };
                Controls.Add(hint);

                // Buttons (centered, 20px gap each)
                int btnY = 112;
                var clearBtn = ColorUtil.MakeButton("清除", OnClear, false, false, Color.Black);
                var confirmBtn = ColorUtil.MakeButton("确认", OnConfirm, true, false, Color.Black);
                var cancelBtn = ColorUtil.MakeButton("取消", OnCancel, false, false, Color.Black);

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

            // ── Key tracking (capture & lock) ────────────────

            protected override void OnKeyDown(KeyEventArgs e)
            {
                e.SuppressKeyPress = true;

                // Enter → confirm captured combo
                if (e.KeyCode == Keys.Enter)
                {
                    if (_hasMainKey) OnConfirm(this, EventArgs.Empty);
                    return;
                }

                // Esc → clear capture, or cancel if already clear
                if (e.KeyCode == Keys.Escape)
                {
                    if (_hasMainKey || _capturedMods != 0)
                    {
                        ClearCapture();
                    }
                    else
                    {
                        DialogResult = DialogResult.Cancel;
                        Close();
                    }
                    return;
                }

                // Read current modifier state (Ctrl/Alt/Shift from event, Win from native API)
                uint mods = 0;
                if ((e.Modifiers & Keys.Control) == Keys.Control) mods |= MOD_CONTROL;
                if ((e.Modifiers & Keys.Alt) == Keys.Alt) mods |= MOD_ALT;
                if ((e.Modifiers & Keys.Shift) == Keys.Shift) mods |= MOD_SHIFT;
                bool winHeld = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0
                            || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
                if (winHeld) mods |= MOD_WIN;

                // Update captured state
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
                // Do nothing — captured state is locked until new key press
            }

            // ── Display ──────────────────────────────────────

            private void RefreshDisplay()
            {
                var sb = new StringBuilder();
                if ((_capturedMods & MOD_CONTROL) != 0) sb.Append("Ctrl + ");
                if ((_capturedMods & MOD_ALT) != 0) sb.Append("Alt + ");
                if ((_capturedMods & MOD_SHIFT) != 0) sb.Append("Shift + ");
                if ((_capturedMods & MOD_WIN) != 0) sb.Append("Win + ");

                if (!_hasMainKey)
                {
                    sb.Append("(等待主键)");
                }
                else
                {
                    sb.Append(KeyName((uint)_capturedKey));
                }

                _comboLabel.Text = "当前组合：" + sb.ToString();
            }

            // ── Button handlers ──────────────────────────────

            private void OnConfirm(object sender, EventArgs e)
            {
                // 1) Must have a main key
                if (!_hasMainKey)
                {
                    ShowMsg("请按一个主键（如字母、数字、F1-F24 等）\n\n不能只用修饰键");
                    return;
                }

                // 2) No modifiers → warn user
                if (_capturedMods == 0)
                {
                    var r = MessageBox.Show(this,
                        "单独使用此键容易与其他程序冲突，确定要使用吗？",
                        "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.No) return;
                }

                uint vk = (uint)_capturedKey;
                uint mods = _capturedMods;

                // 3) Conflict pre-detection: try-register → immediately unregister
                if (!RegisterHotKey(_targetHandle, HOTKEY_ID, mods | MOD_NOREPEAT, vk))
                {
                    int err = Marshal.GetLastWin32Error();
                    ShowMsg("该快捷键已被其他程序占用（错误码 " + err + "）\n\n请更换组合");
                    return;
                }
                UnregisterHotKey(_targetHandle, HOTKEY_ID);

                Key = vk;
                Modifiers = mods;
                DialogResult = DialogResult.OK;
                Close();
            }

            private void OnClear(object sender, EventArgs e)
            {
                ClearCapture();
            }

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

            // ── Helpers ──────────────────────────────────────

            private void ShowMsg(string text)
            {
                MessageBox.Show(this, text, "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            private static bool IsModifierKey(Keys keyCode)
            {
                switch (keyCode)
                {
                    case Keys.ControlKey:
                    case Keys.LControlKey:
                    case Keys.RControlKey:
                    case Keys.Menu:
                    case Keys.LMenu:
                    case Keys.RMenu:
                    case Keys.ShiftKey:
                    case Keys.LShiftKey:
                    case Keys.RShiftKey:
                    case Keys.LWin:
                    case Keys.RWin:
                        return true;
                    default:
                        return false;
                }
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
                    AppLogger.Debug("Config.Load: loaded successfully (HotkeyMods=" + c.HotkeyMods + " HotkeyKey=" + c.HotkeyKey + " RunOnStartup=" + c.RunOnStartup + ")");
                }
                else
                {
                    AppLogger.Debug("Config.Load: config file not found, using defaults");
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
                File.WriteAllText(FilePath,
                    "HotkeyMods=" + HotkeyMods.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "HotkeyKey=" + HotkeyKey.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "RunOnStartup=" + RunOnStartup + "\r\n" +
                    "UnknownColor=" + UnknownColor + "\r\n" +
                    "MutedColor=" + MutedColor + "\r\n" +
                    "UnmutedColor=" + UnmutedColor + "\r\n" +
                    "SlashColor=" + SlashColor + "\r\n");
                AppLogger.Debug("Config.Save: saved successfully");
            }
            catch (IOException ex) { AppLogger.Log("Config.Save", ex); }
            catch (UnauthorizedAccessException ex) { AppLogger.Log("Config.Save", ex); }
        }
    }

    internal static class AppLogger
    {
        private static readonly string LogPath;
        private static readonly string DebugLogPath;
        private const int MaxLogSizeBytes = 1024 * 1024;

        static AppLogger()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VRCMicToggle");
            LogPath = Path.Combine(dir, "error.log");
            DebugLogPath = Path.Combine(dir, "debug.log");
        }

        [Conditional("DEBUG")]
        public static void Debug(string msg)
        {
            WriteLog(DebugLogPath, "DEBUG", msg);
        }

        public static void Info(string msg)
        {
            WriteLog(DebugLogPath, "INFO", msg);
        }

        public static void Warn(string msg)
        {
            WriteLog(DebugLogPath, "WARN", msg);
        }

        public static void Log(string context, Exception ex)
        {
            WriteLog(DebugLogPath, "ERROR", "[" + context + "] " + ex.ToString());
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

        private static void WriteLog(string path, string level, string msg)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > MaxLogSizeBytes)
                    {
                        string backup = path + ".old";
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(path, backup);
                    }
                }
                catch (Exception) { }
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                    " [" + level + "] " + msg + Environment.NewLine);
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