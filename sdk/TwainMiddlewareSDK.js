/**
 * TWAIN扫描仪中间件 JavaScript SDK
 * 版本: 1.0.0
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
     * @returns {Promise<object>}
     */
    sendCommand(action, data = null) {
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
                    reject(new Error('请求超时'));
                }
            }, 10000); // 10秒超时

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
     * 检查TWAIN状态
     * @returns {Promise<object>}
     */
    async checkTwainStatus() {
        try {
            const response = await this.sendCommand('checktwainstatus');
            return response.Data || {
                IsTwainAvailable: false,
                TwainUnavailableReason: '未知错误'
            };
        } catch (error) {
            this.log('检查TWAIN状态失败:', error);
            throw error;
        }
    }

    /**
     * 获取可用扫描仪列表
     * @returns {Promise<Array>}
     */
    async getScanners() {
        try {
            const response = await this.sendCommand('getscanners');
            if (!response.Success && response.Message) {
                // 如果获取失败，抛出友好的错误信息
                throw new Error(response.Message);
            }
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
            const response = await this.sendCommand('scan', scanOptions);
            this.emit('scanCompleted', response);
            return response;
        } catch (error) {
            this.log('扫描失败:', error);
            this.emit('scanCompleted', { success: false, message: error.message });
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
                Duplex: options.duplex || 0, // 0=默认, 1=单面, 2=双面长边, 3=双面短边
                PaperSize: options.paperSize || '',
                StartPage: options.startPage || 1,
                EndPage: options.endPage || 0 // 0表示到最后一页
            };

            this.log('开始打印PDF:', { 
                printer: printOptions.PrinterName, 
                copies: printOptions.Copies,
                duplex: printOptions.Duplex,
                paperSize: printOptions.PaperSize,
                pageRange: `${printOptions.StartPage}-${printOptions.EndPage || '最后一页'}`
            });

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
                Duplex: options.duplex || 0, // 0=默认, 1=单面, 2=双面长边, 3=双面短边
                PaperSize: options.paperSize || '',
                StartPage: options.startPage || 1,
                EndPage: options.endPage || 0 // 0表示到最后一页
            };

            this.log('开始异步打印PDF:', { 
                printer: printOptions.PrinterName, 
                copies: printOptions.Copies,
                duplex: printOptions.Duplex,
                paperSize: printOptions.PaperSize,
                pageRange: `${printOptions.StartPage}-${printOptions.EndPage || '最后一页'}`
            });

            return new Promise((resolve, reject) => {
                const requestId = `req-${++this.requestId}`;
                
                // 设置消息监听器，处理打印过程中的各种响应
                const messageListener = (response) => {
                    if (response.Id === requestId) {
                        // 根据消息类型处理
                        if (response.Message === 'printProgress') {
                            // 进度更新
                            if (progressCallback) {
                                progressCallback(response.Data);
                            }
                            this.emit('printProgress', response.Data);
                        } else if (response.Message === 'printCompleted') {
                            // 打印完成
                            this.off('_internal_async_print', messageListener);
                            this.emit('printCompleted', response.Data);
                            resolve(response.Data);
                        } else if (response.Message && response.Message.startsWith('printFailed:')) {
                            // 打印失败
                            this.off('_internal_async_print', messageListener);
                            this.emit('printCompleted', { success: false, message: response.Message });
                            reject(new Error(response.Message || '打印失败'));
                        } else if (response.Message === 'printStarted') {
                            // 初始确认消息，不需要特殊处理
                            this.log('异步打印已启动，等待进度更新...');
                        }
                    }
                };

                // 添加内部监听器
                this.on('_internal_async_print', messageListener);

                // 覆盖handleMessage方法以处理打印相关消息
                const originalHandleMessage = this.handleMessage.bind(this);
                this.handleMessage = (data) => {
                    try {
                        const response = JSON.parse(data);
                        
                        // 检查是否是我们的异步打印响应
                        if (response.Id === requestId && 
                            (response.Message === 'printProgress' || 
                             response.Message === 'printCompleted' || 
                             response.Message === 'printStarted' ||
                             (response.Message && response.Message.startsWith('printFailed:')))) {
                            this.emit('_internal_async_print', response);
                            return;
                        }
                    } catch (error) {
                        // 如果解析失败，回退到原始处理
                    }
                    
                    // 调用原始处理方法
                    originalHandleMessage(data);
                };

                // 发送异步打印命令
                const command = {
                    Id: requestId,
                    Action: 'printpdfasync',
                    Data: printOptions
                };

                try {
                    this.ws.send(JSON.stringify(command));
                    this.log('发送异步打印命令:', command);
                    this.emit('printStarted', { printerName: printOptions.PrinterName });
                } catch (error) {
                    // 清理监听器
                    this.off('_internal_async_print', messageListener);
                    this.handleMessage = originalHandleMessage;
                    reject(error);
                }

                // 设置超时
                const timeoutId = setTimeout(() => {
                    // 清理监听器
                    this.off('_internal_async_print', messageListener);
                    this.handleMessage = originalHandleMessage;
                    reject(new Error('打印请求超时'));
                }, 300000); // 5分钟超时

                // 清理函数，在成功或失败时调用
                const cleanup = () => {
                    clearTimeout(timeoutId);
                    this.handleMessage = originalHandleMessage;
                };

                // 修改messageListener以包含清理逻辑
                const originalResolve = resolve;
                const originalReject = reject;
                
                resolve = (value) => {
                    cleanup();
                    originalResolve(value);
                };
                
                reject = (error) => {
                    cleanup();
                    originalReject(error);
                };
            });

        } catch (error) {
            this.log('异步打印PDF失败:', error);
            this.emit('printCompleted', { success: false, message: error.message });
            throw error;
        }
    }

    /**
     * 将File或Blob转换为Base64字符串
     * @param {File|Blob} file - 文件对象
     * @returns {Promise<string>}
     */
    fileToBase64(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                // 移除data:application/pdf;base64,前缀，只保留base64数据
                const base64 = reader.result.split(',')[1];
                resolve(base64);
            };
            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    }

    /**
     * 将ArrayBuffer转换为Base64字符串
     * @param {ArrayBuffer} buffer - 数组缓冲区
     * @returns {string}
     */
    arrayBufferToBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary);
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
                    callback.reject(new Error(response.Message || '操作失败'));
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
} 