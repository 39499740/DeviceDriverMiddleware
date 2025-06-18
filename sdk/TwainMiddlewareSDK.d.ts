/**
 * TWAIN扫描仪中间件 JavaScript SDK TypeScript 定义文件
 */

declare class TwainMiddlewareSDK {
    /**
     * 构造函数
     * @param options 配置选项
     */
    constructor(options?: TwainSDKOptions);

    /**
     * 连接到中间件服务
     */
    connect(): Promise<void>;

    /**
     * 断开连接
     */
    disconnect(): void;

    /**
     * 获取可用扫描仪列表
     */
    getScanners(): Promise<string[]>;

    /**
     * 执行扫描操作
     * @param options 扫描参数
     */
    scan(options?: ScanOptions): Promise<ScanResponse>;

    /**
     * 发送心跳检测
     */
    ping(): Promise<PingResponse>;

    /**
     * 检查服务器健康状态
     */
    checkHealth(): Promise<boolean>;

    /**
     * 通过HTTP API获取扫描仪列表（备用方法）
     */
    getScannersHttp(): Promise<string[]>;

    /**
     * 获取连接状态
     */
    getConnectionState(): boolean;

    /**
     * 添加事件监听器
     * @param event 事件名称
     * @param callback 回调函数
     */
    on(event: SDKEvent, callback: EventCallback): void;

    /**
     * 移除事件监听器
     * @param event 事件名称
     * @param callback 回调函数
     */
    off(event: SDKEvent, callback: EventCallback): void;
}

/**
 * SDK配置选项
 */
interface TwainSDKOptions {
    /** 服务器地址 */
    host?: string;
    /** 服务器端口 */
    port?: number;
    /** 是否自动重连 */
    autoReconnect?: boolean;
    /** 重连间隔（毫秒） */
    reconnectInterval?: number;
    /** 最大重连次数 */
    maxReconnectAttempts?: number;
    /** 是否开启调试模式 */
    debug?: boolean;
}

/**
 * 扫描参数
 */
interface ScanOptions {
    /** 扫描仪名称 */
    scannerName?: string;
    /** 分辨率 (DPI) */
    resolution?: number;
    /** 颜色模式 */
    colorMode?: 'Color' | 'Gray' | 'BlackWhite';
    /** 图像格式 */
    format?: 'PNG' | 'JPEG' | 'TIFF' | 'BMP';
    /** 是否显示扫描仪界面 */
    showUI?: boolean;
    /** 亮度 (-1000 ~ 1000) */
    brightness?: number;
    /** 对比度 (-1000 ~ 1000) */
    contrast?: number;
    /** 自动旋转 */
    autoRotate?: boolean;
    /** 自动裁剪 */
    autoCrop?: boolean;
}

/**
 * 扫描响应
 */
interface ScanResponse {
    /** 操作类型 */
    type: string;
    /** 操作是否成功 */
    success: boolean;
    /** 响应消息 */
    message?: string;
    /** 响应数据 */
    data?: ScanResultData;
    /** 请求ID */
    requestId?: string;
    /** 时间戳 */
    timestamp: string;
}

/**
 * 扫描结果数据
 */
interface ScanResultData {
    /** 图像格式 */
    format: string;
    /** 图像宽度 */
    width: number;
    /** 图像高度 */
    height: number;
    /** Base64编码的图像数据 */
    imageData: string;
    /** 时间戳 */
    timestamp: string;
}

/**
 * 心跳响应
 */
interface PingResponse {
    /** 响应类型 */
    type: 'pong';
    /** 操作是否成功 */
    success: boolean;
    /** 响应数据 */
    data: string;
    /** 时间戳 */
    timestamp: string;
}

/**
 * SDK事件类型
 */
type SDKEvent = 
    | 'connected'
    | 'disconnected'
    | 'error'
    | 'scanStarted'
    | 'scanCompleted'
    | 'scannersUpdated';

/**
 * 事件回调函数类型
 */
type EventCallback = (data?: any) => void;

/**
 * 连接断开事件数据
 */
interface DisconnectedEventData {
    /** 关闭代码 */
    code: number;
    /** 关闭原因 */
    reason: string;
}

/**
 * WebSocket命令
 */
interface WebSocketCommand {
    /** 命令动作 */
    action: string;
    /** 命令数据 */
    data?: any;
    /** 请求ID */
    requestId?: string;
    /** 时间戳 */
    timestamp: string;
}

/**
 * WebSocket响应
 */
interface WebSocketResponse {
    /** 响应类型 */
    type: string;
    /** 操作是否成功 */
    success: boolean;
    /** 响应消息 */
    message?: string;
    /** 响应数据 */
    data?: any;
    /** 请求ID */
    requestId?: string;
    /** 时间戳 */
    timestamp: string;
}

export = TwainMiddlewareSDK;
export as namespace TwainMiddlewareSDK; 