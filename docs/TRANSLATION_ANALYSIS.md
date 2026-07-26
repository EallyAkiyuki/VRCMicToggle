# VRCMicToggle UI 字符串日英翻译分析

> 只翻译运行时用户可见的 UI 字符串和 README，不含源代码注释和内部技术文档。

---

## 翻译策略

### 语气风格

项目基调**轻快、友好、带二次元萌文化色彩**（"喵"、"~"）。VRChat 社区典型风格：

- **日本語**：保留萌文化，「喵」→「にゃ」，「~」→「～」，使用「だよ」等亲近语尾。日中共用汉字文化圈，翻译最自然。
- **English**：萌文化难以直接移植。保持 friendly/playful 而非 cold。"喵"意译为轻松措辞或省略（英语圈 "meow" 会显得过于 weeb）。「~」用 `!` 替代。

### 关键术语

| 中文 | 日本語 | English | 备注 |
|------|--------|---------|------|
| 麦克风 | マイク | Mic | |
| 静音 | ミュート | Muted | 中文"静音"字面是 silence，实际指 mute |
| 开麦 | マイクオン | Unmuted | 中文游戏圈口语，字面 open mic |
| 切换 | 切り替え | Toggle | |
| 快捷键 | ショートカット / ホットキー | Hotkey | |
| 托盘 | トレイ | Tray | 系统托盘 |
| 喵 | にゃ | (playful tone) | 猫叫拟声词，日语直译，英语意译 |
| 色相 | 色相（しきそう） | Hue | 日中共用汉字 |
| 斜杠 | スラッシュ | Slash | 静音图标的斜杠线 |

---

## UI 字符串逐条翻译

### Program.cs — 主程序 & 系统托盘

---

**T01** — 重复实例检测
```
中文：VRCMic已在运行，新实例即将退出~
日本語：VRCMicは既に起動中だよ～ 新しいインスタンスは終了します
English：VRCMic is already running — this instance will exit.
```
> 「既に起動中」+「だよ」保留友好感。英语 "~" 无法直译，用 em dash 替代。

---

**T02** — 启动气泡通知
```
中文：VRC麦克风切换工具已启动。快捷键：{key}
日本語：VRCマイク切り替えツールが起動しました。ショートカット：{key}
English：VRChat Mic Toggle is ready. Hotkey: {key}
```
> "已启动"→「起動しました」敬体让通知可靠。"is ready" 比直译 "has started" 更自然。

---

**T03** — OSC 未连接（标题）
```
中文：OSC 未连接
日本語：OSC 未接続
English：OSC Not Connected
```

---

**T04** — OSC 未连接（正文）
```
中文：VRChat已启动 但OSC未开启或VRC未响应

请检查：
1. VRChat 正常运行
2. 在 VRChat 菜单中打开OSC（圆盘菜单>选项>OSC>开启）

日本語：VRChatは起動していますが、OSCが無効または応答がありません

確認事項：
1. VRChatが正常に動作していること
2. VRChatメニューでOSCを有効にする（円盤メニュー＞オプション＞OSC＞有効）

English：VRChat is running but OSC is not responding.

Please check:
1. VRChat is running normally
2. Enable OSC in VRChat (Action Menu > Options > OSC > Enabled)
```
> 「圆盘菜单」在 VRChat 日语社区称「円盤メニュー」，英文官方名称是 "Action Menu"。确保用户能在 VRChat UI 中找到对应选项至关重要。

---

**T05~T09** — 右键菜单项

| 中文 | 日本語 | English |
|------|--------|---------|
| 快捷键设置 | ショートカット設定 | Hotkey Settings |
| 图标颜色设置 | アイコン色設定 | Icon Color Settings |
| 开机自启 | 起動時に自動起動 | Start with Windows |
| 关于 / 帮助 | バージョン情報 / ヘルプ | About / Help |
| 退出 | 終了 | Exit |

---

**T10~T11** — 关于对话框

