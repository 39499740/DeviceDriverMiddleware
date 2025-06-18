using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TwainMiddleware
{
    /// <summary>
    /// 托盘管理器
    /// </summary>
    public class TrayManager
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private readonly WebSocketServer webSocketServer;
        private readonly HttpServer httpServer;

        /// <summary>
        /// 构造函数
        /// </summary>
        public TrayManager(WebSocketServer webSocketServer, HttpServer httpServer)
        {
            this.webSocketServer = webSocketServer ?? throw new ArgumentNullException(nameof(webSocketServer));
            this.httpServer = httpServer ?? throw new ArgumentNullException(nameof(httpServer));
            
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
                notifyIcon.Icon = SystemIcons.Application;
                notifyIcon.Text = $"TWAIN扫描仪中间件 - 端口: {Config.WebSocketPort}";
                notifyIcon.Visible = false;
                notifyIcon.DoubleClick += (s, e) => OpenTestPage();
            }
            catch (Exception ex)
            {
                Logger.Error($"初始化通知图标失败: {ex.Message}", ex);
                throw;
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

                // 当前端口号
                var portItem = new ToolStripMenuItem($"当前端口: {Config.WebSocketPort}");
                portItem.Enabled = false;
                contextMenu.Items.Add(portItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // 修改端口号
                var changePortItem = new ToolStripMenuItem("修改端口号");
                changePortItem.Click += (s, e) => ChangePort();
                contextMenu.Items.Add(changePortItem);

                // 重置为默认端口
                var resetPortItem = new ToolStripMenuItem("重置为默认端口 (45677)");
                resetPortItem.Click += (s, e) => ResetPort();
                contextMenu.Items.Add(resetPortItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // 打开测试页面
                var testPageItem = new ToolStripMenuItem("打开测试页面");
                testPageItem.Click += (s, e) => OpenTestPage();
                contextMenu.Items.Add(testPageItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // 退出程序
                var exitItem = new ToolStripMenuItem("退出程序");
                exitItem.Click += (s, e) => ExitApplication();
                contextMenu.Items.Add(exitItem);

                notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex)
            {
                Logger.Error($"创建右键菜单失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 修改端口号事件处理
        /// </summary>
        private void ChangePort()
        {
            try
            {
                using (var form = new Form())
                {
                    form.Text = "修改端口号";
                    form.Size = new Size(300, 150);
                    form.StartPosition = FormStartPosition.CenterScreen;
                    form.FormBorderStyle = FormBorderStyle.FixedDialog;
                    form.MaximizeBox = false;
                    form.MinimizeBox = false;

                    var label = new Label { Text = "请输入新的端口号 (1-65535):", Location = new Point(20, 20), AutoSize = true };
                    var textBox = new TextBox { Text = Config.WebSocketPort.ToString(), Location = new Point(20, 45), Width = 200 };
                    var okButton = new Button { Text = "确定", Location = new Point(130, 80), DialogResult = DialogResult.OK };
                    var cancelButton = new Button { Text = "取消", Location = new Point(210, 80), DialogResult = DialogResult.Cancel };

                    form.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
                    form.AcceptButton = okButton;
                    form.CancelButton = cancelButton;

                    if (form.ShowDialog() == DialogResult.OK && int.TryParse(textBox.Text, out int newPort))
                {
                    if (newPort >= 1 && newPort <= 65535)
                    {
                        Config.UpdateWebSocketPort(newPort);
                        UpdateContextMenu();
                        Program.RestartServices();
                        ShowNotification("端口修改成功", $"新端口: {newPort}", ToolTipIcon.Info);
                    }
                    else
                    {
                        MessageBox.Show("端口号必须在 1-65535 范围内。");
                    }
                }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"修改端口号失败: {ex.Message}", ex);
                MessageBox.Show($"修改端口号失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置端口号事件处理
        /// </summary>
        private void ResetPort()
        {
            try
            {
                Config.UpdateWebSocketPort(45677);
                UpdateContextMenu();
                Program.RestartServices();
                ShowNotification("端口重置成功", "端口: 45677", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Logger.Error($"重置端口号失败: {ex.Message}", ex);
                MessageBox.Show($"重置端口号失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开测试页面事件处理
        /// </summary>
        private void OpenTestPage()
        {
            try
            {
                string url = $"http://localhost:{Config.HttpPort}/test";
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Logger.Error($"打开测试页面失败: {ex.Message}", ex);
                MessageBox.Show($"打开测试页面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 退出程序事件处理
        /// </summary>
        private void ExitApplication()
        {
            try
            {
                var result = MessageBox.Show("确定要退出TWAIN扫描仪中间件吗？", 
                    "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    Program.ExitApplication();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"退出程序失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新右键菜单
        /// </summary>
        private void UpdateContextMenu()
        {
            try
            {
                if (contextMenu != null && contextMenu.Items.Count > 0)
                {
                    contextMenu.Items[0].Text = $"当前端口: {Config.WebSocketPort}";
                }
                notifyIcon.Text = $"TWAIN扫描仪中间件 - 端口: {Config.WebSocketPort}";
            }
            catch (Exception ex)
            {
                Logger.Error($"更新右键菜单失败: {ex.Message}", ex);
            }
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
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                if (notifyIcon != null && Config.ShowTrayNotifications)
                {
                    notifyIcon.ShowBalloonTip(3000, title, message, icon);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"显示通知失败: {ex.Message}");
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
                Logger.Error($"释放托盘管理器资源失败: {ex.Message}", ex);
            }
        }
    }
} 