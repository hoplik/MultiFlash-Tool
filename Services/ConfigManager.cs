using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OPFlashTool.Services
{
    /// <summary>
    /// 配置管理器
    /// </summary>
    public static class ConfigManager
    {
        private static Dictionary<string, object> _config = new Dictionary<string, object>();
        private static string _configPath;
        private static readonly object _lock = new object();
        
        /// <summary>
        /// 默认配置值
        /// </summary>
        private static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>
        {
            // 串口配置
            ["SerialPort.BaudRate"] = 115200,
            ["SerialPort.ReadTimeout"] = 5000,
            ["SerialPort.WriteTimeout"] = 5000,
            ["SerialPort.ReadBufferSize"] = 1048576,
            ["SerialPort.WriteBufferSize"] = 1048576,
            
            // Firehose 配置
            ["Firehose.MaxPayloadSize"] = 1048576,
            ["Firehose.MaxRetries"] = 3,
            ["Firehose.AckTimeout"] = 5000,
            
            // Sahara 配置
            ["Sahara.HelloTimeout"] = 2000,
            ["Sahara.TransferTimeout"] = 30000,
            
            // 热插拔配置
            ["HotPlug.RetryCount"] = 3,
            ["HotPlug.RetryDelayMs"] = 1000,
            
            // UI 配置
            ["UI.AutoScrollLog"] = true,
            ["UI.MaxLogLines"] = 1000,
            
            // 调试配置
            ["Debug.VerboseLog"] = false,
            ["Debug.SaveProtocolLog"] = false,
        };
        
        /// <summary>
        /// 初始化配置
        /// </summary>
        public static void Initialize(string configPath = null)
        {
            _configPath = configPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.user.json");
            Load();
        }
        
        /// <summary>
        /// 获取配置值
        /// </summary>
        public static T Get<T>(string key, T defaultValue = default)
        {
            lock (_lock)
            {
                if (_config.TryGetValue(key, out object value))
                {
                    try
                    {
                        if (value is JsonElement je)
                        {
                            return JsonSerializer.Deserialize<T>(je.GetRawText());
                        }
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch (Exception ex) 
                    { 
                        System.Diagnostics.Debug.WriteLine($"[ConfigManager] Get<{typeof(T).Name}>({key}) conversion failed: {ex.Message}");
                    }
                }
                
                if (Defaults.TryGetValue(key, out object defValue))
                {
                    return (T)Convert.ChangeType(defValue, typeof(T));
                }
                
                return defaultValue;
            }
        }
        
        /// <summary>
        /// 设置配置值
        /// </summary>
        public static void Set<T>(string key, T value)
        {
            lock (_lock)
            {
                _config[key] = value;
            }
        }
        
        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_configPath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_configPath))
                    {
                        var json = File.ReadAllText(_configPath);
                        _config = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
                    _config = new Dictionary<string, object>();
                }
            }
        }
        
        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _config.Clear();
                foreach (var kv in Defaults)
                {
                    _config[kv.Key] = kv.Value;
                }
                Save();
            }
        }
    }
}
