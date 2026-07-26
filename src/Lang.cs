// Lang.cs — 四语言 UI 字符串（简体中文 / 繁体中文 / 日本語 / English）
using System;

namespace VRCMicToggle
{
    // 语言 ID 常量，与 Config.Language 对应
    internal static class LangId
    {
        public const string ZH_CN = "zh-CN";
        public const string ZH_TW = "zh-TW";
        public const string JA    = "ja";
        public const string EN    = "en";
    }

    internal static class Lang
    {
        // ── 当前语言 ─────────────────────────────────────
        public static string Current = LangId.ZH_CN;

        public static void SetLanguage(string langId)
        {
            if (langId == LangId.ZH_CN || langId == LangId.ZH_TW ||
                langId == LangId.JA || langId == LangId.EN)
                Current = langId;
            else
                Current = LangId.ZH_CN;
        }

        // ── 便捷取值：按当前语言返回对应字符串 ───────────
        // 每个方法用 switch 分发，避免字典开销；字段数量固定且少，编译器可内联。

        // T01
        public static string MsgAlreadyRunning
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "VRCMic已在運行，新實例即將退出~";
                    case LangId.JA:    return "VRCMicは既に起動中だよ～ 新しいインスタンスは終了します";
                    case LangId.EN:    return "VRCMic is already running — this instance will exit.";
                    default:           return "VRCMic已在运行，新实例即将退出~";
                }
            }
        }

        // T02 (前缀，调用方拼接快捷键)
        public static string MsgStartupTip
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "VRC麥克風切換工具已啟動。快捷鍵：";
                    case LangId.JA:    return "VRCマイク切り替えツールが起動しました。ショートカット：";
                    case LangId.EN:    return "VRChat Mic Toggle is ready. Hotkey: ";
                    default:           return "VRC麦克风切换工具已启动。快捷键：";
                }
            }
        }

        // T03
        public static string OscNotConnectedTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "OSC 未連接";
                    case LangId.JA:    return "OSC 未接続";
                    case LangId.EN:    return "OSC Not Connected";
                    default:           return "OSC 未连接";
                }
            }
        }

        // T04
        public static string OscNotConnectedBody
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW:
                        return "VRChat已啟動 但OSC未開啟或VRC未響應\n\n" +
                               "請檢查：\n" +
                               "1. VRChat 正常運行\n" +
                               "2. 在 VRChat 選單中開啟OSC（圓盤選單>選項>OSC>開啟）";
                    case LangId.JA:
                        return "VRChatは起動していますが、OSCが無効または応答がありません\n\n" +
                               "確認事項：\n" +
                               "1. VRChatが正常に動作していること\n" +
                               "2. VRChatメニューでOSCを有効にする（円盤メニュー＞オプション＞OSC＞有効）";
                    case LangId.EN:
                        return "VRChat is running but OSC is not responding.\n\n" +
                               "Please check:\n" +
                               "1. VRChat is running normally\n" +
                               "2. Enable OSC in VRChat (Action Menu > Options > OSC > Enabled)";
                    default:
                        return "VRChat已启动 但OSC未开启或VRC未响应\n\n" +
                               "请检查：\n" +
                               "1. VRChat 正常运行\n" +
                               "2. 在 VRChat 菜单中打开OSC（圆盘菜单>选项>OSC>开启）";
                }
            }
        }

        // T05 — 右键菜单：快捷键设置
        public static string MenuHotkeySettings
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "快捷鍵設定";
                    case LangId.JA:    return "ショートカット設定";
                    case LangId.EN:    return "Hotkey Settings";
                    default:           return "快捷键设置";
                }
            }
        }

        // T06 — 右键菜单：图标颜色设置
        public static string MenuColorSettings
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "圖標顏色設定";
                    case LangId.JA:    return "アイコン色設定";
                    case LangId.EN:    return "Icon Color Settings";
                    default:           return "图标颜色设置";
                }
            }
        }

        // T07 — 右键菜单：开机自启
        public static string MenuStartup
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "開機自啟";
                    case LangId.JA:    return "起動時に自動起動";
                    case LangId.EN:    return "Start with Windows";
                    default:           return "开机自启";
                }
            }
        }

        // T08 — 右键菜单：关于 / 帮助
        public static string MenuAbout
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "關於 / 說明";
                    case LangId.JA:    return "バージョン情報 / ヘルプ";
                    case LangId.EN:    return "About / Help";
                    default:           return "关于 / 帮助";
                }
            }
        }

        // T09 — 右键菜单：退出
        public static string MenuExit
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "退出";
                    case LangId.JA:    return "終了";
                    case LangId.EN:    return "Exit";
                    default:           return "退出";
                }
            }
        }

        // 右键菜单：Language
        public static string MenuLanguage
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "Language";
                    case LangId.JA:    return "Language";
                    case LangId.EN:    return "Language";
                    default:           return "Language";
                }
            }
        }

        // 语言选项名称
        public static string LangNameZhCn
        {
            get { return "简体中文"; }
        }

        public static string LangNameZhTw
        {
            get { return "繁體中文"; }
        }

        public static string LangNameJa
        {
            get { return "日本語"; }
        }

        public static string LangNameEn
        {
            get { return "English"; }
        }

        // T10 — 关于对话框标题
        public static string AboutTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "關於";
                    case LangId.JA:    return "バージョン情報";
                    case LangId.EN:    return "About";
                    default:           return "关于";
                }
            }
        }

        // T11 — 关于对话框正文（两个占位符：{0}=版本号, {1}=快捷键）
        public static string AboutBody
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW:
                        return "VRC 麥克風切換工具\n" +
                               "Version：{0}\n\n" +
                               "使用全域快捷鍵 透過OSC切換VRChat麥克風狀態\n\n" +
                               "Tips:\n" +
                               "1. 在 VRChat 選單中開啟OSC（圓盤選單>選項>OSC>開啟）。\n" +
                               "2. VRChat 設定中「麥克風工作模式」為「按下切換」。\n\n" +
                               "當前快捷鍵：{1}\n\n" +
                               "雙擊工作列圖標可快速切換麥克風狀態~";
                    case LangId.JA:
                        return "VRC マイク切り替えツール\n" +
                               "Version：{0}\n\n" +
                               "グローバルホットキーでOSC経由VRChatマイク状態を切替\n\n" +
                               "Tips:\n" +
                               "1. VRChatメニューでOSCを有効（円盤メニュー＞オプション＞OSC＞有効）\n" +
                               "2. VRChat音声設定の「マイク動作モード」を「押して切替」に\n\n" +
                               "現在のショートカット：{1}\n\n" +
                               "トレイアイコンをダブルクリックでも切替可能だよ～";
                    case LangId.EN:
                        return "VRChat Mic Toggle\n" +
                               "Version: {0}\n\n" +
                               "Toggle your VRChat microphone via OSC with a global hotkey.\n\n" +
                               "Tips:\n" +
                               "1. Enable OSC in VRChat (Action Menu > Options > OSC > Enabled).\n" +
                               "2. Set VRChat mic mode to \"Toggle Voice\".\n\n" +
                               "Current hotkey: {1}\n\n" +
                               "Double-click the tray icon to quickly toggle your mic!";
                    default:
                        return "VRC 麦克风切换工具\n" +
                               "Version：{0}\n\n" +
                               "使用全局快捷键 通过OSC切换VRChat麦克风状态\n\n" +
                               "Tips:\n" +
                               "1. 在 VRChat 菜单中打开OSC（圆盘菜单>选项>OSC>开启）。\n" +
                               "2. VRChat 设置中\"麦克风工作模式\"为\"按下切换\"。\n\n" +
                               "当前快捷键：{1}\n\n" +
                               "双击任务栏图标可快速切换麦克风状态~";
                }
            }
        }

        // T12 — 快捷键注册失败正文
        public static string HotkeyRegFailBody
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "註冊快捷鍵失敗：{0} (錯誤碼 {1})\n 快捷鍵可能被佔用";
                    case LangId.JA:    return "ホットキーの登録に失敗しました：{0} (エラーコード {1})\n他のアプリが使用している可能性があります";
                    case LangId.EN:    return "Could not register hotkey: {0} (error code {1})\nIt may be in use by another application.";
                    default:           return "注册快捷键失败：{0} (错误码 {1})\n 快捷键可能被占用";
                }
            }
        }

        // T13 — 快捷键注册失败标题
        public static string HotkeyRegFailTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "快捷鍵註冊失敗";
                    case LangId.JA:    return "ホットキー登録エラー";
                    case LangId.EN:    return "Hotkey Registration Failed";
                    default:           return "快捷键注册失败";
                }
            }
        }

        // T14 — 快捷键设置成功提示
        public static string HotkeySetTip
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "快捷鍵設定為：";
                    case LangId.JA:    return "ショートカット設定：";
                    case LangId.EN:    return "Hotkey set to: ";
                    default:           return "快捷键设置为：";
                }
            }
        }

        // T15 — 麦克风状态：静音
        public static string StateMuted
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "關閉";
                    case LangId.JA:    return "ミュート中";
                    case LangId.EN:    return "Muted";
                    default:           return "关闭";
                }
            }
        }

        // T16 — 麦克风状态：开麦
        public static string StateUnmuted
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "開啟";
                    case LangId.JA:    return "マイクオン";
                    case LangId.EN:    return "Unmuted";
                    default:           return "开启";
                }
            }
        }

        // T17 — 麦克风状态：未知
        public static string StateUnknown
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "未知";
                    case LangId.JA:    return "不明";
                    case LangId.EN:    return "Unknown";
                    default:           return "未知";
                }
            }
        }

        // T18 — 菜单项状态文本前缀 "VRC麦克风："
        public static string StatusPrefix
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "VRC麥克風：";
                    case LangId.JA:    return "VRCマイク：";
                    case LangId.EN:    return "VRChat Mic: ";
                    default:           return "VRC麦克风：";
                }
            }
        }

        // T19a — tooltip 前缀 "VRCMic："
        public static string TooltipPrefix
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "VRCMic：";
                    case LangId.JA:    return "VRCMic：";
                    case LangId.EN:    return "VRCMic: ";
                    default:           return "VRCMic：";
                }
            }
        }

        // T19b — tooltip 截断版前缀 "Mic:"
        public static string TooltipShortPrefix
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "Mic:";
                    case LangId.JA:    return "マイク:";
                    case LangId.EN:    return "Mic:";
                    default:           return "Mic:";
                }
            }
        }

        // T20 — UDP 监听失败
        public static string ListenFailTip
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW:
                        return "無法監聽 {0}-{1} 埠：{2}\n（埠可能被其他 OSC 工具佔用，切換功能不受影響）";
                    case LangId.JA:
                        return "ポート {0}-{1} をリッスンできません：{2}\n（他のOSCツールが使用中かもしれません。マイク切替機能には影響ありません）";
                    case LangId.EN:
                        return "Unable to listen on ports {0}-{1}: {2}\n(They may be in use by another OSC tool. Mic toggle will still work.)";
                    default:
                        return "无法监听 {0}-{1} 端口：{2}\n（端口可能被其他 OSC 工具占用，切换功能不受影响）";
                }
            }
        }

        // ── SettingsWindow (T21~T34) ─────────────────────

        // T21
        public static string SettingsTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "顏色設定";
                    case LangId.JA:    return "カラー設定";
                    case LangId.EN:    return "Color Settings";
                    default:           return "颜色设置";
                }
            }
        }

        // T22
        public static string SettingsSubtitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "自訂麥克風顏色";
                    case LangId.JA:    return "マイク色のカスタマイズ";
                    case LangId.EN:    return "Customize Mic Colors";
                    default:           return "自定义麦克风颜色";
                }
            }
        }

        // T23
        public static string SettingsHint
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "點擊色塊開啟顏色選擇器喵";
                    case LangId.JA:    return "色ブロックをクリックでカラーピッカーを開くにゃ";
                    case LangId.EN:    return "Click a color swatch to open the color picker";
                    default:           return "点击色块打开颜色选择器喵";
                }
            }
        }

        // T24~T27 — 颜色标签（数组）
        public static string[] ColorLabels
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return new[] { "未知狀態", "已靜音", "已開麥", "斜線顏色" };
                    case LangId.JA:    return new[] { "状態不明", "ミュート中", "マイクオン", "スラッシュ色" };
                    case LangId.EN:    return new[] { "Unknown", "Muted", "Unmuted", "Slash Color" };
                    default:           return new[] { "未知状态", "已静音", "已开麦", "斜杠颜色" };
                }
            }
        }

        // T28
        public static string IconPreviewTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "圖標預覽";
                    case LangId.JA:    return "アイコンプレビュー";
                    case LangId.EN:    return "Icon Preview";
                    default:           return "图标预览";
                }
            }
        }

        // T29~T31 — 图标预览标签（数组）
        public static string[] IconPreviewLabels
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return new[] { "未知", "靜音", "開麥" };
                    case LangId.JA:    return new[] { "不明", "ミュート", "マイクオン" };
                    case LangId.EN:    return new[] { "Unknown", "Muted", "Unmuted" };
                    default:           return new[] { "未知", "静音", "开麦" };
                }
            }
        }

        // T32
        public static string BtnRestoreDefaults
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "恢復預設";
                    case LangId.JA:    return "デフォルトに戻す";
                    case LangId.EN:    return "Restore Defaults";
                    default:           return "恢复默认";
                }
            }
        }

        // T33
        public static string BtnSave
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "儲存";
                    case LangId.JA:    return "保存";
                    case LangId.EN:    return "Save";
                    default:           return "保存";
                }
            }
        }

        // T34 / T44 / T52 — 取消（通用）
        public static string BtnCancel
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "取消";
                    case LangId.JA:    return "キャンセル";
                    case LangId.EN:    return "Cancel";
                    default:           return "取消";
                }
            }
        }

        // ── ColorPickerDialog (T35~T44) ──────────────────

        // T35
        public static string ColorPickerTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "選擇顏色";
                    case LangId.JA:    return "色の選択";
                    case LangId.EN:    return "Choose Color";
                    default:           return "选择颜色";
                }
            }
        }

        // T36
        public static string HueLabel
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "色相";
                    case LangId.JA:    return "色相";
                    case LangId.EN:    return "Hue";
                    default:           return "色相";
                }
            }
        }

        // T37
        public static string PreviewLabel
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "預覽";
                    case LangId.JA:    return "プレビュー";
                    case LangId.EN:    return "Preview";
                    default:           return "预览";
                }
            }
        }

        // T38
        public static string PresetLabel
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "預設";
                    case LangId.JA:    return "プリセット";
                    case LangId.EN:    return "Presets";
                    default:           return "预设";
                }
            }
        }

        // T39
        public static string RecentLabel
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "最近使用";
                    case LangId.JA:    return "最近使用した色";
                    case LangId.EN:    return "Recently Used";
                    default:           return "最近使用";
                }
            }
        }

        // T40
        public static string NoRecentColors
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "沒有最近使用";
                    case LangId.JA:    return "最近使用した色はありません";
                    case LangId.EN:    return "No recent colors";
                    default:           return "没有最近使用";
                }
            }
        }

        // T41
        public static string CurrentLabel
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "當前";
                    case LangId.JA:    return "現在";
                    case LangId.EN:    return "Current";
                    default:           return "当前";
                }
            }
        }

        // T42
        public static string OriginalLabel
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "初始";
                    case LangId.JA:    return "元の色";
                    case LangId.EN:    return "Original";
                    default:           return "初始";
                }
            }
        }

        // T43
        public static string BtnOk
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "確定";
                    case LangId.JA:    return "OK";
                    case LangId.EN:    return "OK";
                    default:           return "确定";
                }
            }
        }

        // ── HotkeyCaptureForm (T45~T57) ──────────────────

        // T45
        public static string HotkeyFormTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "設定你的快捷鍵喵";
                    case LangId.JA:    return "ショートカットを設定してにゃ";
                    case LangId.EN:    return "Set Your Hotkey";
                    default:           return "设置你的快捷键喵";
                }
            }
        }

        // T46
        public static string ComboWaiting
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "當前組合：(等待輸入)";
                    case LangId.JA:    return "現在の組合せ：（入力待ち）";
                    case LangId.EN:    return "Current combo: (waiting for input...)";
                    default:           return "当前组合：(等待输入)";
                }
            }
        }

        // T47 前缀
        public static string ComboPrefix
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "當前組合：";
                    case LangId.JA:    return "現在の組合せ：";
                    case LangId.EN:    return "Current combo: ";
                    default:           return "当前组合：";
                }
            }
        }

        // T48
        public static string WaitingMainKey
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "(等待主鍵)";
                    case LangId.JA:    return "（メインキー待ち）";
                    case LangId.EN:    return "(waiting for main key)";
                    default:           return "(等待主键)";
                }
            }
        }

        // T49
        public static string HotkeyHint
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "按下組合後放開即可鎖定\n按 Enter 確認 / Esc 清除";
                    case LangId.JA:    return "キーを押して離すと確定します\nEnterで確認 / Escでクリア";
                    case LangId.EN:    return "Press your key combination, then release to lock it in.\nEnter to confirm / Esc to clear";
                    default:           return "按下组合后松开即可锁定\n按 Enter 确认 / Esc 清除";
                }
            }
        }

        // T50
        public static string BtnClear
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "清除";
                    case LangId.JA:    return "クリア";
                    case LangId.EN:    return "Clear";
                    default:           return "清除";
                }
            }
        }

        // T51
        public static string BtnConfirm
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "確認";
                    case LangId.JA:    return "確認";
                    case LangId.EN:    return "Confirm";
                    default:           return "确认";
                }
            }
        }

        // T53
        public static string NeedMainKeyMsg
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "請按一個主鍵（如字母、數字、F1-F24 等）\n\n不能只用修飾鍵";
                    case LangId.JA:    return "メインキー（英字、数字、F1〜F24など）を押してください\n\n修飾キーだけでは設定できません";
                    case LangId.EN:    return "Please press a main key (letter, number, F1-F24, etc.)\n\nModifier keys alone are not enough.";
                    default:           return "请按一个主键（如字母、数字、F1-F24 等）\n\n不能只用修饰键";
                }
            }
        }

        // T54
        public static string WarningTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "警告";
                    case LangId.JA:    return "警告";
                    case LangId.EN:    return "Warning";
                    default:           return "警告";
                }
            }
        }

        // T55
        public static string NoModWarningMsg
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "單獨使用此鍵容易與其他程式衝突，確定要使用嗎？";
                    case LangId.JA:    return "修飾キーなしで単独使用すると、他のアプリと競合する可能性があります。\nこのまま設定しますか？";
                    case LangId.EN:    return "Using this key without modifiers may conflict with other apps. Are you sure?";
                    default:           return "单独使用此键容易与其他程序冲突，确定要使用吗？";
                }
            }
        }

        // T56
        public static string HotkeyConflictMsg
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "該快捷鍵已被其他程式佔用（錯誤碼 {0}）\n\n請更換組合";
                    case LangId.JA:    return "このショートカットは既に他のアプリで使用されています（エラーコード {0}）\n\n別の組合せをお試しください";
                    case LangId.EN:    return "This hotkey is already in use by another application (error code: {0}).\n\nPlease try a different combination.";
                    default:           return "该快捷键已被其他程序占用（错误码 {0}）\n\n请更换组合";
                }
            }
        }

        // T57
        public static string InfoTitle
        {
            get
            {
                switch (Current)
                {
                    case LangId.ZH_TW: return "提示";
                    case LangId.JA:    return "お知らせ";
                    case LangId.EN:    return "Info";
                    default:           return "提示";
                }
            }
        }
    }
}
