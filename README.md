# VRCMicToggle 🎙️

> **Windows 全局快捷键工具 —— 一键切换 VRChat 麦克风静音/开麦状态**  
> **Windows Global Hotkey Tool — Toggle VRChat Microphone Mute/Unmute with One Key**  
> **Windows グローバルホットキーツール — VRChat マイクのミュート/オンをワンキーで切り替え**

---

## 中文说明

### 简介

**VRCMicToggle** 是一个轻量级 Windows 系统托盘工具，通过 OSC 协议向 VRChat 发送指令，实现全局快捷键一键切换麦克风开关。纯 C# 编写，零外部依赖，仅需 .NET Framework 4.0+（Windows 自带）即可运行和构建。

### 功能特点

- **全局快捷键** — 任意窗口下按快捷键即可切换麦克风（默认 `Insert` 键，支持 Ctrl/Alt/Shift/Win 组合，含冲突检测）
- **系统托盘图标** — GDI+ 动态绘制麦克风形状图标，颜色实时反映当前状态（灰色=未知 / 粉色=静音 / 蓝色=开麦）
- **状态同步** — 自动通过 UDP 监听（端口 9001~9003）和 OSCQuery HTTP 轮询获取 VRChat 真实麦克风状态，端口自动发现
- **自定义颜色** — 完整 HSV 颜色选择器，支持预设色板、最近颜色记忆、Hex/RGB 输入，自由设置三种状态图标颜色和斜杠颜色
- **开机自启** — 可选开机自动启动（注册表 Run 键）
- **单实例运行** — 全局命名 Mutex 防止重复启动
- **双击切换** — 双击托盘图标快速切换麦克风
- **深色/浅色主题自适应** — 设置窗口自动跟随系统主题
- **日志记录** — Release 构建记录错误日志，Debug 构建记录完整调试日志（1MB 自动轮转）

### 前提条件

1. VRChat 中打开 **OSC**：`菜单 → 选项 → OSC → 启用`
2. VRChat 语音设置保持 **Toggle Voice（切换开麦）** 开启（默认状态）

### 使用方法

1. 下载 `VRCMic.exe` 直接运行（无需安装）
2. 托盘出现麦克风图标，表示已启动
3. 按默认快捷键 `Insert` 切换麦克风状态
4. 右键托盘图标可：
   - 查看当前状态（点击可切换）
   - 设置快捷键
   - 设置图标颜色
   - 开机自启
   - 关于 / 帮助
   - 退出程序
5. 双击托盘图标快速切换

### 手动构建

需要 `.NET Framework 4.0+` SDK（Windows 自带 csc.exe 编译器）。

```powershell
# Release 构建（默认）
.\build.ps1

# Debug 构建（含完整调试日志和 PDB）
.\build.ps1 -Configuration Debug

# 或手动编译
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /unsafe /langversion:4 /platform:anycpu /win32icon:resources\VRCMic.ico /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.dll" src\Program.cs src\AppVersion.cs src\AppLogger.cs src\Config.cs src\Controls.cs src\Theme.cs src\HotkeyCaptureForm.cs src\SettingsWindow.cs src\ColorPickerDialog.cs
```

构建产物位于 `bin\Release\VRCMic.exe` 或 `bin\Debug\VRCMic.exe`。
Debug 构建会在 `%APPDATA%\VRCMicToggle\debug.log` 中输出完整的调试日志。

### 配置文件

配置文件位于 `%APPDATA%\VRCMicToggle\config.txt`，首次运行时自动生成，可手动编辑：

```
HotkeyMods=0
HotkeyKey=45
RunOnStartup=False
UnknownColor=#888888
MutedColor=#F48FB1
UnmutedColor=#4FC3F7
SlashColor=#ECECEC
```

---

## English

### Introduction

**VRCMicToggle** is a lightweight Windows system tray tool that sends OSC commands to VRChat, allowing you to toggle your microphone mute/unmute with a global hotkey. Written in pure C# with zero external dependencies — runs and builds with just the .NET Framework 4.0+ that ships with Windows.

### Features

