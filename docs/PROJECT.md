# VRCMicToggle 项目文档

## 项目概述

VRCMicToggle 是一个轻量级 Windows 系统托盘工具，通过 OSC（Open Sound Control）协议与 VRChat 通信，实现全局快捷键一键切换麦克风静音/开麦状态。

- **语言**：C#（.NET Framework 4.0+，无第三方依赖）
- **许可证**：MIT
- **运行环境**：Windows（需与 VRChat 在同一台机器上运行）

---

## 目录结构

```
VRCMic/
├── src/
│   ├── Program.cs              # 主程序入口 + AppContext 核心逻辑
│   ├── AppLogger.cs            # 简易日志（error.log + debug.log），自动轮转
│   ├── Config.cs               # 配置加载/保存（纯文本 KV 格式，原子写入）
│   ├── Theme.cs                # 深色/浅色主题颜色定义 + 系统主题检测
│   ├── Controls.cs             # ColorUtil（颜色/图形工具）、DbPanel（双缓冲）、HotkeyWindow（隐藏消息窗口）
│   ├── HotkeyCaptureForm.cs    # 快捷键录制对话框
│   ├── SettingsWindow.cs       # 颜色设置窗口 UI
│   ├── ColorPickerDialog.cs    # 自定义 HSV 颜色选择器对话框
│   └── AppVersion.cs           # 集中管理版本号
├── resources/
│   ├── VRCMic.ico              # 应用程序图标
│   └── VRCMic.png              # 图标源文件
├── docs/
│   ├── VRC_OSCQuery.md         # VRChat OSCQuery API 参考文档
│   └── PROJECT.md              # 本文档
├── scripts/
│   └── create_ico.ps1          # 图标生成脚本
├── build.ps1                   # 一键构建脚本
├── LICENSE                     # MIT 许可证
└── README.md                   # 项目说明（中/英/日三语）
```

---

## 核心架构

### 类结构

| 类名 | 文件 | 职责 |
|------|------|------|
| `Program` | Program.cs | 入口点，单实例互斥锁，DPI 感知 |
| `AppContext` | Program.cs | 应用主上下文，管理托盘图标、全局快捷键、OSC 收发 |
| `Config` | Config.cs | 配置加载/保存（纯文本 KV 格式，原子写入） |
| `AppLogger` | AppLogger.cs | 简易日志（error.log + debug.log），自动轮转 |
| `Theme` | Theme.cs | 深色/浅色主题颜色定义，系统主题检测 |
| `ColorUtil` | Controls.cs | 颜色转换、圆角矩形绘制、按钮工厂等工具方法 |
| `DbPanel` | Controls.cs | 双缓冲 Panel，消除闪烁 |
| `HotkeyWindow` | Controls.cs | 隐藏窗体，接收 `WM_HOTKEY` 消息 |
| `HotkeyCaptureForm` | HotkeyCaptureForm.cs | 快捷键录制对话框 |
| `AppVersion` | AppVersion.cs | 集中管理版本号常量 |
| `SettingsWindow` | SettingsWindow.cs | 颜色设置窗口，支持实时预览和主题自适应 |
| `ColorPickerDialog` | ColorPickerDialog.cs | HSV 颜色选择器，支持预置色板、最近使用、Hex/RGB 输入 |

### 运行流程

```
Main()
  ├─ 单实例检查（Mutex）
  └─ Application.Run(new AppContext())
        ├─ 加载配置 Config.Load()
        ├─ 初始化 UDP 发送端（127.0.0.1:9000）
        ├─ 预构建 OSC 消息（/input/Voice = 1 和 0）
        ├─ 创建 HotkeyWindow，注册全局快捷键
        ├─ 构建 GDI+ 麦克风图标缓存（3 种状态）
        ├─ 创建系统托盘 NotifyIcon + 右键菜单
        ├─ 启动 UDP 监听（端口 9001-9003，接收 MuteSelf 状态）
        ├─ 启动 OSCQuery 轮询定时器（每 3 秒）
        │   ├─ 通过 GetExtendedTcpTable 获取 VRChat 进程的 TCP 监听端口
        │   ├─ HTTP GET 查询 /avatar/parameters/MuteSelf 获取麦克风状态
        │   └─ 连续两次无响应则置为未知状态
        └─ 进入消息循环
```

