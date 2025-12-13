using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace OPFlashTool.Localization
{
    /// <summary>
    /// 多语言管理器 / Language Manager
    /// 支持中文和英文界面切换
    /// </summary>
    public static class LanguageManager
    {
        private static ResourceManager? _resourceManager;
        private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        /// <summary>
        /// 支持的语言列表
        /// </summary>
        public static readonly (string Code, string Name, string NativeName)[] SupportedLanguages = new[]
        {
            ("zh-CN", "Chinese (Simplified)", "简体中文"),
            ("en", "English", "English")
        };

        /// <summary>
        /// 当前语言代码
        /// </summary>
        public static string CurrentLanguageCode => _currentCulture.Name;

        /// <summary>
        /// 语言变更事件
        /// </summary>
        public static event EventHandler? LanguageChanged;

        /// <summary>
        /// 初始化语言管理器
        /// </summary>
        public static void Initialize()
        {
            _resourceManager = new ResourceManager(
                "OPFlashTool.Properties.Resources",
                typeof(LanguageManager).Assembly);

            // 尝试从配置加载保存的语言设置
            var savedLanguage = LoadSavedLanguage();
            if (!string.IsNullOrEmpty(savedLanguage))
            {
                SetLanguage(savedLanguage, false);
            }
        }

        /// <summary>
        /// 设置当前语言
        /// </summary>
        /// <param name="cultureCode">语言代码 (如 "zh-CN", "en")</param>
        /// <param name="savePreference">是否保存偏好设置</param>
        public static void SetLanguage(string cultureCode, bool savePreference = true)
        {
            try
            {
                _currentCulture = new CultureInfo(cultureCode);
                Thread.CurrentThread.CurrentUICulture = _currentCulture;
                Thread.CurrentThread.CurrentCulture = _currentCulture;

                if (savePreference)
                {
                    SaveLanguagePreference(cultureCode);
                }

                LanguageChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (CultureNotFoundException)
            {
                // 如果语言代码无效，使用默认语言
                _currentCulture = new CultureInfo("zh-CN");
            }
        }

        /// <summary>
        /// 获取本地化字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>本地化后的字符串</returns>
        public static string GetString(string key)
        {
            if (_resourceManager == null)
            {
                Initialize();
            }

            try
            {
                var value = _resourceManager?.GetString(key, _currentCulture);
                return value ?? key; // 如果找不到，返回键名
            }
            catch
            {
                return key;
            }
        }

        /// <summary>
        /// 获取本地化字符串（带格式化参数）
        /// </summary>
        public static string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        /// <summary>
        /// 快捷方法：获取本地化字符串
        /// </summary>
        public static string L(string key) => GetString(key);

        /// <summary>
        /// 快捷方法：获取本地化字符串（带参数）
        /// </summary>
        public static string L(string key, params object[] args) => GetString(key, args);

        /// <summary>
        /// 检查是否为中文环境
        /// </summary>
        public static bool IsChinese => _currentCulture.Name.StartsWith("zh");

        /// <summary>
        /// 检查是否为英文环境
        /// </summary>
        public static bool IsEnglish => _currentCulture.Name.StartsWith("en");

        /// <summary>
        /// 切换语言（在中英文之间切换）
        /// </summary>
        public static void ToggleLanguage()
        {
            if (IsChinese)
            {
                SetLanguage("en");
            }
            else
            {
                SetLanguage("zh-CN");
            }
        }

        /// <summary>
        /// 保存语言偏好到配置文件
        /// </summary>
        private static void SaveLanguagePreference(string cultureCode)
        {
            try
            {
                Properties.Settings.Default["Language"] = cultureCode;
                Properties.Settings.Default.Save();
            }
            catch
            {
                // 忽略保存错误
            }
        }

        /// <summary>
        /// 从配置文件加载保存的语言设置
        /// </summary>
        private static string? LoadSavedLanguage()
        {
            try
            {
                return Properties.Settings.Default["Language"] as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