- **Global Hotkey** — Toggle mic from any window (default: `Insert`, supports Ctrl/Alt/Shift/Win combos with conflict detection)
- **System Tray Icon** — GDI+ dynamically-drawn microphone icon with colors reflecting current state (Gray=Unknown / Pink=Muted / Blue=Unmuted)
- **Status Tracking** — Auto-syncs with VRChat via UDP listener (ports 9001~9003) and OSCQuery HTTP polling, with automatic port discovery
- **Custom Colors** — Full HSV color picker with preset palette, recent color history, and Hex/RGB input; freely customize icon colors for each state and the slash color
- **Auto-start** — Optionally launch on Windows boot (registry Run key)
- **Single Instance** — Global named mutex prevents duplicate launches
- **Double-click Toggle** — Double-click tray icon to quickly toggle mic
- **Dark/Light Theme** — Settings windows auto-detect system theme
- **Logging** — Error log for all builds; full debug log with 1MB auto-rotation for Debug builds

### Prerequisites

1. Enable **OSC** in VRChat: `Menu → Options → OSC → Enabled`
2. Keep **Toggle Voice** enabled in VRChat voice settings (default)

### Usage

1. Download `VRCMic.exe` and run it directly (no installation required)
2. A microphone icon appears in the system tray
3. Press the default hotkey `Insert` to toggle microphone
4. Right-click the tray icon to:
   - View current status (click to toggle)
   - Set hotkey
   - Set icon colors
   - Enable auto-start
   - About / Help
   - Exit
5. Double-click the tray icon to quickly toggle

### Build from Source

Requires `.NET Framework 4.0+` SDK (the `csc.exe` compiler ships with Windows).

```powershell
# Release build (default)
.\build.ps1

# Debug build (with full debug log and PDB)
.\build.ps1 -Configuration Debug

# Or compile manually
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /unsafe /langversion:4 /platform:anycpu /win32icon:resources\VRCMic.ico /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.dll" src\Program.cs src\AppVersion.cs src\AppLogger.cs src\Config.cs src\Controls.cs src\Theme.cs src\HotkeyCaptureForm.cs src\SettingsWindow.cs src\ColorPickerDialog.cs
```

Build output is at `bin\Release\VRCMic.exe` or `bin\Debug\VRCMic.exe`.
Debug builds write full debug logs to `%APPDATA%\VRCMicToggle\debug.log`.

### Configuration

Config file is at `%APPDATA%\VRCMicToggle\config.txt`. Auto-generated on first run; can be edited manually:

```
HotkeyMods=0
HotkeyKey=45
RunOnStartup=False
UnknownColor=#888888
MutedColor=#F48FB1
UnmutedColor=#4FC3F7
SlashColor=#ECECEC
```

---

## 日本語

### 概要

**VRCMicToggle** は軽量な Windows トレイ常駐ツールです。OSC プロトコルを使用して VRChat にコマンドを送信し、グローバルホットキーでマイクのミュート/オンを切り替えます。純粋な C# で記述され、外部依存はゼロ — Windows 標準搭載の .NET Framework 4.0+ のみで動作・ビルド可能です。

### 機能

- **グローバルホットキー** — どのウィンドウからでもマイクを切り替え可能（デフォルト: `Insert`、Ctrl/Alt/Shift/Win 対応、競合検出付き）
- **トレイアイコン** — GDI+ で動的に描画されるマイクアイコン。色分けで状態を表示（グレー=不明 / ピンク=ミュート / ブルー=オン）
- **状態追跡** — UDP リスナー（ポート 9001~9003）と OSCQuery HTTP ポーリングで自動同期、ポート自動検出
- **カスタムカラー** — フル HSV カラーピッカー。プリセットパレット、履歴色記憶、Hex/RGB 入力で各状態のアイコン色とスラッシュ色を自由に設定
- **自動起動** — Windows 起動時に自動起動（オプション、レジストリ Run キー）
- **シングルインスタンス** — グローバル Mutex で重複起動を防止
- **ダブルクリック切替** — トレイアイコンをダブルクリックで素早く切替
- **ダーク/ライトテーマ対応** — 設定ウィンドウがシステムテーマに自動追従
- **ログ記録** — 全ビルドでエラーログ、Debug ビルドで完全なデバッグログ（1MB 自動ローテーション）

### 前提条件

1. VRChat で **OSC を有効**にする: `メニュー → オプション → OSC → 有効`
2. VRChat の音声設定で **Toggle Voice** がオンになっていること（デフォルト）

### 使い方