```
标题 — 中文：关于  日本語：バージョン情報  English：About

正文：
中文：VRC 麦克风切换工具
Version：{0}

使用全局快捷键 通过OSC切换VRChat麦克风状态

Tips:
1. 在 VRChat 菜单中打开OSC（圆盘菜单>选项>OSC>开启）。
2. VRChat 设置中"麦克风工作模式"为"按下切换"。

当前快捷键：{1}

双击任务栏图标可快速切换麦克风状态~

日本語：VRC マイク切り替えツール
Version：{0}

グローバルホットキーでOSC経由VRChatマイク状態を切替

Tips:
1. VRChatメニューでOSCを有効（円盤メニュー＞オプション＞OSC＞有効）
2. VRChat音声設定の「マイク動作モード」を「押して切替」に

現在のショートカット：{1}

トレイアイコンをダブルクリックでも切替可能だよ～

English：VRChat Mic Toggle
Version: {0}

Toggle your VRChat microphone via OSC with a global hotkey.

Tips:
1. Enable OSC in VRChat (Action Menu > Options > OSC > Enabled).
2. Set VRChat mic mode to "Toggle Voice".

Current hotkey: {1}

Double-click the tray icon to quickly toggle your mic!
```
> 「麦克风工作模式」在英文 VRChat 中叫 "Toggle Voice"，日语版应核实实际菜单文字。用户必须能在 VRChat UI 中找到对应选项。

---

**T12~T13** — 快捷键注册失败

```
标题 — 中文：快捷键注册失败  日本語：ホットキー登録エラー  English：Hotkey Registration Failed

正文：
中文：注册快捷键失败：{key} (错误码 {err})
 快捷键可能被占用

日本語：ホットキーの登録に失敗しました：{key} (エラーコード {err})
他のアプリが使用している可能性があります

English：Could not register hotkey: {key} (error code {err})
It may be in use by another application.
```
> "可能被占用"→「使用している可能性」/ "in use by another application" 比直译 "occupied" 更地道。

---

**T14** — 快捷键设置成功
```
中文：快捷键设置为：{key}
日本語：ショートカットを {key} に設定しました
English：Hotkey set to: {key}
```

---

**T15~T17** — 麦克风状态文本（核心，在托盘菜单和 tooltip 中出现）

| 中文 | 日本語 | English | 分析 |
|------|--------|---------|------|
| 关闭 | ミュート中 | Muted | "关闭"字面是 off，但指静音。不能用「オフ」 |
| 开启 | マイクオン | Unmuted | "开启"字面是 on，但指开麦。不能用「オン」 |
| 未知 | 不明 | Unknown | 日语「不明」比「未知」更常用于状态表示 |

> **关键**：中文用"关闭/开启"有歧义（可能指关程序而非关麦克风）。日语和英语应直接用「ミュート中/マイクオン」和 "Muted/Unmuted"，明确指向麦克风状态。

---

**T18~T19** — 组合状态文本
```
中文：VRC麦克风：{state}    →  日本語：VRCマイク：{state}    English：VRChat Mic: {state}
中文：VRCMic：{state}        →  日本語：VRCMic：{state}        English：VRCMic: {state}
```
> 截断版 tooltip "Mic:{state}" 日语用「マイク:{state}」。

---

**T20** — UDP 监听失败
```
中文：无法监听 {portRange} 端口：{msg}
（端口可能被其他 OSC 工具占用，切换功能不受影响）

日本語：ポート {portRange} をリッスンできません：{msg}
（他のOSCツールが使用中かもしれません。マイク切替機能には影響ありません）

English：Unable to listen on ports {portRange}: {msg}
(They may be in use by another OSC tool. Mic toggle will still work.)
```

---

### SettingsWindow.cs — 颜色设置窗口

---

**T21~T22** — 标题
```
中文：颜色设置              日本語：カラー設定              English：Color Settings
中文：自定义麦克风颜色        日本語：マイク色のカスタマイズ   English：Customize Mic Colors
```

