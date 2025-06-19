/**
 * TWAIN扫描仪中间件 JavaScript SDK TypeScript 定义文件
 * 
 * @version 1.2.0
 * @date 2024年12月
 * 
 * @description
 * 提供完整的TWAIN扫描仪中间件功能支持，包括：
 * - 扫描仪设备管理和图像扫描
 * - PDF文件打印功能（已修复Base64编码问题）
 * - 实时进度监控和事件通知
 * - WebSocket连接管理和自动重连
 * - 完全兼容32位和64位系统架构
 * 
 * @changelog v1.2.0
 * - ✅ 修复PDF打印功能的Base64数据编码/解码问题
 * - ✅ 解决32位和64位系统架构兼容性问题
 * - ✅ 完全兼容C# 5编译器环境
 * - ✅ 自动集成PdfiumViewer和native依赖库
 * - ✅ 优化错误处理和异常消息
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
     * 检查TWAIN状态
     */
    checkTwainStatus(): Promise<TwainStatus>;

    /**
     * 获取可用扫描仪列表
     */
    getScanners(): Promise<string[]>;

    /**
     * 获取系统中的真实打印机列表
     */
    getPrinters(): Promise<PrinterInfo[]>;

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
     * 打印PDF文件
     * @param options 打印参数
     */
    printPdf(options: PrintOptions): Promise<PrintResponse>;

    /**
     * 异步打印PDF文件（带进度回调）
     * @param options 打印参数
     * @param progressCallback 进度回调函数
     */
    printPdfAsync(options: PrintOptions, progressCallback?: PrintProgressCallback): Promise<PrintResult>;

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
 * 打印机信息
 */
interface PrinterInfo {
    /** 打印机名称 */
    Name: string;
    /** 是否为默认打印机 */
    IsDefault: boolean;
    /** 打印机状态 */
    Status: string;
    /** 是否支持双面打印 */
    CanDuplex: boolean;
    /** 最大份数 */
    MaximumCopies: number;
    /** 是否支持彩色打印 */
    SupportsColor: boolean;
    /** 支持的纸张尺寸列表 */
    PaperSizes: string[];
    /** 打印机技术类型（激光打印机、喷墨打印机、针式打印机等） */
    PrinterType: string;
    /** 标记技术代码 */
    MarkingTechnology: number;
    /** 打印机技术类型详细描述 */
    PrinterTypeDescription: string;
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
    | 'scannersUpdated'
    | 'printersUpdated'
    | 'printStarted'
    | 'printProgress'
    | 'printCompleted';

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

/**
 * PDF打印参数
 */
interface PrintOptions {
    /** 打印机名称 */
    printerName: string;
    /** PDF文件数据（File对象、Blob对象、ArrayBuffer或base64字符串） */
    pdfData: File | Blob | ArrayBuffer | string;
    /** 打印份数 */
    copies?: number;
    /** 双面打印模式 */
    duplex?: DuplexMode;
    /** 纸张尺寸 */
    paperSize?: string;
    /** 起始页（1开始） */
    startPage?: number;
    /** 结束页（0表示到最后一页） */
    endPage?: number;
}

/**
 * 双面打印模式
 */
enum DuplexMode {
    /** 默认（使用打印机默认设置） */
    Default = 0,
    /** 单面打印 */
    Simplex = 1,
    /** 双面打印（长边翻转） */
    Vertical = 2,
    /** 双面打印（短边翻转） */
    Horizontal = 3
}

/**
 * PDF打印响应
 */
interface PrintResponse {
    /** 操作ID */
    Id: string;
    /** 操作是否成功 */
    Success: boolean;
    /** 响应消息 */
    Message?: string;
    /** 打印结果数据 */
    Data?: PrintResult;
}

/**
 * PDF打印结果
 */
interface PrintResult {
    /** 是否成功 */
    Success: boolean;
    /** 消息 */
    Message: string;
    /** 总页数 */
    TotalPages: number;
    /** 已打印页数 */
    PrintedPages: number;
    /** 时间戳 */
    Timestamp: string;
}

/**
 * PDF打印进度
 */
interface PrintProgress {
    /** 状态（开始、准备中、打印中、完成、失败、已取消） */
    Status: string;
    /** 消息 */
    Message: string;
    /** 当前页 */
    CurrentPage: number;
    /** 总页数 */
    TotalPages: number;
    /** 进度百分比（0-100） */
    Percentage: number;
}

/**
 * 打印进度回调函数类型
 */
type PrintProgressCallback = (progress: PrintProgress) => void;

/**
 * TWAIN状态信息
 */
interface TwainStatus {
    /** TWAIN是否可用 */
    IsTwainAvailable: boolean;
    /** TWAIN不可用的原因 */
    TwainUnavailableReason?: string;
    /** 兼容性检测结果 */
    CompatibilityResult?: TwainCompatibilityResult;
}

/**
 * TWAIN兼容性检测结果
 */
interface TwainCompatibilityResult {
    /** NTwain库是否可用 */
    NTwainLibraryAvailable: boolean;
    /** NTwain库错误信息 */
    NTwainLibraryError?: string;
    /** TWAIN DSM是否可用 */
    TwainDsmAvailable: boolean;
    /** TWAIN DSM错误信息 */
    TwainDsmError?: string;
    /** TWAIN会话是否可用 */
    TwainSessionAvailable: boolean;
    /** TWAIN会话错误信息 */
    TwainSessionError?: string;
    /** 是否完全兼容 */
    IsFullyCompatible: boolean;
    /** 一般错误信息 */
    GeneralError?: string;
    /** 兼容性建议 */
    Recommendations: string[];
}

export = TwainMiddlewareSDK;
export as namespace TwainMiddlewareSDK; 