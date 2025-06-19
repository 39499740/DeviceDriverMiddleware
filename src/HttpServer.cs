using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TwainMiddleware
{
    /// <summary>
    /// 简单的HTTP服务器，用于提供测试页面
    /// </summary>
    public class HttpServer : IDisposable
    {
        private HttpListener listener;
        private Thread listenerThread;
        private bool isRunning = false;
        private readonly int port;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="port">监听端口</param>
        public HttpServer(int port = 8080)
        {
            this.port = port;
        }

        /// <summary>
        /// 启动HTTP服务器
        /// </summary>
        public void Start()
        {
            if (isRunning)
                return;

            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(string.Format("http://localhost:{0}/", port));
                listener.Start();

                isRunning = true;
                listenerThread = new Thread(HandleRequests);
                listenerThread.Start();

                Logger.Info("HTTP服务器已启动，端口: " + port.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error("启动HTTP服务器失败: " + ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// 停止HTTP服务器
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            try
            {
                isRunning = false;
                
                if (listener != null)
                {
                    listener.Stop();
                    listener.Close();
                }

                if (listenerThread != null && listenerThread.IsAlive)
                {
                    listenerThread.Join(1000);
                }

                Logger.Info("HTTP服务器已停止");
            }
            catch (Exception ex)
            {
                Logger.Error("停止HTTP服务器失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 获取服务器URL
        /// </summary>
        /// <returns></returns>
        public string GetServerUrl()
        {
            return "http://localhost:" + port.ToString();
        }

        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        private void HandleRequests()
        {
            while (isRunning && listener != null && listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // 服务器停止时会抛出异常，忽略
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("处理HTTP请求失败: " + ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// 处理单个请求
        /// </summary>
        /// <param name="context"></param>
        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                Logger.Debug("HTTP请求: " + request.HttpMethod + " " + request.Url.ToString());

                // 设置响应头
                string path = request.Url.AbsolutePath;
                if (path.EndsWith(".js"))
                {
                    response.ContentType = "application/javascript; charset=utf-8";
                }
                else if (path.EndsWith(".d.ts"))
                {
                    response.ContentType = "text/plain; charset=utf-8";
                }
                else if (path.EndsWith(".css"))
                {
                    response.ContentType = "text/css; charset=utf-8";
                }
                else
                {
                    response.ContentType = "text/html; charset=utf-8";
                }
                response.StatusCode = 200;

                // 获取响应内容
                string responseContent = GetResponseContent(path);
                byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

                // 发送响应
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.Error("处理HTTP请求失败: " + ex.Message, ex);
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        /// <summary>
        /// 获取响应内容
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string GetResponseContent(string path)
        {
            try
            {
                // 根据路径返回不同内容
                if (path == "/" || path == "/test.html" || path == "/web/test.html")
                {
                    return GetTestPageContent();
                }
                else if (path.StartsWith("/sdk/") || path.StartsWith("../sdk/"))
                {
                    // 尝试读取SDK文件
                    string fileName = System.IO.Path.GetFileName(path);
                    return GetSDKFile(fileName);
                }
                else
                {
                    // 默认返回测试页面
                    return GetTestPageContent();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("获取响应内容失败: " + ex.Message, ex);
                return GetTestPageContent();
            }
        }
        
        /// <summary>
        /// 获取SDK文件内容
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string GetSDKFile(string fileName)
        {
            try
            {
                // 对于分发版本，直接使用内嵌SDK，确保功能完整且不依赖外部文件
                if (fileName.Equals("TwainMiddlewareSDK.js", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Debug("使用内嵌SDK文件: " + fileName);
                    return GetEmbeddedSDKContent();
                }
                
                // TypeScript定义文件返回空内容
                if (fileName.Equals("TwainMiddlewareSDK.d.ts", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Debug("TypeScript定义文件请求: " + fileName);
                    return "// TypeScript定义文件不在分发包中提供";
                }
                
                Logger.Warning("未知的SDK文件: " + fileName);
                return "// 未知的SDK文件: " + fileName;
            }
            catch (Exception ex)
            {
                Logger.Error("读取SDK文件失败: " + ex.Message, ex);
                
                // 发生异常时，如果是主SDK文件，返回内嵌版本
                if (fileName.Equals("TwainMiddlewareSDK.js", StringComparison.OrdinalIgnoreCase))
                {
                    return GetEmbeddedSDKContent();
                }
                
                return "// 读取SDK文件失败: " + ex.Message;
            }
        }

        /// <summary>
        /// 获取内嵌的SDK内容
        /// </summary>
        /// <returns></returns>
        private string GetEmbeddedSDKContent()
        {
            return @"/**
 * TWAIN扫描仪中间件 JavaScript SDK
 * 版本: 1.2.0
 * 提供与TWAIN扫描仪中间件的WebSocket通信接口
 */
class TwainMiddlewareSDK {
    constructor(options = {}) {
        this.options = {
            host: options.host || 'localhost',
            port: options.port || 45677,
            autoReconnect: options.autoReconnect || true,
            reconnectInterval: options.reconnectInterval || 3000,
            maxReconnectAttempts: options.maxReconnectAttempts || 3,
            debug: options.debug || false
        };

        this.ws = null;
        this.isConnected = false;
        this.reconnectAttempts = 0;
        this.requestCallbacks = new Map();
        this.eventListeners = new Map();
        this.requestId = 0;

        // 初始化事件监听器映射
        this.eventListeners.set('connected', []);
        this.eventListeners.set('disconnected', []);
        this.eventListeners.set('error', []);
        this.eventListeners.set('scanStarted', []);
        this.eventListeners.set('scanCompleted', []);
        this.eventListeners.set('scannersUpdated', []);
        this.eventListeners.set('printersUpdated', []);
        this.eventListeners.set('printStarted', []);
        this.eventListeners.set('printProgress', []);
        this.eventListeners.set('printCompleted', []);
    }

    /**
     * 连接到中间件服务
     * @returns {Promise<void>}
     */
    connect() {
        return new Promise((resolve, reject) => {
            const wsUrl = `ws://${this.options.host}:${this.options.port}/twain`;
            this.log('正在连接到:', wsUrl);

            try {
                this.ws = new WebSocket(wsUrl);

                this.ws.onopen = () => {
                    this.isConnected = true;
                    this.reconnectAttempts = 0;
                    this.log('WebSocket连接已建立');
                    this.emit('connected');
                    resolve();
                };

                this.ws.onmessage = (event) => {
                    this.handleMessage(event.data);
                };

                this.ws.onclose = (event) => {
                    this.isConnected = false;
                    this.log('WebSocket连接已关闭:', event.code, event.reason);
                    this.emit('disconnected', { code: event.code, reason: event.reason });

                    if (this.options.autoReconnect && this.reconnectAttempts < this.options.maxReconnectAttempts) {
                        this.attemptReconnect();
                    }
                };

                this.ws.onerror = (error) => {
                    this.log('WebSocket错误:', error);
                    this.emit('error', error);
                    if (!this.isConnected) {
                        reject(error);
                    }
                };

            } catch (error) {
                this.log('连接失败:', error);
                reject(error);
            }
        });
    }

    /**
     * 断开连接
     */
    disconnect() {
        if (this.ws && this.isConnected) {
            this.ws.close();
        }
    }

    /**
     * 尝试重新连接
     */
    attemptReconnect() {
        this.reconnectAttempts++;
        this.log(`尝试重新连接 (${this.reconnectAttempts}/${this.options.maxReconnectAttempts})`);

        setTimeout(() => {
            this.connect().catch(() => {
                // 重连失败，自动触发下一次重连或放弃
            });
        }, this.options.reconnectInterval);
    }

    /**
     * 发送命令到中间件
     * @param {string} action - 命令动作
     * @param {object} data - 命令数据
     * @param {number} timeout - 超时时间（毫秒），默认10秒
     * @returns {Promise<object>}
     */
    sendCommand(action, data = null, timeout = 10000) {
        return new Promise((resolve, reject) => {
            if (!this.isConnected) {
                reject(new Error('WebSocket未连接'));
                return;
            }

            const requestId = `req-${++this.requestId}`;
            const command = {
                Id: requestId,
                Action: action,
                Data: data
            };

            // 存储回调函数
            this.requestCallbacks.set(requestId, { resolve, reject });

            // 设置超时
            setTimeout(() => {
                if (this.requestCallbacks.has(requestId)) {
                    this.requestCallbacks.delete(requestId);
                    reject(new Error('请求超时，操作可能仍在进行中'));
                }
            }, timeout);

            // 发送命令
            try {
                this.ws.send(JSON.stringify(command));
                this.log('发送命令:', command);
            } catch (error) {
                this.requestCallbacks.delete(requestId);
                reject(error);
            }
        });
    }

    /**
     * 获取可用扫描仪列表
     * @returns {Promise<Array>}
     */
    async getScanners() {
        try {
            const response = await this.sendCommand('getscanners');
            return response.Data || [];
        } catch (error) {
            this.log('获取扫描仪列表失败:', error);
            throw error;
        }
    }

    /**
     * 获取系统中的真实打印机列表
     * @returns {Promise<Array>}
     */
    async getPrinters() {
        try {
            const response = await this.sendCommand('getprinters');
            this.emit('printersUpdated', response.Data);
            return response.Data || [];
        } catch (error) {
            this.log('获取打印机列表失败:', error);
            throw error;
        }
    }

    /**
     * 打印PDF文件
     * @param {object} options - 打印参数
     * @returns {Promise<object>}
     */
    async printPdf(options = {}) {
        try {
            // 验证必需参数
            if (!options.printerName) {
                throw new Error('打印机名称不能为空');
            }

            if (!options.pdfData) {
                throw new Error('PDF数据不能为空');
            }

            // 如果pdfData是File对象或Blob对象，转换为base64
            let pdfDataBytes;
            if (options.pdfData instanceof File || options.pdfData instanceof Blob) {
                pdfDataBytes = await this.fileToBase64(options.pdfData);
            } else if (typeof options.pdfData === 'string') {
                // 假设是base64字符串
                pdfDataBytes = options.pdfData;
            } else if (options.pdfData instanceof ArrayBuffer) {
                // 转换ArrayBuffer为base64
                pdfDataBytes = this.arrayBufferToBase64(options.pdfData);
            } else {
                throw new Error('不支持的PDF数据格式');
            }

            const printOptions = {
                PrinterName: options.printerName,
                PdfData: pdfDataBytes,
                Copies: options.copies || 1,
                Duplex: options.duplex || 0,
                PaperSize: options.paperSize || '',
                StartPage: options.startPage || 1,
                EndPage: options.endPage || 0
            };

            this.emit('printStarted', { printerName: printOptions.PrinterName });
            const response = await this.sendCommand('printpdf', printOptions);
            this.emit('printCompleted', response);
            return response;
        } catch (error) {
            this.log('打印PDF失败:', error);
            this.emit('printCompleted', { success: false, message: error.message });
            throw error;
        }
    }

    /**
     * 异步打印PDF文件（带进度回调）
     * @param {object} options - 打印参数
     * @param {function} progressCallback - 进度回调函数
     * @returns {Promise<object>}
     */
    async printPdfAsync(options = {}, progressCallback = null) {
        try {
            // 验证必需参数
            if (!options.printerName) {
                throw new Error('打印机名称不能为空');
            }

            if (!options.pdfData) {
                throw new Error('PDF数据不能为空');
            }

            // 如果pdfData是File对象或Blob对象，转换为base64
            let pdfDataBytes;
            if (options.pdfData instanceof File || options.pdfData instanceof Blob) {
                pdfDataBytes = await this.fileToBase64(options.pdfData);
            } else if (typeof options.pdfData === 'string') {
                pdfDataBytes = options.pdfData;
            } else if (options.pdfData instanceof ArrayBuffer) {
                pdfDataBytes = this.arrayBufferToBase64(options.pdfData);
            } else {
                throw new Error('不支持的PDF数据格式');
            }

            const printOptions = {
                PrinterName: options.printerName,
                PdfData: pdfDataBytes,
                Copies: options.copies || 1,
                Duplex: options.duplex || 0,
                PaperSize: options.paperSize || '',
                StartPage: options.startPage || 1,
                EndPage: options.endPage || 0
            };

            return new Promise((resolve, reject) => {
                let isResolved = false;
                
                const messageListener = (response) => {
                    if (!response || !response.Message) return;
                    
                    try {
                        const message = JSON.parse(response.Message);
                        
                        if (message.Type === 'printProgress' && progressCallback) {
                            progressCallback({
                                Status: message.Status,
                                Percentage: message.Percentage,
                                CurrentPage: message.CurrentPage,
                                TotalPages: message.TotalPages,
                                Message: message.Message
                            });
                        } else if (message.Type === 'printCompleted') {
                            cleanup();
                            if (!isResolved) {
                                isResolved = true;
                                resolve({
                                    Success: message.Success || true,
                                    Message: message.Message || '打印完成',
                                    TotalPages: message.TotalPages,
                                    PrintedPages: message.PrintedPages
                                });
                            }
                        } else if (message.Type === 'printFailed') {
                            cleanup();
                            if (!isResolved) {
                                isResolved = true;
                                reject(new Error(message.Message || '打印失败'));
                            }
                        }
                    } catch (e) {
                        // 忽略解析错误
                    }
                };

                const cleanup = () => {
                    this.off('printProgress', messageListener);
                    this.off('printCompleted', messageListener);
                    this.off('printFailed', messageListener);
                };

                this.on('printProgress', messageListener);
                this.on('printCompleted', messageListener);
                this.on('printFailed', messageListener);

                this.sendCommand('printpdfasync', printOptions).then((response) => {
                    if (response.Success === false) {
                        cleanup();
                        if (!isResolved) {
                            isResolved = true;
                            reject(new Error(response.Message || '启动异步打印失败'));
                        }
                    }
                }).catch((error) => {
                    cleanup();
                    if (!isResolved) {
                        isResolved = true;
                        reject(error);
                    }
                });

                setTimeout(() => {
                    if (!isResolved) {
                        cleanup();
                        isResolved = true;
                        reject(new Error('打印操作超时'));
                    }
                }, 300000);
            });
        } catch (error) {
            this.log('异步打印PDF失败:', error);
            throw error;
        }
    }

    /**
     * 将File对象转换为Base64字符串
     * @param {File} file - 文件对象
     * @returns {Promise<string>}
     */
    fileToBase64(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.readAsDataURL(file);
            reader.onload = () => {
                const base64 = reader.result.split(',')[1];
                resolve(base64);
            };
            reader.onerror = error => reject(error);
        });
    }

    /**
     * 将ArrayBuffer转换为Base64字符串
     * @param {ArrayBuffer} buffer - 数组缓冲区
     * @returns {string}
     */
    arrayBufferToBase64(buffer) {
        let binary = '';
        const bytes = new Uint8Array(buffer);
        const len = bytes.byteLength;
        for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    }

    /**
     * 执行扫描
     * @param {object} options - 扫描参数
     * @returns {Promise<object>}
     */
    async scan(options = {}) {
        const scanOptions = {
            ScannerName: options.scannerName || '默认扫描仪',
            Resolution: options.resolution || 300,
            ColorMode: options.colorMode || 'Color',
            Format: options.format || 'PNG',
            ShowUI: options.showUI || false,
            Brightness: options.brightness || 0,
            Contrast: options.contrast || 0,
            AutoRotate: options.autoRotate || false,
            AutoCrop: options.autoCrop || false
        };

        try {
            this.emit('scanStarted', { scannerName: scanOptions.ScannerName });
            // 扫描操作使用更长的超时时间（60秒），以应对扫描仪热机时间
            const response = await this.sendCommand('scan', scanOptions, 60000);
            this.emit('scanCompleted', response);
            return response;
        } catch (error) {
            this.log('扫描失败:', error);
            this.emit('scanCompleted', { success: false, message: error.message || '扫描操作失败' });
            throw error;
        }
    }

    /**
     * 发送心跳检测
     * @returns {Promise<object>}
     */
    async ping() {
        try {
            const response = await this.sendCommand('ping');
            return response;
        } catch (error) {
            this.log('心跳检测失败:', error);
            throw error;
        }
    }

    /**
     * 处理接收到的消息
     * @param {string} data - 消息数据
     */
    handleMessage(data) {
        try {
            const response = JSON.parse(data);
            this.log('收到响应:', response);

            // 处理有请求ID的响应
            if (response.Id && this.requestCallbacks.has(response.Id)) {
                const callback = this.requestCallbacks.get(response.Id);
                this.requestCallbacks.delete(response.Id);

                if (response.Success) {
                    callback.resolve(response);
                } else {
                    const errorMessage = response.Message || response.message || '操作失败，请检查扫描仪状态';
                    callback.reject(new Error(errorMessage));
                }
                return;
            }

            // 处理不同类型的响应
            switch (response.type) {
                case 'scan_started':
                    this.emit('scanStarted', response);
                    break;
                case 'scan_completed':
                    this.emit('scanCompleted', response);
                    break;
                case 'scanners_list':
                    this.emit('scannersUpdated', response.data);
                    break;
                case 'printers_list':
                    this.emit('printersUpdated', response.data);
                    break;
                case 'print_started':
                    this.emit('printStarted', response);
                    break;
                case 'print_progress':
                    this.emit('printProgress', response);
                    break;
                case 'print_completed':
                    this.emit('printCompleted', response);
                    break;
                case 'error':
                    this.emit('error', new Error(response.message));
                    break;
                case 'pong':
                    // 心跳响应，通常不需要特殊处理
                    break;
                default:
                    this.log('未知响应类型:', response.type);
            }

        } catch (error) {
            this.log('解析消息失败:', error, data);
            this.emit('error', error);
        }
    }

    /**
     * 添加事件监听器
     * @param {string} event - 事件名称
     * @param {function} callback - 回调函数
     */
    on(event, callback) {
        if (!this.eventListeners.has(event)) {
            this.eventListeners.set(event, []);
        }
        this.eventListeners.get(event).push(callback);
    }

    /**
     * 移除事件监听器
     * @param {string} event - 事件名称
     * @param {function} callback - 回调函数
     */
    off(event, callback) {
        if (!this.eventListeners.has(event)) {
            return;
        }
        const listeners = this.eventListeners.get(event);
        const index = listeners.indexOf(callback);
        if (index > -1) {
            listeners.splice(index, 1);
        }
    }

    /**
     * 触发事件
     * @param {string} event - 事件名称
     * @param {any} data - 事件数据
     */
    emit(event, data) {
        if (!this.eventListeners.has(event)) {
            return;
        }
        const listeners = this.eventListeners.get(event);
        listeners.forEach(callback => {
            try {
                callback(data);
            } catch (error) {
                this.log('事件回调执行失败:', error);
            }
        });
    }

    /**
     * 日志输出
     * @param {...any} args - 日志参数
     */
    log(...args) {
        if (this.options.debug) {
            console.log('[TwainMiddlewareSDK]', ...args);
        }
    }

    /**
     * 获取连接状态
     * @returns {boolean}
     */
    getConnectionState() {
        return this.isConnected;
    }

    /**
     * 获取服务器健康状态
     * @returns {Promise<boolean>}
     */
    async checkHealth() {
        try {
            await this.ping();
            return true;
        } catch (error) {
            this.log('健康检查失败:', error);
            return false;
        }
    }

    /**
     * 通过HTTP API获取扫描仪列表（备用方法）
     * @returns {Promise<Array>}
     */
    async getScannersHttp() {
        try {
            const response = await fetch(`http://${this.options.host}:${this.options.port}/scanners`);
            const data = await response.json();
            return data.scanners || [];
        } catch (error) {
            this.log('HTTP获取扫描仪列表失败:', error);
            throw error;
        }
    }

    /**
     * 获取SDK版本信息
     * @returns {string}
     */
    getVersion() {
        return '1.0.0';
    }
}

// 导出SDK类
if (typeof module !== 'undefined' && module.exports) {
    module.exports = TwainMiddlewareSDK;
} else if (typeof window !== 'undefined') {
    window.TwainMiddlewareSDK = TwainMiddlewareSDK;
}";
        }

        /// <summary>
        /// 获取测试页面内容
        /// </summary>
        /// <returns></returns>
        private string GetTestPageContent()
        {
            try
            {
                // 获取应用程序目录
                string appDir = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                
                // 尝试多个可能的测试页面路径
                string[] possiblePaths = {
                    System.IO.Path.Combine(appDir, "..", "web", "test.html"),
                    System.IO.Path.Combine(appDir, "web", "test.html"),
                    System.IO.Path.Combine(appDir, "..", "..", "web", "test.html"),
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "web", "test.html")
                };
                
                foreach (string path in possiblePaths)
                {
                    string fullPath = System.IO.Path.GetFullPath(path);
                    if (System.IO.File.Exists(fullPath))
                    {
                        string content = System.IO.File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                        Logger.Info("读取测试页面: " + fullPath);
                        return content;
                    }
                }
                
                // 如果找不到文件，返回备用页面
                Logger.Warning("未找到web/test.html文件，使用备用页面");
                return GetFallbackTestPage();
            }
            catch (Exception ex)
            {
                Logger.Error("读取测试页面失败: " + ex.Message, ex);
                return GetFallbackTestPage();
            }
        }
        
        /// <summary>
        /// 获取备用测试页面
        /// </summary>
        /// <returns></returns>
        private string GetFallbackTestPage()
        {
            return "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>TWAIN扫描仪中间件</title></head>" +
                   "<body><h1>TWAIN扫描仪中间件测试页面</h1>" +
                   "<p>原始测试页面未找到，请确保web/test.html文件存在。</p>" +
                   "<p>WebSocket端口: " + Config.WebSocketPort.ToString() + "</p>" +
                   "<p>HTTP端口: " + port.ToString() + "</p></body></html>";
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
} 