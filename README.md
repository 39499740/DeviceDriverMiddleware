# TWAIN扫描仪中间件

## 项目简介

TWAIN扫描仪中间件是一个基于C#开发的Windows桌面应用程序，为Web应用提供扫描仪设备访问能力。通过WebSocket和HTTP接口，Web前端可以轻松控制本地扫描仪设备，实现扫描功能。

## 主要功能

### 扫描仪功能
- 🖨️ **扫描仪设备管理**: 自动检测和列出可用的TWAIN扫描仪设备
- 📷 **图像扫描**: 支持多种分辨率、颜色模式和图像格式的扫描
- ⚙️ **扫描参数配置**: 分辨率、颜色模式、亮度、对比度等参数调节
- 🖼️ **多格式支持**: PNG、JPEG、TIFF、BMP等常见图像格式

### 打印机功能
- 🖨️ **真实打印机检测**: 自动识别并列出系统中安装的真实打印机（过滤虚拟打印机）
- 📋 **打印机信息获取**: 获取打印机状态、功能特性、支持的纸张尺寸等详细信息
- 🔍 **打印机属性查询**: 支持彩色打印、双面打印、最大打印份数等属性查询
- 📡 **实时更新**: 通过WebSocket推送打印机列表更新通知

### PDF打印功能 (新增)
- 📄 **PDF文件打印**: 支持接收PDF文件内容（BLOB格式）并打印到指定打印机
- ⚙️ **打印参数配置**: 支持设置打印份数、双面打印模式、纸张尺寸、页面范围等
- 📊 **实时进度监控**: 异步打印模式下提供实时进度更新和状态反馈
- 🔄 **同步/异步模式**: 支持同步打印和带进度回调的异步打印两种模式
- 🛡️ **错误处理**: 完善的错误处理机制，支持打印任务取消和状态跟踪

### 通信接口
- 🌐 **WebSocket接口**: 实时双向通信，支持事件推送
- 🔗 **HTTP接口**: RESTful API，便于集成
- 📡 **JavaScript SDK**: 封装完整的前端SDK，简化开发
- 🛡️ **自动重连**: 支持连接断开后自动重连

## 系统架构

```
┌─────────────────┐    WebSocket/HTTP    ┌─────────────────┐
│   Web前端应用   │ ←─────────────────→ │  TWAIN中间件服务 │
│                 │                      │                 │
│  JavaScript SDK │                      │   C# 服务程序   │
└─────────────────┘                      └─────────────────┘
                                                    ↓
                                         ┌─────────────────┐
                                         │  Windows系统    │
                                         │                 │
                                         │ 扫描仪/打印机设备│
                                         └─────────────────┘
```

## 快速开始

### 1. 环境要求

- Windows 7/8/10/11
- .NET Framework 4.5+
- 已安装的TWAIN兼容扫描仪驱动
- 系统打印机驱动

### 2. 安装部署

1. 下载中间件程序
2. 运行`setup.bat`进行环境配置
3. 启动`TwainMiddleware.exe`
4. 检查系统托盘图标确认服务运行状态

### 3. Web应用集成

#### 引入SDK
```html
<script src="sdk/TwainMiddlewareSDK.js"></script>
```

#### 基本使用
```javascript
// 创建SDK实例
const twainSDK = new TwainMiddlewareSDK({
    host: 'localhost',
    port: 45677
});

// 连接服务
await twainSDK.connect();

// 获取扫描仪列表
const scanners = await twainSDK.getScanners();

// 获取打印机列表
const printers = await twainSDK.getPrinters();

// 执行扫描
const result = await twainSDK.scan({
    resolution: 300,
    colorMode: 'Color',
    format: 'PNG'
});

// PDF打印示例
const printResult = await twainSDK.printPdf({
    printerName: printers[0].Name,
    pdfData: pdfFile, // File对象或PDF数据
    copies: 1,
    duplex: 0 // 使用默认双面设置
});
```

## API 接口

### WebSocket接口

| 命令 | 功能 | 说明 |
|------|------|------|
| `ping` | 心跳检测 | 测试连接状态 |
| `getscanners` | 获取扫描仪列表 | 返回可用扫描仪设备 |
| `getprinters` | 获取打印机列表 | 返回真实打印机设备 |
| `scan` | 执行扫描 | 根据参数进行扫描操作 |
| `printpdf` | 同步打印PDF | 同步执行PDF打印任务 |
| `printpdfasync` | 异步打印PDF | 异步执行PDF打印，支持进度回调 |

