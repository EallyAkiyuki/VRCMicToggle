// AppLogger.cs — 日志工具：error.log（Release）+ debug.log（DEBUG only），自动轮转
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace VRCMicToggle
{
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
            WriteEntry("[INFO] " + msg);
        }

        public static void Warn(string msg)
        {
            WriteEntry("[WARN] " + msg);
        }

        public static void Log(string context, Exception ex)
        {
            string line = "[" + context + "] " + ex;
            WriteEntry("[ERROR] " + line);
#if DEBUG
            WriteTo(DebugLogPath, "[ERROR] " + line);
#endif
        }

        // ── 内部方法 ────────────────────────────────────

        // Info / Warn / Log 共用：写入 error.log，DEBUG 构建时同步写入 debug.log
        private static void WriteEntry(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " " + message + Environment.NewLine;
            try
            {
                EnsureDir(LogPath);
                File.AppendAllText(LogPath, line);
            }
            catch (Exception) { }
#if DEBUG
            try { WriteTo(DebugLogPath, line); } catch (Exception) { }
#endif
        }

        // 带自动轮转的单文件写入（Debug 专用）
        private static void WriteLog(string path, string level, string msg)
        {
            try
            {
                EnsureDir(path);
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

        private static void WriteTo(string path, string line)
        {
            EnsureDir(path);
            File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " " + line + Environment.NewLine);
        }

        private static void EnsureDir(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
    }
}
