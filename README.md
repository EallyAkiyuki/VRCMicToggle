# VRCMicToggle 🎙️

> **Windows 全局快捷键工具 —— 一键切换 VRChat 麦克风静音/开麦状态**  
> **Windows Global Hotkey Tool — Toggle VRChat Microphone Mute/Unmute with One Key**  
> **Windows グローバルホットキーツール — VRChat マイクのミュート/オンをワンキーで切り替え**

---

## 中文说明

### 简介

**VRCMicToggle** 是一个轻量级 Windows 系统托盘工具，通过 OSC 协议向 VRChat 发送指令，实现全局快捷键一键切换麦克风开关。

### 功能特点

- **全局快捷键** — 任意窗口下按快捷键即可切换麦克风（默认 `Insert` 键，支持 Ctrl/Alt/Shift/Win 组合）
- **系统托盘图标** — 不同颜色图标显示当前麦克风状态（未知 / 静音 / 开麦）
- **自定义颜色** — 自由设置三种状态下的图标颜色和斜杠颜色
- **状态监听** — 可选开启 OSC 监听，自动同步 VRChat 内的真实麦克风状态
- **开机自启** — 可选开机自动启动
- **单实例运行** — 防止重复启动
- **双击切换** — 双击托盘图标快速切换麦克风
- **深色/浅色主题自适应** — 颜色设置窗口自动跟随系统主题

### 使用前提

1. VRChat 中打开 ** OSC**：`菜单 → 选项 → OSC → 启用`
2. VRChat 语音设置保持 **Toggle Voice（切换开麦）** 开启（默认状态）
3. 确保电脑与 VRChat 在同一台机器上（工具向 `127.0.0.1:9000` 发送 OSC）

### 使用方法

1. 下载 `VRCMic.exe` 直接运行（无需安装）
2. 托盘出现麦克风图标，表示已启动
3. 按默认快捷键 `Insert` 切换麦克风状态
4. 右键托盘图标可：
   - 查看当前状态
   - 设置快捷键
   - 设置颜色
   - 显示麦克风状态（开启后图标会随 VRChat 实际状态变化）
   - 开机自启
   - 退出程序
5. 双击托盘图标快速切换

### 手动构建

需要 `.NET Framework 4.0+` 环境（Windows 自带）。

```powershell
# 一键构建
.\build.ps1

# 或手动编译
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /unsafe /langversion:4 /platform:anycpu /win32icon:resources\VRCMic.ico /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.dll" src\Program.cs src\SettingsWindow.cs src\ColorPickerDialog.cs
```

### 配置文件

配置文件位于 `%APPDATA%\VRCMicToggle\config.txt`，可手动编辑：

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

**VRCMicToggle** is a lightweight Windows system tray tool that sends OSC commands to VRChat, allowing you to toggle your microphone mute/unmute with a global hotkey.

### Features

- **Global Hotkey** — Toggle mic from any window (default: `Insert`, supports Ctrl/Alt/Shift/Win combos)
- **System Tray Icon** — Colored icons show current mic state (Unknown / Muted / Unmuted)
- **Custom Colors** — Customize icon colors for each state and the slash color
- **Status Tracking** — Optionally listen for OSC feedback to sync with VRChat's actual mic state
- **Auto-start** — Optionally launch on Windows boot
- **Single Instance** — Prevents duplicate launches
- **Double-click Toggle** — Double-click tray icon to quickly toggle mic
- **Dark/Light Theme** — Color settings window follows system theme automatically

### Prerequisites

1. Enable **OSC** in VRChat: `Menu → Options → OSC → Enabled`
2. Keep **Toggle Voice** enabled in VRChat voice settings (default)
3. Run on the same machine as VRChat (sends OSC to `127.0.0.1:9000`)

### Usage