---

**T23** — 提示（含"喵"）
```
中文：点击色块打开颜色选择器喵
日本語：色ブロックをクリックでカラーピッカーを開くにゃ
English：Click a color swatch to open the color picker
```
> 日语「にゃ」完美对应中文"喵"。英语无法传达，建议保持干净友好措辞。

---

**T24~T27** — 颜色状态标签

| 中文 | 日本語 | English |
|------|--------|---------|
| 未知状态 | 状態不明 | Unknown |
| 已静音 | ミュート中 | Muted |
| 已开麦 | マイクオン | Unmuted |
| 斜杠颜色 | スラッシュ色 | Slash Color |

---

**T28~T31** — 图标预览

| 中文 | 日本語 | English |
|------|--------|---------|
| 图标预览 | アイコンプレビュー | Icon Preview |
| 未知 | 不明 | Unknown |
| 静音 | ミュート | Muted |
| 开麦 | マイクオン | Unmuted |

---

**T32~T34** — 按钮

| 中文 | 日本語 | English |
|------|--------|---------|
| 恢复默认 | デフォルトに戻す | Restore Defaults |
| 保存 | 保存 | Save |
| 取消 | キャンセル | Cancel |

---

### ColorPickerDialog.cs — HSV 颜色选择器

---

**T35** — 窗口标题
```
中文：选择颜色  日本語：色の選択  English：Choose Color
```

---

**T36~T40** — 标签

| 中文 | 日本語 | English |
|------|--------|---------|
| 色相 | 色相 | Hue |
| 预览 | プレビュー | Preview |
| 预设 | プリセット | Presets |
| 最近使用 | 最近使用した色 | Recently Used |
| 没有最近使用 | 最近使用した色はありません | No recent colors |

> 「色相」日中共用汉字。"最近使用"日语补全为「最近使用した色」语义才完整。

---

**T41~T42** — 预览标签
```
中文：当前  日本語：現在   English：Current
中文：初始  日本語：元の色  English：Original
```
> "初始"→「元の色」比直译「初期」在此语境更自然。"Original" 比 "Initial" 更有对比感。

---

**T43~T44** — 按钮

| 中文 | 日本語 | English |
|------|--------|---------|
| 确定 | OK | OK |
| 取消 | キャンセル | Cancel |

> 中文"确定"在此等价于 OK，不是"確認"。日语直接用「OK」。

---

### HotkeyCaptureForm.cs — 快捷键捕获对话框

---

**T45** — 窗口标题（含"喵"）
```
中文：设置你的快捷键喵
日本語：ショートカットを設定してにゃ
English：Set Your Hotkey
```
> 「〜してにゃ」是日语猫娘口吻的标准请求句式，完美对应。

---

**T46~T48** — 组合显示

| 中文 | 日本語 | English |
|------|--------|---------|
| 当前组合：(等待输入) | 現在の組合せ：（入力待ち） | Current combo: (waiting for input...) |
| 当前组合：{keys} | 現在の組合せ：{keys} | Current combo: {keys} |
| (等待主键) | （メインキー待ち） | (waiting for main key) |

---

**T49** — 操作提示
```
中文：按下组合后松开即可锁定
按 Enter 确认 / Esc 清除

日本語：キーを押して離すと確定します
Enterで確認 / Escでクリア

English：Press your key combination, then release to lock it in.
Enter to confirm / Esc to clear
```

---

**T50~T52** — 按钮

| 中文 | 日本語 | English |
|------|--------|---------|
| 清除 | クリア | Clear |
| 确认 | 確認 | Confirm |
| 取消 | キャンセル | Cancel |

---

**T53** — 校验：无主键
```
中文：请按一个主键（如字母、数字、F1-F24 等）

不能只用修饰键

日本語：メインキー（英字、数字、F1〜F24など）を押してください

修飾キーだけでは設定できません

English：Please press a main key (letter, number, F1-F24, etc.)

Modifier keys alone are not enough.
```

