using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace TwainMiddleware
{
    /// <summary>
    /// WebSocket服务器
    /// </summary>
    public class WebSocketServer
    {
        private WebSocketSharp.Server.WebSocketServer server;
        private readonly TwainService twainService;
        private bool isStarted = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public WebSocketServer(TwainService twainService)
        {
            this.twainService = twainService ?? throw new ArgumentNullException(nameof(twainService));
        }

        /// <summary>
        /// 启动WebSocket服务器
        /// </summary>
        public void Start()
        {
            try
            {
                if (isStarted)
                    return;

                string url = $"ws://{Config.WebSocketHost}:{Config.WebSocketPort}";
                server = new WebSocketSharp.Server.WebSocketServer(url);

                // 添加WebSocket服务
                server.AddWebSocketService<TwainWebSocketBehavior>("/", () => new TwainWebSocketBehavior(twainService));

                // 启动服务器
                server.Start();
                isStarted = true;

                Logger.Info($"WebSocket服务器已启动: {url}");
            }
            catch (Exception ex)
            {
                Logger.Error($"启动WebSocket服务器失败: {ex.Message}", ex);
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
                    server.Stop();
                    server = null;
                    isStarted = false;
                    Logger.Info("WebSocket服务器已停止");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"停止WebSocket服务器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取服务器状态
        /// </summary>
        public bool IsRunning => isStarted && server != null && server.IsListening;
    }

    /// <summary>
    /// TWAIN WebSocket行为类
    /// </summary>
    public class TwainWebSocketBehavior : WebSocketBehavior
    {
        private readonly TwainService twainService;

        /// <summary>
        /// 构造函数
        /// </summary>
        public TwainWebSocketBehavior(TwainService twainService)
        {
            this.twainService = twainService ?? throw new ArgumentNullException(nameof(twainService));
        }

        /// <summary>
        /// 连接建立时的处理
        /// </summary>
        protected override void OnOpen()
        {
            Logger.Info($"WebSocket连接已建立: {Context.UserEndPoint}");
            
            // 发送欢迎消息
            var welcomeMessage = new
            {
                type = "connected",
                success = true,
                message = "连接已建立",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            Send(JsonConvert.SerializeObject(welcomeMessage));
        }

        /// <summary>
        /// 连接关闭时的处理
        /// </summary>
        protected override void OnClose(CloseEventArgs e)
        {
            Logger.Info($"WebSocket连接已关闭: {Context.UserEndPoint}, 代码: {e.Code}, 原因: {e.Reason}");
        }

        /// <summary>
        /// 接收到消息时的处理
        /// </summary>
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                Logger.Debug($"收到WebSocket消息: {e.Data}");

                var command = JsonConvert.DeserializeObject<WebSocketCommand>(e.Data);
                var response = ProcessCommand(command);

                Send(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                Logger.Error($"处理WebSocket消息失败: {ex.Message}", ex);

                var errorResponse = new WebSocketResponse
                {
                    Type = "error",
                    Success = false,
                    Message = ex.Message,
                    RequestId = "unknown",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };

                Send(JsonConvert.SerializeObject(errorResponse));
            }
        }

        /// <summary>
        /// 发生错误时的处理
        /// </summary>
        protected override void OnError(ErrorEventArgs e)
        {
            Logger.Error($"WebSocket错误: {e.Message}", e.Exception);
        }

        /// <summary>
        /// 处理WebSocket命令
        /// </summary>
        private WebSocketResponse ProcessCommand(WebSocketCommand command)
        {
            try
            {
                var response = new WebSocketResponse
                {
                    RequestId = command.RequestId,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };

                switch (command.Action?.ToLower())
                {
                    case "getscanners":
                        response = HandleGetScanners(command);
                        break;

                    case "scan":
                        response = HandleScan(command);
                        break;

                    case "ping":
                        response = HandlePing(command);
                        break;

                    default:
                        response.Type = "error";
                        response.Success = false;
                        response.Message = $"未知的命令: {command.Action}";
                        break;
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"处理命令失败: {ex.Message}", ex);
                
                return new WebSocketResponse
                {
                    Type = "error",
                    Success = false,
                    Message = ex.Message,
                    RequestId = command?.RequestId,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
            }
        }

        /// <summary>
        /// 处理获取扫描仪列表命令
        /// </summary>
        private WebSocketResponse HandleGetScanners(WebSocketCommand command)
        {
            try
            {
                var scanners = twainService.GetScanners();

                return new WebSocketResponse
                {
                    Type = "getScanners",
                    Success = true,
                    Data = scanners,
                    RequestId = command.RequestId,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
            }
            catch (Exception ex)
            {
                return new WebSocketResponse
                {
                    Type = "getScanners",
                    Success = false,
                    Message = ex.Message,
                    RequestId = command.RequestId,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
            }
        }

        /// <summary>
        /// 处理扫描命令
        /// </summary>
        private WebSocketResponse HandleScan(WebSocketCommand command)
        {
            try
            {
                // 解析扫描选项
                var scanOptions = new ScanOptions();
                if (command.Data != null)
                {
                    var dataJson = JsonConvert.SerializeObject(command.Data);
                    scanOptions = JsonConvert.DeserializeObject<ScanOptions>(dataJson);
                }

                // 应用默认值
                scanOptions.ScannerName = scanOptions.ScannerName ?? "默认扫描仪";
                scanOptions.Resolution = scanOptions.Resolution > 0 ? scanOptions.Resolution : Config.DefaultResolution;
                scanOptions.ColorMode = scanOptions.ColorMode ?? Config.DefaultColorMode;
                scanOptions.Format = scanOptions.Format ?? Config.DefaultFormat;

                // 执行扫描
                var result = twainService.Scan(scanOptions);

                return new WebSocketResponse
                {
                    Type = "scan",
                    Success = result.Success,
                    Message = result.Message,
                    Data = result,
                    RequestId = command.RequestId,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
            }
            catch (Exception ex)
            {
                return new WebSocketResponse
                {
                    Type = "scan",
                    Success = false,
                    Message = ex.Message,
                    RequestId = command.RequestId,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
            }
        }

        /// <summary>
        /// 处理心跳命令
        /// </summary>
        private WebSocketResponse HandlePing(WebSocketCommand command)
        {
            return new WebSocketResponse
            {
                Type = "pong",
                Success = true,
                Data = "pong",
                RequestId = command.RequestId,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
        }
    }

    /// <summary>
    /// WebSocket命令
    /// </summary>
    public class WebSocketCommand
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }

    /// <summary>
    /// WebSocket响应
    /// </summary>
    public class WebSocketResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }
} 