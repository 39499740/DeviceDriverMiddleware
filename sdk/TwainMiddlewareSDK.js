/**
 * TWAIN扫描仪中间件 JavaScript SDK
 * 版本: 1.0.0
 * 提供与TWAIN扫描仪中间件的WebSocket通信接口
 */
class TwainMiddlewareSDK {
    constructor(options = {}) {
        this.options = {
            host: options.host || 'localhost',
            port: options.port || 5000,
            autoReconnect: options.autoReconnect !== false,
            reconnectInterval: options.reconnectInterval || 3000,
            maxReconnectAttempts: options.maxReconnectAttempts || 5,
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
    }

    /**
     * 连接到中间件服务
     * @returns {Promise<void>}
     */
    connect() {
        return new Promise((resolve, reject) => {
            const wsUrl = `ws://${this.options.host}:${this.options.port}/scan`;
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
                action,
                data,
                requestId,
                timestamp: new Date().toISOString()
            };

            // 存储回调函数
            this.requestCallbacks.set(requestId, { resolve, reject });

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
            const response = await this.sendCommand('get_scanners');
            return response.data || [];
        } catch (error) {
            this.log('获取扫描仪列表失败:', error);
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
            scannerName: options.scannerName || '默认扫描仪',
            resolution: options.resolution || 300,
            colorMode: options.colorMode || 'Color',
            format: options.format || 'PNG',
            showUI: options.showUI || false,
            brightness: options.brightness || 0,
            contrast: options.contrast || 0,
            autoRotate: options.autoRotate || false,
            autoCrop: options.autoCrop || false,
            ...options
        };

        try {
            const response = await this.sendCommand('scan', scanOptions);
            return response;
        } catch (error) {
            this.log('扫描失败:', error);
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
            if (response.requestId && this.requestCallbacks.has(response.requestId)) {
                const callback = this.requestCallbacks.get(response.requestId);
                this.requestCallbacks.delete(response.requestId);

                if (response.success) {
                    callback.resolve(response);
                } else {
                    callback.reject(new Error(response.message || '操作失败'));
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
            const response = await fetch(`http://${this.options.host}:${this.options.port}/health`);
            return response.ok;
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
}

// 导出SDK类
if (typeof module !== 'undefined' && module.exports) {
    module.exports = TwainMiddlewareSDK;
} else if (typeof window !== 'undefined') {
    window.TwainMiddlewareSDK = TwainMiddlewareSDK;
} 