---

**T54~T55** — 校验：单独按键无修饰键
```
标题 — 中文：警告  日本語：警告  English：Warning

正文：
中文：单独使用此键容易与其他程序冲突，确定要使用吗？

日本語：修飾キーなしで単独使用すると、他のアプリと競合する可能性があります。
このまま設定しますか？

English：Using this key without modifiers may conflict with other apps. Are you sure?
```
> "容易冲突"不直译为 "easy to conflict"（中式英文），"may conflict" 才是地道表达。

---

**T56** — 校验：快捷键已被占用
```
中文：该快捷键已被其他程序占用（错误码 {err}）

请更换组合

日本語：このショートカットは既に他のアプリで使用されています（エラーコード {err}）

別の組合せをお試しください

English：This hotkey is already in use by another application (error code: {err}).

Please try a different combination.
```

---

**T57** — 对话框标题
```
中文：提示  日本語：お知らせ  English：Info
```
> 中文"提示"作为 MessageBox 标题。日语「お知らせ」最友好。英语 "Info" 即可。

---

## 核心翻译难点

### "喵"→"にゃ"→(无) —— 萌文化跨语言迁移

| 语言 | 策略 | 原因 |
|------|------|------|
| 日本語 | 直译为「にゃ」 | 日中共用萌文化，"喵"与"にゃ"在 ACG 文化中功能**完全等价** |
| English | 省略，用友好措辞替代 | "meow" 在英语中主要是字面猫叫，句尾用会让非 ACG 圈用户困惑 |

### "开麦" vs "静音" —— 游戏口语

中文游戏圈："开麦"=unmute、"静音"=mute。不能直译为 "open/close mic"。

日语推荐「マイクオン/ミュート」、英语 "Unmuted/Muted"。

### "关闭/开启" 作为麦克风状态 —— 中文歧义

`string state = (_muted ? "关闭" : "开启")`——脱离上下文时"关闭"可能指关闭程序。

日语和英语应直接用「ミュート中/マイクオン」和 "Muted/Unmuted"，避免歧义。

### "圆盘菜单" —— VRChat 术语

- 日本語：VRChat 社区用「円盤メニュー」或「アクションメニュー」
- English：官方名称 "Action Menu"

在指导文字中使用目标平台 VRChat 的实际菜单名称。

---

## README.md 日语部分改进建议

README.md 已有三语版本，日语部分整体质量不错，以下为微调：

| 现有日语 | 建议改为 | 理由 |
|----------|----------|------|
| `プリセットパレット、履歴色記憶` | `プリセットパレット、最近使用した色の記憶` | "履歴色記憶"略显生硬 |
| `ダブルクリック切替` | `ダブルクリックで切替` | 省略"で"复合词不自然 |
| `Debug ビルド` | `デバッグビルド` | 日语中通常全用片假名 |

---

## 附录：完整 UI 字符串日英对照表

