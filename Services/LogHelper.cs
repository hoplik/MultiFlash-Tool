using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace OPFlashTool.Services
{
    /// <summary>
    /// 统一日志帮助类
    /// </summary>
    public static class LogHelper
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;
        private static bool _enableFileLogging = true;
        
        /// <summary>
        /// 初始化日志文件路径
        /// </summary>
        public static void Initialize(string logDirectory = null)
        {
            logDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiFlashTool", "Logs");
            
            if (!Directory.Exists(logDirectory))
            {
                try { Directory.CreateDirectory(logDirectory); }
                catch { _enableFileLogging = false; return; }
            }
            
            _logFilePath = Path.Combine(logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
        }
        
        /// <summary>
        /// 记录信息
        /// </summary>
        public static void Info(string message) => Log("INFO", message);
        
        /// <summary>
        /// 记录警告
        /// </summary>
        public static void Warn(string message) => Log("WARN", message);
        
        /// <summary>
        /// 记录错误
        /// </summary>
        public static void Error(string message) => Log("ERROR", message);
        
        /// <summary>
        /// 记录错误及异常
        /// </summary>
        public static void Error(string message, Exception ex) => Log("ERROR", $"{message}: {ex.Message}\n{ex.StackTrace}");
        
        /// <summary>
        /// 记录调试信息
        /// </summary>
        public static void Debug(string message)
        {
#if DEBUG
            Log("DEBUG", message);
#endif
        }
        
        /// <summary>
        /// 记录协议通信
        /// </summary>
        public static void Protocol(string protocol, string direction, string message)
        {
            Log(protocol, $"[{direction}] {message}");
        }
        
        private static void Log(string level, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logLine = $"[{timestamp}] [{level}] {message}";
            
            // 输出到调试窗口
            System.Diagnostics.Debug.WriteLine(logLine);
            
            // 写入文件
            if (_enableFileLogging && !string.IsNullOrEmpty(_logFilePath))
            {
                lock (_lock)
                {
                    try
                    {
                        File.AppendAllText(_logFilePath, logLine + Environment.NewLine, Encoding.UTF8);
                    }
                    catch { /* 忽略文件写入错误 */ }
                }
            }
        }
        
        /// <summary>
        /// 获取日志颜色
        /// </summary>
        public static Color GetLogColor(string level)
        {
            return level.ToUpper() switch
            {
                "ERROR" => Color.Red,
                "WARN" => Color.Orange,
                "INFO" => Color.Black,
                "DEBUG" => Color.Gray,
                "SAHARA" => Color.Purple,
                "FIREHOSE" => Color.Blue,
                _ => Color.Black
            };
        }
        
        /// <summary>
        /// 格式化字节大小
        /// </summary>
        public static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// 格式化速度
        /// </summary>
        public static string FormatSpeed(double bytesPerSecond)
        {
            return $"{FormatSize((long)bytesPerSecond)}/s";
        }
        
        /// <summary>
        /// 格式化耗时
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 60)
                return $"{duration.TotalSeconds:0.0}秒";
            if (duration.TotalMinutes < 60)
                return $"{duration.Minutes}分{duration.Seconds}秒";
            return $"{duration.Hours}时{duration.Minutes}分{duration.Seconds}秒";
        }
    }
}
