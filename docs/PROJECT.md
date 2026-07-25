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
│   ├── Program.cs              # 主程序入口、核心逻辑、OSC 通信、托盘图标
│   ├── SettingsWindow.cs       # 颜色设置窗口 UI
│   └── ColorPickerDialog.cs    # 自定义颜色选择器对话框
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
| `Config` | Program.cs | 配置加载/保存（纯文本 KV 格式） |
| `AppLogger` | Program.cs | 简易错误日志，写入 `%APPDATA%\VRCMicToggle\error.log` |
| `Theme` | Program.cs | 深色/浅色主题颜色定义 |
| `ColorUtil` | Program.cs | 颜色转换、圆角矩形绘制、按钮工厂等工具方法 |
| `DbPanel` | Program.cs | 双缓冲 Panel，消除闪烁 |
| `HotkeyWindow` | Program.cs | 隐藏窗体，接收 `WM_HOTKEY` 消息 |
| `HotkeyCaptureForm` | Program.cs | 快捷键录制对话框 |
| `SettingsWindow` | SettingsWindow.cs | 颜色设置窗口，支持实时预览和主题自适应 |
| `ColorPickerDialog` | ColorPickerDialog.cs | HSL 颜色选择器，支持预置色板、最近使用、Hex/RGB 输入 |

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
       ├─ 可选：启动 UDP 监听（端口 9001-9003）
       ├─ 启动时 OSCQuery 探测 VRChat 连接状态
       │   ├─ 通过 GetExtendedTcpTable 获取 VRChat 进程的 TCP 监听端口
       │   ├─ HTTP GET 探测 OSCQuery 服务
       │   └─ 查询 /input/Voice 获取初始麦克风状态
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

### OSCQuery 探测（启动时）

通过 Windows API `GetExtendedTcpTable`（iphlpapi.dll）枚举 VRChat.exe 进程的所有 TCP LISTENING 端口，对每个端口发送 HTTP GET 请求探测 OSCQuery 服务，响应中包含 `FULL_PATH` 字段即确认为 OSCQuery 端口。

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
.\build.ps1
```

构建脚本自动查找 `csc.exe`（优先 Framework64），编译 3 个源文件并嵌入图标，输出 `VRCMic.exe`。

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