1. `VRCMic.exe` をダウンロードして実行（インストール不要）
2. トレイにマイクアイコンが表示されます
3. デフォルトホットキー `Insert` を押してマイクを切替
4. トレイアイコンを右クリックすると：
   - 現在の状態を表示（クリックで切替）
   - ホットキーを設定
   - アイコン色を設定
   - 自動起動の設定
   - バージョン情報 / ヘルプ
   - 終了
5. トレイアイコンをダブルクリックで素早く切替

### ビルド

`.NET Framework 4.0+` SDK が必要です（Windows に標準搭載の csc.exe を使用）。

```powershell
# Release ビルド（デフォルト）
.\build.ps1

# Debug ビルド（完全なデバッグログと PDB 付き）
.\build.ps1 -Configuration Debug

# または手動コンパイル
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /unsafe /langversion:4 /platform:anycpu /win32icon:resources\VRCMic.ico /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.dll" src\Program.cs src\AppVersion.cs src\AppLogger.cs src\Config.cs src\Controls.cs src\Theme.cs src\HotkeyCaptureForm.cs src\SettingsWindow.cs src\ColorPickerDialog.cs
```

ビルド出力は `bin\Release\VRCMic.exe` または `bin\Debug\VRCMic.exe` にあります。
Debug ビルドは `%APPDATA%\VRCMicToggle\debug.log` に完全なデバッグログを出力します。

### 設定ファイル

設定ファイルの場所: `%APPDATA%\VRCMicToggle\config.txt`。初回起動時に自動生成され、手動編集も可能です：

```
HotkeyMods=0
HotkeyKey=45
RunOnStartup=False
UnknownColor=#888888
MutedColor=#F48FB1
UnmutedColor=#4FC3F7
SlashColor=#ECECEC
```

---

## How It Works / 实现原理 / 仕組み

The tool uses the **OSC (Open Sound Control)** protocol to communicate with VRChat:

**Sending (toggle mic):**
1. Pressing the hotkey sends an OSC message `/input/Voice` with value `1` to `127.0.0.1:9000` (VRChat's OSC input port)
2. After a 60ms delay, it sends the same address with value `0`, simulating a button press for VRChat's "Toggle Voice" mode

**Receiving (status tracking):**
1. **UDP Listener** — Listens on ports 9001~9003 for OSC broadcasts from VRChat containing `/avatar/parameters/MuteSelf`
2. **OSCQuery Polling** — Every 3 seconds, discovers VRChat's OSCQuery TCP port via `GetExtendedTcpTable`, then sends an HTTP GET to query `/avatar/parameters/MuteSelf` state

**Tray Icon:**
The microphone-shaped icon is dynamically drawn using GDI+, with colors reflecting the current mute state. Colors are fully customizable via the built-in HSV color picker.

## Project Structure / 项目结构 / プロジェクト構成

```
VRCMic/
├── build.ps1                   One-click build script (Release/Debug)
├── src/                        Source code (9 C# files)
│   ├── Program.cs              Entry point + AppContext: tray, hotkey, OSC, polling, icon
│   ├── AppVersion.cs           Version constant
│   ├── AppLogger.cs            Logging (error.log / debug.log, auto-rotation)
│   ├── Config.cs               Configuration load/save (atomic write)
│   ├── Controls.cs             Shared UI: DbPanel, ColorUtil, HotkeyWindow
│   ├── Theme.cs                Dark/light theme detection and colors
│   ├── HotkeyCaptureForm.cs    Hotkey recording dialog with conflict detection
│   ├── SettingsWindow.cs       Color settings window with live icon preview
│   └── ColorPickerDialog.cs    Full HSV color picker with presets and history
├── resources/                  Application assets
│   ├── VRCMic.ico              Application icon
│   └── VRCMic.png              Icon source PNG
├── docs/                       Documentation
│   ├── PROJECT.md              Technical architecture
│   └── VRC_OSCQuery.md         VRChat OSCQuery API reference
└── scripts/
    └── create_ico.ps1          PNG to multi-size ICO converter
```

## AI Assistance / AI 辅助开发 / AI 支援

This project was developed with the assistance of AI tools (large language models) for code generation, debugging, and documentation.

本项目的代码生成、调试和文档编写过程中使用了 AI 工具（大型语言模型）作为辅助。

このプロジェクトは、コード生成、デバッグ、ドキュメント作成に AI ツール（大規模言語モデル）を活用しています。

---

## License / 许可证

MIT License — see [LICENSE](LICENSE) file.
