using System;
using System.Configuration;
using System.IO;

namespace TwainMiddleware
{
    /// <summary>
    /// 配置管理类
    /// </summary>
    public static class Config
    {
        /// <summary>
        /// WebSocket服务器端口
        /// </summary>
        public static int WebSocketPort { get; private set; }

        /// <summary>
        /// WebSocket服务器主机
        /// </summary>
        public static string WebSocketHost { get; private set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public static string LogLevel { get; private set; }

        /// <summary>
        /// 是否记录到文件
        /// </summary>
        public static bool LogToFile { get; private set; }

        /// <summary>
        /// 日志文件路径
        /// </summary>
        public static string LogFilePath { get; private set; }

        /// <summary>
        /// 默认分辨率
        /// </summary>
        public static int DefaultResolution { get; private set; }

        /// <summary>
        /// 默认颜色模式
        /// </summary>
        public static string DefaultColorMode { get; private set; }

        /// <summary>
        /// 默认图像格式
        /// </summary>
        public static string DefaultFormat { get; private set; }

        /// <summary>
        /// 是否自动启动服务
        /// </summary>
        public static bool AutoStartService { get; private set; }

        /// <summary>
        /// 是否最小化到托盘
        /// </summary>
        public static bool MinimizeToTray { get; private set; }

        /// <summary>
        /// 是否显示托盘通知
        /// </summary>
        public static bool ShowTrayNotifications { get; private set; }

        /// <summary>
        /// 静态构造函数 - 设置默认值
        /// </summary>
        static Config()
        {
            // 设置默认值
            WebSocketPort = 45677;
            WebSocketHost = "localhost";
            LogLevel = "Info";
            LogToFile = true;
            LogFilePath = "logs\\twain-middleware.log";
            DefaultResolution = 300;
            DefaultColorMode = "Color";
            DefaultFormat = "PNG";
            AutoStartService = true;
            MinimizeToTray = true;
            ShowTrayNotifications = true;
        }

        /// <summary>
        /// 初始化配置
        /// </summary>
        public static void Initialize()
        {
            try
            {
                LoadConfiguration();
                ValidateConfiguration();
                CreateDirectories();
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("配置初始化失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public static void Reload()
        {
            try
            {
                ConfigurationManager.RefreshSection("appSettings");
                LoadConfiguration();
                ValidateConfiguration();
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("配置重新加载失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private static void LoadConfiguration()
        {
            // WebSocket配置
            WebSocketPort = GetIntSetting("WebSocketPort", 45677);
            WebSocketHost = GetStringSetting("WebSocketHost", "localhost");

            // 日志配置
            LogLevel = GetStringSetting("LogLevel", "Info");
            LogToFile = GetBoolSetting("LogToFile", true);
            LogFilePath = GetStringSetting("LogFilePath", "logs\\twain-middleware.log");

            // TWAIN配置
            DefaultResolution = GetIntSetting("DefaultResolution", 300);
            DefaultColorMode = GetStringSetting("DefaultColorMode", "Color");
            DefaultFormat = GetStringSetting("DefaultFormat", "PNG");

            // 系统配置
            AutoStartService = GetBoolSetting("AutoStartService", true);
            MinimizeToTray = GetBoolSetting("MinimizeToTray", true);
            ShowTrayNotifications = GetBoolSetting("ShowTrayNotifications", true);
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        private static void ValidateConfiguration()
        {
            // 验证端口号
            if (WebSocketPort < 1 || WebSocketPort > 65535)
                throw new ConfigurationErrorsException("WebSocket端口号无效: " + WebSocketPort);

            // 验证分辨率
            if (DefaultResolution < 50 || DefaultResolution > 2400)
                throw new ConfigurationErrorsException("默认分辨率无效: " + DefaultResolution);

            // 验证颜色模式
            if (DefaultColorMode != "Color" && DefaultColorMode != "Gray" && DefaultColorMode != "BlackWhite")
                throw new ConfigurationErrorsException("默认颜色模式无效: " + DefaultColorMode);

            // 验证图像格式
            if (DefaultFormat != "PNG" && DefaultFormat != "JPEG" && DefaultFormat != "TIFF" && DefaultFormat != "BMP")
                throw new ConfigurationErrorsException("默认图像格式无效: " + DefaultFormat);
        }

        /// <summary>
        /// 创建必要的目录
        /// </summary>
        private static void CreateDirectories()
        {
            try
            {
                // 创建日志目录
                if (LogToFile && !string.IsNullOrEmpty(LogFilePath))
                {
                    string logDir = Path.GetDirectoryName(LogFilePath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("创建目录失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 更新WebSocket端口
        /// </summary>
        /// <param name="port">新端口号</param>
        public static void UpdateWebSocketPort(int port)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentException("端口号无效: " + port);

            WebSocketPort = port;
            UpdateAppSetting("WebSocketPort", port.ToString());
        }

        /// <summary>
        /// 获取字符串配置项
        /// </summary>
        private static string GetStringSetting(string key, string defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        /// <summary>
        /// 获取整数配置项
        /// </summary>
        private static int GetIntSetting(string key, int defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            int result;
            if (int.TryParse(value, out result))
                return result;

            return defaultValue;
        }

        /// <summary>
        /// 获取布尔配置项
        /// </summary>
        private static bool GetBoolSetting(string key, bool defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            bool result;
            if (bool.TryParse(value, out result))
                return result;

            return defaultValue;
        }

        /// <summary>
        /// 更新应用设置
        /// </summary>
        private static void UpdateAppSetting(string key, string value)
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                
                if (config.AppSettings.Settings[key] != null)
                    config.AppSettings.Settings[key].Value = value;
                else
                    config.AppSettings.Settings.Add(key, value);

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException("更新配置失败: " + ex.Message, ex);
            }
        }
    }
} 
