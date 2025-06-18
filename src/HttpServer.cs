using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace TwainMiddleware
{
    /// <summary>
    /// HTTP服务器
    /// </summary>
    public class HttpServer
    {
        private HttpListener listener;
        private readonly TwainService twainService;
        private bool isStarted = false;
        private Thread listenerThread;

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpServer(TwainService twainService)
        {
            this.twainService = twainService ?? throw new ArgumentNullException(nameof(twainService));
        }

        /// <summary>
        /// 启动HTTP服务器
        /// </summary>
        public void Start()
        {
            try
            {
                if (isStarted)
                    return;

                listener = new HttpListener();
                string url = $"http://{Config.HttpHost}:{Config.HttpPort}/";
                listener.Prefixes.Add(url);

                listener.Start();
                isStarted = true;

                // 启动监听线程
                listenerThread = new Thread(ListenerWorker)
                {
                    IsBackground = true,
                    Name = "HttpServerListener"
                };
                listenerThread.Start();

                Logger.Info($"HTTP服务器已启动: {url}");
            }
            catch (Exception ex)
            {
                Logger.Error($"启动HTTP服务器失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 停止HTTP服务器
        /// </summary>
        public void Stop()
        {
            try
            {
                if (listener != null && isStarted)
                {
                    isStarted = false;
                    listener.Stop();
                    listener.Close();
                    listener = null;

                    listenerThread?.Join(5000);
                    Logger.Info("HTTP服务器已停止");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"停止HTTP服务器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 监听器工作线程
        /// </summary>
        private void ListenerWorker()
        {
            while (isStarted && listener != null && listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, context);
                }
                catch (HttpListenerException ex)
                {
                    if (isStarted) // 只有在服务器还在运行时才记录错误
                    {
                        Logger.Error($"HTTP监听器错误: {ex.Message}", ex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"HTTP服务器错误: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        private void ProcessRequest(object state)
        {
            var context = (HttpListenerContext)state;
            
            try
            {
                var request = context.Request;
                var response = context.Response;

                Logger.Debug($"收到HTTP请求: {request.HttpMethod} {request.Url.AbsolutePath}");

                // 设置CORS头
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                // 处理OPTIONS请求（预检请求）
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                // 路由处理
                var path = request.Url.AbsolutePath.ToLower();
                switch (path)
                {
                    case "/":
                    case "/test":
                        HandleTestPage(response);
                        break;

                    case "/api/health":
                        WriteJsonResponse(response, new { status = "ok", timestamp = DateTime.Now });
                        break;

                    default:
                        WriteJsonResponse(response, new { error = "页面未找到" }, 404);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"处理HTTP请求失败: {ex.Message}", ex);
                
                try
                {
                    WriteJsonResponse(context.Response, new { error = ex.Message }, 500);
                }
                catch
                {
                    // 忽略响应写入错误
                }
            }
        }

        /// <summary>
        /// 处理测试页面请求
        /// </summary>
        private void HandleTestPage(HttpListenerResponse response)
        {
            string html = $@"<!DOCTYPE html><html><head><title>TWAIN测试页面</title></head><body><h1>TWAIN扫描仪中间件测试页面</h1><p>端口: {Config.WebSocketPort}</p></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = 200;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        /// <summary>
        /// 写入JSON响应
        /// </summary>
        private void WriteJsonResponse(HttpListenerResponse response, object data, int statusCode = 200)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = statusCode;

                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"写入JSON响应失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取服务器状态
        /// </summary>
        public bool IsRunning => isStarted && listener != null && listener.IsListening;
    }
} 