1. Download `VRCMic.exe` and run it directly (no installation required)
2. A microphone icon appears in the system tray
3. Press the default hotkey `Insert` to toggle microphone
4. Right-click the tray icon to:
   - View current status
   - Set hotkey
   - Set colors
   - Show microphone status (when enabled, icons sync with VRChat)
   - Enable auto-start
   - Exit
5. Double-click the tray icon to quickly toggle

### Build from Source

Requires `.NET Framework 4.0+` (comes with Windows).

```powershell
# One-click build
.\build.ps1

# Or compile manually
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /unsafe /langversion:4 /platform:anycpu /win32icon:resources\VRCMic.ico /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.dll" src\Program.cs src\SettingsWindow.cs src\ColorPickerDialog.cs
```

### Configuration

Config file is at `%APPDATA%\VRCMicToggle\config.txt`. You can edit it manually:

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

**VRCMicToggle** は軽量な Windows トレイ常駐ツールです。OSC プロトコルを使用して VRChat にコマンドを送信し、グローバルホットキーでマイクのミュート/オンを切り替えます。

### 機能

- **グローバルホットキー** — どのウィンドウからでもマイクを切り替え可能（デフォルト: `Insert`、Ctrl/Alt/Shift/Win 対応）
- **トレイアイコン** — 色分けされたアイコンでマイク状態を表示（不明 / ミュート / オン）
- **カスタムカラー** — 各状態のアイコン色とスラッシュ色を自由に設定
- **状態追跡** — VRChat からの OSC フィードバックを受信して実際の状態と同期（オプション）
- **自動起動** — Windows 起動時に自動起動（オプション）
- **シングルインスタンス** — 重複起動を防止
- **ダブルクリック切替** — トレイアイコンをダブルクリックで素早く切替
- **ダーク/ライトテーマ対応** — カラー設定ウィンドウがシステムテーマに自動追従

### 前提条件

1. VRChat で **OSC を有効**にする: `メニュー → オプション → OSC → 有効`
2. VRChat の音声設定で **Toggle Voice** がオンになっていること（デフォルト）
3. VRChat と同じ PC で実行すること（`127.0.0.1:9000` に OSC 送信）

### 使い方

1. `VRCMic.exe` をダウンロードして実行（インストール不要）
2. トレイにマイクアイコンが表示されます
3. デフォルトホットキー `Insert` を押してマイクを切替
4. トレイアイコンを右クリックすると：
   - 現在の状態を表示
   - ホットキーを設定
   - 色を設定
   - マイク状態を表示（有効にすると VRChat と同期）
   - 自動起動の設定
   - 終了
5. トレイアイコンをダブルクリックで素早く切替

### ビルド

`.NET Framework 4.0+` が必要です（Windows に標準搭載）。

```powershell
# ワンクリックビルド
.\build.ps1

# または手動コンパイル
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /unsafe /langversion:4 /platform:anycpu /win32icon:resources\VRCMic.ico /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\System.dll" src\Program.cs src\SettingsWindow.cs src\ColorPickerDialog.cs
```

### 設定ファイル

設定ファイルの場所: `%APPDATA%\VRCMicToggle\config.txt`

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

1. Pressing the hotkey sends an OSC message `/input/Voice` with value `1` to `127.0.0.1:9000`
2. After a 60ms delay, it sends the same address with value `0` to simulate a toggle press
3. Optionally, it listens on port `9001` for OSC feedback from VRChat (specifically the `/avatar/parameters/MuteSelf` parameter) to display the actual mic state

The tray icon is dynamically drawn using GDI+ based on the user's color settings, creating a microphone shape with an optional diagonal slash for the muted state.

## AI Assistance / AI 辅助开发 / AI 支援

This project was developed with the assistance of AI tools (large language models) for code generation, debugging, and documentation.

本项目的代码生成、调试和文档编写过程中使用了 AI 工具（大型语言模型）作为辅助。

このプロジェクトは、コード生成、デバッグ、ドキュメント作成に AI ツール（大規模言語モデル）を活用しています。

---

## License / 许可证

MIT License — see [LICENSE](LICENSE) file.