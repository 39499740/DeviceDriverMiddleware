using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TwainMiddleware
{
    /// <summary>
    /// 系统托盘管理器
    /// </summary>
    public class TrayManager : IDisposable
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private readonly WebSocketServerWrapper webSocketServer;
        private HttpServer httpServer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="webSocketServer">WebSocket服务器实例</param>
        /// <param name="httpServer">HTTP服务器实例</param>
        public TrayManager(WebSocketServerWrapper webSocketServer, HttpServer httpServer)
        {
            this.webSocketServer = webSocketServer;
            this.httpServer = httpServer;
            InitializeNotifyIcon();
            CreateContextMenu();
        }

        /// <summary>
        /// 初始化通知图标
        /// </summary>
        private void InitializeNotifyIcon()
        {
            try
            {
                notifyIcon = new NotifyIcon();
                
                // 设置自定义图标
                notifyIcon.Icon = CreateCustomIcon();
                
                // 设置文本
                notifyIcon.Text = "TWAIN扫描仪中间件 - 端口: " + Config.WebSocketPort.ToString();
                
                // 设置双击事件
                notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
                
                Logger.Debug("通知图标初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error("初始化通知图标失败: " + ex.Message, ex);
                // 如果自定义图标创建失败，使用系统默认图标
                if (notifyIcon != null)
                {
                    notifyIcon.Icon = SystemIcons.Application;
                }
            }
        }

        /// <summary>
        /// 创建自定义图标
        /// </summary>
        private Icon CreateCustomIcon()
        {
            try
            {
                // 创建16x16的位图
                using (var bitmap = new Bitmap(16, 16))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // 设置高质量绘图
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        
                        // 填充背景为透明
                        graphics.Clear(Color.Transparent);
                        
                        // 绘制扫描仪图标
                        // 外框 - 扫描仪主体
                        using (var brush = new SolidBrush(Color.FromArgb(45, 85, 135)))
                        {
                            graphics.FillRectangle(brush, 2, 4, 12, 8);
                        }
                        
                        // 扫描仪顶部
                        using (var brush = new SolidBrush(Color.FromArgb(60, 110, 165)))
                        {
                            graphics.FillRectangle(brush, 1, 2, 14, 3);
                        }
                        
                        // 扫描仪按钮
                        using (var brush = new SolidBrush(Color.FromArgb(100, 200, 100)))
                        {
                            graphics.FillEllipse(brush, 12, 3, 2, 2);
                        }
                        
                        // 扫描仪显示屏
                        using (var brush = new SolidBrush(Color.FromArgb(30, 30, 30)))
                        {
                            graphics.FillRectangle(brush, 4, 6, 6, 4);
                        }
                        
                        // 文档图标
                        using (var brush = new SolidBrush(Color.White))
                        {
                            graphics.FillRectangle(brush, 5, 7, 4, 2);
                        }
                        
                        // 扫描线
                        using (var pen = new Pen(Color.FromArgb(255, 100, 100), 1))
                        {
                            graphics.DrawLine(pen, 5, 8, 8, 8);
                        }
                    }
                    
                    // 将位图转换为图标
                    IntPtr hIcon = bitmap.GetHicon();
                    Icon icon = Icon.FromHandle(hIcon);
                    return icon;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("创建自定义图标失败，使用系统默认图标: " + ex.Message);
                return SystemIcons.Application;
            }
        }

        /// <summary>
        /// 创建右键菜单
        /// </summary>
        private void CreateContextMenu()
        {
            try
            {
                contextMenu = new ContextMenuStrip();
                
                // 当前端口信息
                var portInfoItem = new ToolStripMenuItem("当前端口: " + Config.WebSocketPort.ToString());
                portInfoItem.Enabled = false;
                contextMenu.Items.Add(portInfoItem);
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                // 修改端口
                var changePortItem = new ToolStripMenuItem("修改端口");
                changePortItem.Click += ChangePortItem_Click;
                contextMenu.Items.Add(changePortItem);
                
                // 重置端口
                var resetPortItem = new ToolStripMenuItem("重置端口 (45677)");
                resetPortItem.Click += ResetPortItem_Click;
                contextMenu.Items.Add(resetPortItem);
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                // 显示测试页面
                var testPageItem = new ToolStripMenuItem("显示测试页面");
                testPageItem.Click += TestPageItem_Click;
                contextMenu.Items.Add(testPageItem);
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                // 退出
                var exitItem = new ToolStripMenuItem("退出");
                exitItem.Click += ExitItem_Click;
                contextMenu.Items.Add(exitItem);
                
                // 设置右键菜单
                notifyIcon.ContextMenuStrip = contextMenu;
                
                Logger.Debug("右键菜单创建完成");
            }
            catch (Exception ex)
            {
                Logger.Error("创建右键菜单失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 修改端口菜单项点击事件
        /// </summary>
        private void ChangePortItem_Click(object sender, EventArgs e)
        {
            try
            {
                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    "请输入新的端口号 (1-65535):",
                    "修改端口",
                    Config.WebSocketPort.ToString());

                if (string.IsNullOrEmpty(input))
                    return;

                int newPort;
                if (int.TryParse(input, out newPort) && newPort >= 1 && newPort <= 65535)
                {
                    Config.UpdateWebSocketPort(newPort);
                    Program.RestartServices();
                    UpdateContextMenu();
                    ShowNotification("端口修改成功", "新端口: " + newPort.ToString(), ToolTipIcon.Info);
                }
                else
                {
                    MessageBox.Show("端口号必须是1-65535之间的数字！", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("修改端口号失败: " + ex.Message, ex);
                MessageBox.Show("修改端口号失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 重置端口菜单项点击事件
        /// </summary>
        private void ResetPortItem_Click(object sender, EventArgs e)
        {
            try
            {
                Config.UpdateWebSocketPort(45677);
                Program.RestartServices();
                UpdateContextMenu();
                ShowNotification("端口重置成功", "端口已重置为: 45677", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Logger.Error("重置端口号失败: " + ex.Message, ex);
                MessageBox.Show("重置端口号失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 显示测试页面菜单项点击事件
        /// </summary>
        private void TestPageItem_Click(object sender, EventArgs e)
        {
            try
            {
                // 确保HTTP服务器正在运行
                if (httpServer == null)
                {
                    MessageBox.Show("HTTP服务器未初始化", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 获取HTTP服务器URL
                string url = httpServer.GetServerUrl();
                
                // 在默认浏览器中打开测试页面
                System.Diagnostics.Process.Start(url);
                Logger.Info("显示测试页面: " + url);
                
                // 显示通知
                ShowNotification("测试页面已打开", "已在浏览器中打开测试页面", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Logger.Error("显示测试页面失败: " + ex.Message, ex);
                MessageBox.Show("显示测试页面失败。\n\n错误详情: " + ex.Message, 
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 退出菜单项点击事件
        /// </summary>
        private void ExitItem_Click(object sender, EventArgs e)
        {
            try
            {
                // 直接退出，不需要确认框
                Program.ExitApplication();
            }
            catch (Exception ex)
            {
                Logger.Error("退出程序失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 更新右键菜单
        /// </summary>
        public void UpdateContextMenu()
        {
            try
            {
                if (contextMenu != null && contextMenu.Items.Count > 0)
                {
                    contextMenu.Items[0].Text = "当前端口: " + Config.WebSocketPort.ToString();
                }
                if (notifyIcon != null)
                {
                    notifyIcon.Text = "TWAIN扫描仪中间件 - 端口: " + Config.WebSocketPort.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("更新右键菜单失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 更新HTTP服务器引用
        /// </summary>
        /// <param name="newHttpServer">新的HTTP服务器实例</param>
        public void UpdateHttpServer(HttpServer newHttpServer)
        {
            this.httpServer = newHttpServer;
            Logger.Debug("托盘管理器HTTP服务器引用已更新");
        }

        /// <summary>
        /// 双击托盘图标事件
        /// </summary>
        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            // 双击时显示测试页面
            TestPageItem_Click(sender, e);
        }

        /// <summary>
        /// 显示托盘图标
        /// </summary>
        public void Show()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = true;
            }
        }

        /// <summary>
        /// 隐藏托盘图标
        /// </summary>
        public void Hide()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
            }
        }

        /// <summary>
        /// 显示通知
        /// </summary>
        public void ShowNotification(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (notifyIcon != null && notifyIcon.Visible)
                {
                    notifyIcon.ShowBalloonTip(3000, title, text, icon);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("显示通知失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                    notifyIcon = null;
                }

                if (contextMenu != null)
                {
                    contextMenu.Dispose();
                    contextMenu = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("释放托盘管理器资源失败: " + ex.Message, ex);
            }
        }
    }
} 
