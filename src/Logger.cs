using System;
using System.IO;
using System.Threading;

namespace TwainMiddleware
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// 日志管理类
    /// </summary>
    public static class Logger
    {
        private static LogLevel currentLogLevel = LogLevel.Info;
        private static readonly object lockObject = new object();
        private static StreamWriter fileWriter;
        private static bool initialized = false;

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static void Initialize()
        {
            lock (lockObject)
            {
                if (initialized)
                    return;

                try
                {
                    // 解析日志级别
                    LogLevel level;
                    if (Enum.TryParse(Config.LogLevel, true, out level))
                        currentLogLevel = level;

                    // 初始化文件日志
                    if (Config.LogToFile && !string.IsNullOrEmpty(Config.LogFilePath))
                    {
                        InitializeFileLogger();
                    }

                    initialized = true;
                    Info("日志系统已初始化");
                }
                catch (Exception ex)
                {
                    // 如果日志初始化失败，至少输出到控制台
                    Console.WriteLine("日志系统初始化失败: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 初始化文件日志
        /// </summary>
        private static void InitializeFileLogger()
        {
            try
            {
                string logDir = Path.GetDirectoryName(Config.LogFilePath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // 如果文件存在且大于10MB，则备份
                if (File.Exists(Config.LogFilePath))
                {
                    FileInfo fileInfo = new FileInfo(Config.LogFilePath);
                    if (fileInfo.Length > 10 * 1024 * 1024) // 10MB
                    {
                        string backupPath = Config.LogFilePath + ".bak";
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);
                        File.Move(Config.LogFilePath, backupPath);
                    }
                }

                fileWriter = new StreamWriter(Config.LogFilePath, true)
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("文件日志初始化失败: " + ex.Message);
                fileWriter = null;
            }
        }

        /// <summary>
        /// 记录调试信息
        /// </summary>
        public static void Debug(string message)
        {
            WriteLog(LogLevel.Debug, message, null);
        }

        /// <summary>
        /// 记录信息
        /// </summary>
        public static void Info(string message)
        {
            WriteLog(LogLevel.Info, message, null);
        }

        /// <summary>
        /// 记录警告
        /// </summary>
        public static void Warning(string message)
        {
            WriteLog(LogLevel.Warning, message, null);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        public static void Error(string message)
        {
            WriteLog(LogLevel.Error, message, null);
        }

        /// <summary>
        /// 记录错误（包含异常信息）
        /// </summary>
        public static void Error(string message, Exception exception)
        {
            WriteLog(LogLevel.Error, message, exception);
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        private static void WriteLog(LogLevel level, string message, Exception exception)
        {
            if (level < currentLogLevel)
                return;

            lock (lockObject)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string threadId = Thread.CurrentThread.ManagedThreadId.ToString("D3");
                    string levelStr = level.ToString().ToUpper().PadRight(7);
                    
                    string logMessage = "[" + timestamp + "] [" + threadId + "] [" + levelStr + "] " + message;

                    // 添加异常信息
                    if (exception != null)
                    {
                        logMessage += Environment.NewLine + "异常类型: " + exception.GetType().Name;
                        logMessage += Environment.NewLine + "异常信息: " + exception.Message;
                        logMessage += Environment.NewLine + "堆栈跟踪: " + exception.StackTrace;
                        
                        // 内部异常
                        Exception innerEx = exception.InnerException;
                        while (innerEx != null)
                        {
                            logMessage += Environment.NewLine + "内部异常: " + innerEx.Message;
                            innerEx = innerEx.InnerException;
                        }
                    }

                    // 输出到控制台
                    Console.WriteLine(logMessage);

                    // 输出到文件
                    if (fileWriter != null)
                    {
                        fileWriter.WriteLine(logMessage);
                    }
                }
                catch (Exception ex)
                {
                    // 如果日志记录失败，输出到控制台
                                    Console.WriteLine("日志记录失败: " + ex.Message);
                Console.WriteLine("原始消息: " + message);
                }
            }
        }

        /// <summary>
        /// 清理日志资源
        /// </summary>
        public static void Cleanup()
        {
            lock (lockObject)
            {
                try
                {
                    if (fileWriter != null)
                    {
                        fileWriter.Flush();
                        fileWriter.Close();
                        fileWriter.Dispose();
                        fileWriter = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("日志清理失败: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 设置日志级别
        /// </summary>
        public static void SetLogLevel(LogLevel level)
        {
            currentLogLevel = level;
            Info("日志级别已设置为: " + level);
        }

        /// <summary>
        /// 获取当前日志级别
        /// </summary>
        public static LogLevel GetLogLevel()
        {
            return currentLogLevel;
        }
    }
} 