### 扫描参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| scannerName | string | "默认扫描仪" | 扫描仪名称 |
| resolution | int | 300 | 分辨率(DPI) |
| colorMode | string | "Color" | 颜色模式 |
| format | string | "PNG" | 图像格式 |
| brightness | int | 0 | 亮度(-1000~1000) |
| contrast | int | 0 | 对比度(-1000~1000) |

### PDF打印参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| printerName | string | - | 打印机名称（必需） |
| pdfData | File/Blob/ArrayBuffer/string | - | PDF文件数据（必需） |
| copies | int | 1 | 打印份数 |
| duplex | int | 0 | 双面打印模式(0=默认,1=单面,2=长边,3=短边) |
| paperSize | string | "" | 纸张尺寸 |
| startPage | int | 1 | 起始页 |
| endPage | int | 0 | 结束页(0=到最后一页) |

### 打印机信息

| 属性 | 类型 | 说明 |
|------|------|------|
| Name | string | 打印机名称 |
| IsDefault | boolean | 是否为默认打印机 |
| Status | string | 打印机状态 |
| CanDuplex | boolean | 是否支持双面打印 |
| MaximumCopies | number | 最大打印份数 |
| SupportsColor | boolean | 是否支持彩色打印 |
| PaperSizes | Array | 支持的纸张尺寸列表 |
| PrinterType | string | 打印机技术类型（激光打印机、喷墨打印机、针式打印机等） |
| MarkingTechnology | number | Windows标记技术代码 |
| PrinterTypeDescription | string | 打印机技术类型详细描述 |

#### 支持的打印机类型
- 🖨️ **激光打印机**: 电子照相激光技术
- 💧 **喷墨打印机**: 水性/固体喷墨技术  
- ⚙️ **针式打印机**: 点阵撞击式技术
- 🔥 **热敏打印机**: 热转印/热敏技术
- 💡 **LED打印机**: 电子照相LED技术
- 📏 **绘图仪**: 绘图笔技术
- 🔧 **其他类型**: 其他或未知技术类型

## 项目结构

```
DeviceDriverMiddleware/
├── bin/                    # 编译输出目录
├── docs/                   # 文档目录
│   └── 部署说明.md
├── src/                    # 源代码目录
│   ├── Program.cs          # 程序入口
│   ├── TwainService.cs     # TWAIN扫描仪服务
│   ├── WebSocketServer.cs  # WebSocket服务器
│   ├── HttpServer.cs       # HTTP服务器
│   └── ...
├── sdk/                    # JavaScript SDK
│   ├── TwainMiddlewareSDK.js
│   ├── TwainMiddlewareSDK.d.ts
│   ├── example.html        # SDK使用示例
│   └── README.md
├── web/                    # Web测试页面
│   └── test.html
└── README.md              # 项目说明
```

## 示例和测试

### SDK示例
打开 `sdk/example.html` 查看完整的SDK使用示例，包含：
- 连接管理
- 扫描仪操作
- 打印机管理
- 参数配置
- 结果显示

### 测试页面
打开 `web/test.html` 进行功能测试：
- 基本连接测试
- 扫描功能测试
- 打印机列表获取测试

## 更新日志

### v1.2.0 (最新)
- ✨ 新增PDF文件打印功能
- ✨ 支持同步和异步PDF打印模式
- ✨ 实时打印进度监控和状态反馈
- ✨ 支持打印参数配置（份数、双面打印、页面范围等）
- 📝 更新SDK API，添加printPdf和printPdfAsync方法
- 🔧 完善测试页面，添加PDF打印测试功能
- 📚 更新文档和TypeScript定义

### v1.1.0
- ✨ 新增打印机列表获取功能
- ✨ 支持真实打印机识别（过滤虚拟打印机）
- ✨ 获取打印机详细属性信息
- 📝 更新SDK文档和示例
- 🔧 完善测试页面

### v1.0.0
- 🎉 初始版本发布
- ✨ TWAIN扫描仪支持
- ✨ WebSocket和HTTP接口
- ✨ JavaScript SDK
- 📝 完整文档和示例

## 技术支持

如有问题或建议，请联系技术支持团队。

## 许可证

本项目采用专有软件许可证。 