| ID | 文件 | 中文 | 日本語 | English |
|----|------|------|--------|---------|
| T01 | Program.cs | VRCMic已在运行，新实例即将退出~ | VRCMicは既に起動中だよ～ 新しいインスタンスは終了します | VRCMic is already running — this instance will exit. |
| T02 | Program.cs | VRC麦克风切换工具已启动。快捷键： | VRCマイク切り替えツールが起動しました。ショートカット： | VRChat Mic Toggle is ready. Hotkey: |
| T03 | Program.cs | OSC 未连接 | OSC 未接続 | OSC Not Connected |
| T04 | Program.cs | VRChat已启动 但OSC未开启或VRC未响应\n\n请检查：\n1. VRChat 正常运行\n2. 在 VRChat 菜单中打开OSC（圆盘菜单>选项>OSC>开启） | VRChatは起動していますが、OSCが無効または応答がありません\n\n確認事項：\n1. VRChatが正常に動作していること\n2. VRChatメニューでOSCを有効にする（円盤メニュー＞オプション＞OSC＞有効） | VRChat is running but OSC is not responding.\n\nPlease check:\n1. VRChat is running normally\n2. Enable OSC in VRChat (Action Menu > Options > OSC > Enabled) |
| T05 | Program.cs | 快捷键设置 | ショートカット設定 | Hotkey Settings |
| T06 | Program.cs | 图标颜色设置 | アイコン色設定 | Icon Color Settings |
| T07 | Program.cs | 开机自启 | 起動時に自動起動 | Start with Windows |
| T08 | Program.cs | 关于 / 帮助 | バージョン情報 / ヘルプ | About / Help |
| T09 | Program.cs | 退出 | 終了 | Exit |
| T10 | Program.cs | 关于 | バージョン情報 | About |
| T11 | Program.cs | VRC 麦克风切换工具\nVersion：{0}\n\n使用全局快捷键 通过OSC切换VRChat麦克风状态\n\nTips:\n1. 在 VRChat 菜单中打开OSC...\n2. VRChat 设置中"麦克风工作模式"为"按下切换"...\n\n当前快捷键：{1}\n\n双击任务栏图标可快速切换麦克风状态~ | VRC マイク切り替えツール\nVersion：{0}\n\nグローバルホットキーでOSC経由VRChatマイク状態を切替\n\nTips:\n1. VRChatメニューでOSCを有効...\n2. VRChat音声設定の「マイク動作モード」を「押して切替」に\n\n現在のショートカット：{1}\n\nトレイアイコンをダブルクリックでも切替可能だよ～ | VRChat Mic Toggle\nVersion: {0}\n\nToggle your VRChat microphone via OSC with a global hotkey.\n\nTips:\n1. Enable OSC in VRChat...\n2. Set VRChat mic mode to "Toggle Voice".\n\nCurrent hotkey: {1}\n\nDouble-click the tray icon to quickly toggle your mic! |
| T12 | Program.cs | 注册快捷键失败：{0} (错误码 {1})\n 快捷键可能被占用 | ホットキーの登録に失敗しました：{0} (エラーコード {1})\n他のアプリが使用している可能性があります | Could not register hotkey: {0} (error code {1})\nIt may be in use by another application. |
| T13 | Program.cs | 快捷键注册失败 | ホットキー登録エラー | Hotkey Registration Failed |
| T14 | Program.cs | 快捷键设置为：{0} | ショートカットを {0} に設定しました | Hotkey set to: {0} |
| T15 | Program.cs | 关闭 | ミュート中 | Muted |
| T16 | Program.cs | 开启 | マイクオン | Unmuted |
| T17 | Program.cs | 未知 | 不明 | Unknown |
| T18 | Program.cs | VRC麦克风：{0} | VRCマイク：{0} | VRChat Mic: {0} |
| T19 | Program.cs | VRCMic：{0} \| {1} | VRCMic：{0} \| {1} | VRCMic: {0} \| {1} |
| T20 | Program.cs | 无法监听 {0}-{1} 端口：{2}\n（端口可能被其他 OSC 工具占用，切换功能不受影响） | ポート {0}-{1} をリッスンできません：{2}\n（他のOSCツールが使用中かもしれません。マイク切替機能には影響ありません） | Unable to listen on ports {0}-{1}: {2}\n(They may be in use by another OSC tool. Mic toggle will still work.) |
| T21 | SettingsWindow.cs | 颜色设置 | カラー設定 | Color Settings |
| T22 | SettingsWindow.cs | 自定义麦克风颜色 | マイク色のカスタマイズ | Customize Mic Colors |
| T23 | SettingsWindow.cs | 点击色块打开颜色选择器喵 | 色ブロックをクリックでカラーピッカーを開くにゃ | Click a color swatch to open the color picker |
| T24 | SettingsWindow.cs | 未知状态 | 状態不明 | Unknown |
| T25 | SettingsWindow.cs | 已静音 | ミュート中 | Muted |
| T26 | SettingsWindow.cs | 已开麦 | マイクオン | Unmuted |
| T27 | SettingsWindow.cs | 斜杠颜色 | スラッシュ色 | Slash Color |
| T28 | SettingsWindow.cs | 图标预览 | アイコンプレビュー | Icon Preview |
| T29 | SettingsWindow.cs | 未知 | 不明 | Unknown |
| T30 | SettingsWindow.cs | 静音 | ミュート | Muted |
| T31 | SettingsWindow.cs | 开麦 | マイクオン | Unmuted |
| T32 | SettingsWindow.cs | 恢复默认 | デフォルトに戻す | Restore Defaults |
| T33 | SettingsWindow.cs | 保存 | 保存 | Save |
| T34 | SettingsWindow.cs | 取消 | キャンセル | Cancel |
| T35 | ColorPickerDialog.cs | 选择颜色 | 色の選択 | Choose Color |
| T36 | ColorPickerDialog.cs | 色相 | 色相 | Hue |
| T37 | ColorPickerDialog.cs | 预览 | プレビュー | Preview |
| T38 | ColorPickerDialog.cs | 预设 | プリセット | Presets |
| T39 | ColorPickerDialog.cs | 最近使用 | 最近使用した色 | Recently Used |
| T40 | ColorPickerDialog.cs | 没有最近使用 | 最近使用した色はありません | No recent colors |
| T41 | ColorPickerDialog.cs | 当前 | 現在 | Current |
| T42 | ColorPickerDialog.cs | 初始 | 元の色 | Original |
| T43 | ColorPickerDialog.cs | 确定 | OK | OK |
| T44 | ColorPickerDialog.cs | 取消 | キャンセル | Cancel |
| T45 | HotkeyCaptureForm.cs | 设置你的快捷键喵 | ショートカットを設定してにゃ | Set Your Hotkey |
| T46 | HotkeyCaptureForm.cs | 当前组合：(等待输入) | 現在の組合せ：（入力待ち） | Current combo: (waiting for input...) |
| T47 | HotkeyCaptureForm.cs | 当前组合：{0} | 現在の組合せ：{0} | Current combo: {0} |
| T48 | HotkeyCaptureForm.cs | (等待主键) | （メインキー待ち） | (waiting for main key) |
| T49 | HotkeyCaptureForm.cs | 按下组合后松开即可锁定\n按 Enter 确认 / Esc 清除 | キーを押して離すと確定します\nEnterで確認 / Escでクリア | Press your key combination, then release to lock it in.\nEnter to confirm / Esc to clear |
| T50 | HotkeyCaptureForm.cs | 清除 | クリア | Clear |
| T51 | HotkeyCaptureForm.cs | 确认 | 確認 | Confirm |
| T52 | HotkeyCaptureForm.cs | 取消 | キャンセル | Cancel |
| T53 | HotkeyCaptureForm.cs | 请按一个主键（如字母、数字、F1-F24 等）\n\n不能只用修饰键 | メインキー（英字、数字、F1〜F24など）を押してください\n\n修飾キーだけでは設定できません | Please press a main key (letter, number, F1-F24, etc.)\n\nModifier keys alone are not enough. |
| T54 | HotkeyCaptureForm.cs | 警告 | 警告 | Warning |
| T55 | HotkeyCaptureForm.cs | 单独使用此键容易与其他程序冲突，确定要使用吗？ | 修飾キーなしで単独使用すると、他のアプリと競合する可能性があります。このまま設定しますか？ | Using this key without modifiers may conflict with other apps. Are you sure? |
| T56 | HotkeyCaptureForm.cs | 该快捷键已被其他程序占用（错误码 {0}）\n\n请更换组合 | このショートカットは既に他のアプリで使用されています（エラーコード {0}）\n\n別の組合せをお試しください | This hotkey is already in use by another application (error code: {0}).\n\nPlease try a different combination. |
| T57 | HotkeyCaptureForm.cs | 提示 | お知らせ | Info |
