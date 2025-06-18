using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;

namespace TwainMiddleware
{
    /// <summary>
    /// WebSocket服务器包装类
    /// </summary>
    public class WebSocketServerWrapper
    {
        private WebSocketSharp.Server.WebSocketServer server;
        private bool isStarted = false;
        private TwainService twainService;

        /// <summary>
        /// 启动WebSocket服务器
        /// </summary>
        public void Start()
        {
            try
            {
                if (isStarted)
                    return;

                // 创建TWAIN服务
                twainService = new TwainService();
                twainService.Initialize();

                string url = "ws://" + Config.WebSocketHost + ":" + Config.WebSocketPort;
                server = new WebSocketSharp.Server.WebSocketServer(Config.WebSocketPort);

                // 添加WebSocket服务
                server.AddWebSocketService<TwainWebSocketService>("/twain", () => new TwainWebSocketService(twainService));

                server.Start();
                isStarted = true;

                Logger.Info("WebSocket服务器已启动: " + url);
            }
            catch (Exception ex)
            {
                Logger.Error("启动WebSocket服务器失败: " + ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// 停止WebSocket服务器
        /// </summary>
        public void Stop()
        {
            try
            {
                if (server != null && isStarted)
                {
                    isStarted = false;
                    server.Stop();
                    server = null;

                    if (twainService != null)
                    {
                        twainService.Dispose();
                        twainService = null;
                    }

                    Logger.Info("WebSocket服务器已停止");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("停止WebSocket服务器失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 获取服务器运行状态
        /// </summary>
        public bool IsRunning
        {
            get { return isStarted && server != null; }
        }
    }

    /// <summary>
    /// TWAIN WebSocket服务类
    /// </summary>
    public class TwainWebSocketService : WebSocketBehavior
    {
        private readonly TwainService twainService;

        public TwainWebSocketService(TwainService twainService)
        {
            this.twainService = twainService;
        }

        protected override void OnOpen()
        {
            Logger.Info("WebSocket连接已建立: " + Context.UserEndPoint);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Logger.Info("WebSocket连接已关闭: " + Context.UserEndPoint + ", 代码: " + e.Code + ", 原因: " + e.Reason);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                Logger.Debug("收到WebSocket消息: " + e.Data);

                var command = JsonConvert.DeserializeObject<WebSocketCommand>(e.Data);
                var response = ProcessCommand(command);

                string responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
                Send(responseJson);
            }
            catch (Exception ex)
            {
                Logger.Error("处理WebSocket消息失败: " + ex.Message, ex);
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Logger.Error("WebSocket错误: " + e.Message, e.Exception);
        }

        /// <summary>
        /// 处理WebSocket命令
        /// </summary>
        private WebSocketResponse ProcessCommand(WebSocketCommand command)
        {
            var response = new WebSocketResponse
            {
                Id = command.Id,
                Success = false
            };

            try
            {
                switch (command.Action != null ? command.Action.ToLower() : "")
                {
                    case "ping":
                        response.Success = true;
                        response.Data = "pong";
                        break;

                    case "getscanners":
                        response.Success = true;
                        response.Data = twainService.GetScanners();
                        break;

                    case "scan":
                        var options = JsonConvert.DeserializeObject<ScanOptions>(command.Data != null ? command.Data.ToString() : "{}");
                        var result = twainService.Scan(options);
                        response.Success = result.Success;
                        response.Data = result;
                        response.Message = result.Message;
                        break;

                    default:
                        response.Message = "未知的命令: " + command.Action;
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("处理命令失败: " + ex.Message, ex);
                response.Success = false;
                response.Message = ex.Message;
            }

            return response;
        }
    }

    /// <summary>
    /// WebSocket命令
    /// </summary>
    public class WebSocketCommand
    {
        public string Id { get; set; }
        public string Action { get; set; }
        public object Data { get; set; }
    }

    /// <summary>
    /// WebSocket响应
    /// </summary>
    public class WebSocketResponse
    {
        public string Id { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
} 
