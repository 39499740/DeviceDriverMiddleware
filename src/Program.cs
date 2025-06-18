using System;
using System.Threading;
using System.Windows.Forms;

namespace TwainMiddleware
{
    /// <summary>
    /// 程序主入口
    /// </summary>
    static class Program
    {
        private static TrayManager trayManager;
        private static WebSocketServerWrapper webSocketServer;
        private static TwainService twainService;
        private static HttpServer httpServer;
        private static Mutex mutex;

        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 检查是否已经有实例在运行
            bool createdNew;
            mutex = new Mutex(true, "TwainMiddleware_SingleInstance", out createdNew);
            
            if (!createdNew)
            {
                MessageBox.Show("TWAIN扫描仪中间件已经在运行中！", "提示", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 设置应用程序
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // 初始化日志
                Logger.Initialize();
                Logger.Info("TWAIN扫描仪中间件启动...");

                // 初始化配置
                Config.Initialize();

                // 初始化TWAIN服务
                twainService = new TwainService();
                
                // 初始化WebSocket服务器
                webSocketServer = new WebSocketServerWrapper();
                WebSocketServerWrapper.SetTwainService(twainService);
                
                // 初始化HTTP服务器
                httpServer = new HttpServer(Config.WebSocketPort + 1);
                
                // 初始化托盘管理器
                trayManager = new TrayManager(webSocketServer, httpServer);

                // 设置应用程序退出事件
                Application.ApplicationExit += Application_ApplicationExit;

                // 启动服务
                StartServices();

                // 运行应用程序
                Application.Run();
            }
            catch (Exception ex)
            {
                Logger.Error("程序启动失败: " + ex.Message, ex);
                MessageBox.Show("程序启动失败: " + ex.Message, "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (mutex != null)
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                }
            }
        }

        /// <summary>
        /// 启动所有服务
        /// </summary>
        private static void StartServices()
        {
            try
            {
                // 启动TWAIN服务
                twainService.Initialize();
                Logger.Info("TWAIN服务已启动");

                // 启动WebSocket服务器
                webSocketServer.Start();
                Logger.Info("WebSocket服务器已启动，端口: " + Config.WebSocketPort.ToString());

                // 启动HTTP服务器
                httpServer.Start();
                Logger.Info("HTTP服务器已启动，端口: " + (Config.WebSocketPort + 1).ToString());

                // 显示托盘图标
                trayManager.Show();
                Logger.Info("托盘图标已显示");

                // 显示启动通知
                if (Config.ShowTrayNotifications)
                {
                    trayManager.ShowNotification("TWAIN扫描仪中间件", 
                        "服务已启动，端口: " + Config.WebSocketPort.ToString(),
                        ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("服务启动失败: " + ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// 停止所有服务
        /// </summary>
        public static void StopServices()
        {
            try
            {
                Logger.Info("正在停止服务...");

                // 停止WebSocket服务器
                if (webSocketServer != null)
                    webSocketServer.Stop();
                Logger.Info("WebSocket服务器已停止");

                // 停止HTTP服务器
                if (httpServer != null)
                    httpServer.Stop();
                Logger.Info("HTTP服务器已停止");

                // 停止TWAIN服务
                if (twainService != null)
                    twainService.Dispose();
                Logger.Info("TWAIN服务已停止");

                Logger.Info("所有服务已停止");
            }
            catch (Exception ex)
            {
                Logger.Error("停止服务时发生错误: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 重启服务
        /// </summary>
        public static void RestartServices()
        {
            try
            {
                Logger.Info("正在重启服务...");
                
                StopServices();
                
                // 重新加载配置
                Config.Reload();
                
                // 重新创建服务实例
                webSocketServer = new WebSocketServerWrapper();
                httpServer = new HttpServer(Config.WebSocketPort + 1);
                
                // 更新托盘管理器的HTTP服务器引用
                if (trayManager != null)
                {
                    trayManager.UpdateHttpServer(httpServer);
                }
                
                StartServices();
                
                Logger.Info("服务重启完成");
            }
            catch (Exception ex)
            {
                Logger.Error("重启服务失败: " + ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// 应用程序退出事件处理
        /// </summary>
        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            StopServices();
            if (trayManager != null)
                trayManager.Hide();
            Logger.Info("TWAIN扫描仪中间件已退出");
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        public static void ExitApplication()
        {
            Application.Exit();
        }
    }
} 