---

## 通信协议

### OSC 发送（切换麦克风）

向 `127.0.0.1:9000` 发送 UDP 数据包：

1. 发送 `/input/Voice` = `1`（模拟按下）
2. 延迟 60ms 后发送 `/input/Voice` = `0`（模拟释放）
3. VRChat 在 Toggle Voice 模式下将此次操作解释为切换麦克风

OSC 消息格式遵循 OSC 1.0 规范：地址字符串 + 类型标签 + 参数值，4 字节对齐。

### OSC 监听（状态同步）

在 `127.0.0.1:9001`（若被占用则尝试 9002、9003）接收 VRChat 回传的 UDP 数据包，解析 `/avatar/parameters/MuteSelf` 参数获取真实麦克风状态。

支持解析 OSC 消息包和 Bundle（`#bundle`），支持 `T`/`F`/`i`/`f` 四种类型标签。

### OSCQuery 轮询（每 3 秒）

通过 Windows API `GetExtendedTcpTable`（iphlpapi.dll）枚举 VRChat.exe 进程的所有 TCP LISTENING 端口，对每个端口发送 HTTP GET 请求查询 `/avatar/parameters/MuteSelf`，解析响应中 `VALUE` 字段的 `[true]`/`[false]` 获取麦克风静音状态。已发现的端口会被缓存以加速后续轮询。

> 注意：不能查询 `/input/Voice`（ACCESS=2，只写），它是瞬时输入参数，查询后返回的 VALUE 始终为 `[false]`，与麦克风实际状态无关。

---

## 图标系统

托盘图标使用 GDI+ 动态绘制，24x24 像素，1.375 倍缩放：

- **麦克风头部**：圆角矩形
- **支架**：贝塞尔曲线弧形 + 直线底座
- **静音斜杠**：对角线（仅静音状态显示）
- **三种状态**：未知（灰色）、静音（粉色 + 斜杠）、开麦（蓝色）
- 颜色均可通过设置窗口自定义

---

## 配置

配置文件路径：`%APPDATA%\VRCMicToggle\config.txt`

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `HotkeyMods` | uint | 0 | 修饰键标志位（1=Alt, 2=Ctrl, 4=Shift, 8=Win） |
| `HotkeyKey` | uint | 45 | 虚拟键码（45 = Insert） |
| `RunOnStartup` | bool | false | 是否开机自启（写入注册表 Run 键） |
| `UnknownColor` | string | #888888 | 未知状态图标颜色 |
| `MutedColor` | string | #F48FB1 | 静音状态图标颜色 |
| `UnmutedColor` | string | #4FC3F7 | 开麦状态图标颜色 |
| `SlashColor` | string | #ECECEC | 静音斜杠颜色 |

---

## 构建

无需 Visual Studio，使用 .NET Framework 自带的 C# 编译器直接编译：

```powershell
.\build.ps1              # Release 构建
.\build.ps1 Debug        # Debug 构建（含调试符号，关闭优化）
```

构建脚本自动查找 `csc.exe`（优先 Framework64），编译 9 个源文件并嵌入图标，输出 `VRCMic.exe`。

编译选项：`/optimize+ /unsafe /langversion:4 /platform:anycpu`

---

## 关键 Win32 API 调用

| API | 来源 | 用途 |
|-----|------|------|
| `RegisterHotKey` / `UnregisterHotKey` | user32.dll | 注册/注销全局快捷键 |
| `SetProcessDPIAware` | user32.dll | 高 DPI 适配 |
| `DestroyIcon` | user32.dll | 释放动态创建的图标句柄 |
| `GetAsyncKeyState` | user32.dll | 检测 Win 键按下状态 |
| `GetExtendedTcpTable` | iphlpapi.dll | 枚举 VRChat 进程的 TCP 监听端口 |

---

## 错误处理

- 所有异常通过 `AppLogger` 记录到 `%APPDATA%\VRCMicToggle\error.log`
- 快捷键注册失败时弹窗提示错误码
- OSC 未连接时检测 VRChat 进程状态并给出针对性提示
- UDP 监听端口被占用时自动尝试备用端口
