# TWAIN扫描仪中间件 JavaScript SDK 使用指南

## 概述

TWAIN扫描仪中间件 JavaScript SDK 提供了一个简单易用的接口，让Web应用程序能够通过WebSocket与TWAIN扫描仪中间件服务进行通信，实现扫描仪设备的控制和图像获取功能。

## 版本更新

### v1.2.0 (最新版本)
- ✅ **修复PDF打印功能** - 完全支持PDF文件打印，包含Base64数据编码和解码
- ✅ **架构兼容性** - 支持32位和64位系统，自动使用正确的native库
- ✅ **C# 5兼容性** - 完全兼容旧版编译器环境  
- ✅ **依赖库集成** - 自动集成PdfiumViewer和native依赖库
- ✅ **错误处理优化** - 改进的错误消息和异常处理

## 📚 文档导航

- **[🚀 快速开始指南](QUICKSTART.md)** - 5分钟快速上手
- **[📋 更新日志](CHANGELOG.md)** - 版本历史和改进记录
- **[📖 完整文档](#api-参考)** - 详细API参考文档

## 功能特性

- 🔗 自动WebSocket连接管理
- 🔄 自动重连机制
- 📷 扫描仪设备列表获取
- 🖨️ 真实打印机列表获取
- 🖼️ 图像扫描功能
- 📄 PDF文件打印功能
- ⚙️ 丰富的扫描参数配置
- 📊 实时打印进度监控
- 📡 实时事件通知
- 💓 心跳检测
- 🛡️ 错误处理和重试机制

## 安装

### 浏览器环境

直接在HTML中引入SDK文件：

```html
<script src="sdk/TwainMiddlewareSDK.js"></script>
```

### Node.js环境

```javascript
const TwainMiddlewareSDK = require('./sdk/TwainMiddlewareSDK');
```

## 快速开始

### 基本使用

```javascript
// 创建SDK实例
const twainSDK = new TwainMiddlewareSDK({
    host: 'localhost',
    port: 5000,
    debug: true
});

// 连接到中间件服务
async function connectAndScan() {
    try {
        // 建立连接
        await twainSDK.connect();
        console.log('连接成功');

        // 获取扫描仪列表
        const scanners = await twainSDK.getScanners();
        console.log('可用扫描仪:', scanners);

        // 获取打印机列表
        const printers = await twainSDK.getPrinters();
        console.log('可用打印机:', printers);

        // 执行扫描
        const result = await twainSDK.scan({
            resolution: 300,
            colorMode: 'Color',
            format: 'PNG'
        });
        
        console.log('扫描完成:', result);

        // PDF打印示例（假设有一个文件输入元素）
        const fileInput = document.getElementById('pdfFile');
        if (fileInput.files.length > 0 && printers.length > 0) {
            const printResult = await twainSDK.printPdf({
                printerName: printers[0].Name,
                pdfData: fileInput.files[0],
                copies: 1,
                duplex: 0 // 使用打印机默认设置
            });
            console.log('打印完成:', printResult);
        }
    } catch (error) {
        console.error('操作失败:', error);
    }
}

connectAndScan();
```

## API 参考

### 构造函数

```javascript
new TwainMiddlewareSDK(options)
```

#### 参数

| 参数名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| host | string | 'localhost' | 中间件服务器地址 |
| port | number | 5000 | 中间件服务器端口 |
| autoReconnect | boolean | true | 是否自动重连 |
| reconnectInterval | number | 3000 | 重连间隔（毫秒） |
| maxReconnectAttempts | number | 5 | 最大重连次数 |
| debug | boolean | false | 是否开启调试模式 |

### 方法

#### connect()

建立WebSocket连接

```javascript
await twainSDK.connect();
```

**返回值:** `Promise<void>`

#### disconnect()

断开WebSocket连接

```javascript
twainSDK.disconnect();
```

#### getScanners()

获取可用扫描仪列表

```javascript
const scanners = await twainSDK.getScanners();
```

**返回值:** `Promise<Array<string>>`

#### getPrinters()

获取系统中的真实打印机列表（过滤虚拟打印机）

```javascript
const printers = await twainSDK.getPrinters();
```

**返回值:** `Promise<Array<PrinterInfo>>`

**PrinterInfo 对象结构:**

| 属性名 | 类型 | 说明 |
|--------|------|------|
| Name | string | 打印机名称 |
| IsDefault | boolean | 是否为默认打印机 |
| Status | string | 打印机状态 |
| CanDuplex | boolean | 是否支持双面打印 |
| MaximumCopies | number | 最大打印份数 |
| SupportsColor | boolean | 是否支持彩色打印 |
| PaperSizes | Array<string> | 支持的纸张尺寸列表 |

#### scan(options)

执行扫描操作

```javascript
const result = await twainSDK.scan({
    scannerName: '默认扫描仪',
    resolution: 300,
    colorMode: 'Color',
    format: 'PNG',
    showUI: false,
    brightness: 0,
    contrast: 0,
    autoRotate: false,
    autoCrop: false
});
```

**参数说明:**

| 参数名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| scannerName | string | '默认扫描仪' | 扫描仪名称 |
| resolution | number | 300 | 分辨率 (DPI) |
| colorMode | string | 'Color' | 颜色模式: 'Color', 'Gray', 'BlackWhite' |
| format | string | 'PNG' | 图像格式: 'PNG', 'JPEG', 'TIFF', 'BMP' |
| showUI | boolean | false | 是否显示扫描仪界面 |
| brightness | number | 0 | 亮度 (-1000 ~ 1000) |
| contrast | number | 0 | 对比度 (-1000 ~ 1000) |
| autoRotate | boolean | false | 自动旋转 |
| autoCrop | boolean | false | 自动裁剪 |

**返回值:** `Promise<object>`

#### ping()

发送心跳检测

```javascript
const response = await twainSDK.ping();
```

**返回值:** `Promise<object>`

#### printPdf(options)

打印PDF文件

> **🎉 功能已修复**: 在最新版本中，PDF打印功能已完全修复，包括：
> - ✅ Base64数据编码/解码问题已解决
> - ✅ 支持32位和64位系统架构
> - ✅ 自动集成所需的native依赖库
> - ✅ 兼容C# 5编译器环境

```javascript
const printResult = await twainSDK.printPdf({
    printerName: '打印机名称',
    pdfData: pdfFile, // File对象、Blob对象、ArrayBuffer或base64字符串
    copies: 1,
    duplex: 0, // 0=默认, 1=单面, 2=双面长边, 3=双面短边
    paperSize: 'A4',
    startPage: 1,
    endPage: 0 // 0表示到最后一页
});
```

**参数说明:**

| 参数名 | 类型 | 必需 | 默认值 | 说明 |
|--------|------|------|--------|------|
| printerName | string | ✓ | - | 打印机名称 |
| pdfData | File\|Blob\|ArrayBuffer\|string | ✓ | - | PDF文件数据 |
| copies | number | ✗ | 1 | 打印份数 |
| duplex | number | ✗ | 0 | 双面打印模式 |
| paperSize | string | ✗ | '' | 纸张尺寸 |
| startPage | number | ✗ | 1 | 起始页 |
| endPage | number | ✗ | 0 | 结束页(0=全部) |

**返回值:** `Promise<PrintResponse>`

#### printPdfAsync(options, progressCallback)

异步打印PDF文件（带进度回调）

```javascript
const printResult = await twainSDK.printPdfAsync({
    printerName: '打印机名称',
    pdfData: pdfFile,
    copies: 1
}, (progress) => {
    console.log(`打印进度: ${progress.Percentage}%`);
    console.log(`状态: ${progress.Status}`);
    console.log(`当前页: ${progress.CurrentPage}/${progress.TotalPages}`);
});
```

**参数说明:**

- **options**: 同 `printPdf()` 方法的参数
- **progressCallback**: 可选的进度回调函数

**进度对象结构:**

| 属性名 | 类型 | 说明 |
|--------|------|------|
| Status | string | 状态（开始、准备中、打印中、完成、失败、已取消） |
| Message | string | 状态消息 |
| CurrentPage | number | 当前页 |
| TotalPages | number | 总页数 |
| Percentage | number | 进度百分比（0-100） |

**返回值:** `Promise<PrintResult>`

#### checkHealth()

检查服务器健康状态

```javascript
const isHealthy = await twainSDK.checkHealth();
```

**返回值:** `Promise<boolean>`

#### getScannersHttp()

通过HTTP API获取扫描仪列表（备用方法）

```javascript
const scanners = await twainSDK.getScannersHttp();
```

**返回值:** `Promise<Array<string>>`

#### getConnectionState()

获取当前连接状态

```javascript
const isConnected = twainSDK.getConnectionState();
```

**返回值:** `boolean`

### 事件监听

SDK支持多种事件监听，用于实时获取扫描状态和连接信息。

#### 支持的事件

- `connected` - 连接成功
- `disconnected` - 连接断开
- `error` - 发生错误
- `scanStarted` - 扫描开始
- `scanCompleted` - 扫描完成
- `scannersUpdated` - 扫描仪列表更新
- `printersUpdated` - 打印机列表更新
- `printStarted` - PDF打印开始
- `printProgress` - PDF打印进度更新
- `printCompleted` - PDF打印完成

#### 添加事件监听器

```javascript
twainSDK.on('connected', () => {
    console.log('已连接到中间件服务');
});

twainSDK.on('scanStarted', (data) => {
    console.log('扫描开始:', data);
});

twainSDK.on('scanCompleted', (data) => {
    console.log('扫描完成:', data);
    
    // 显示扫描结果
    if (data.success) {
        const imageData = data.data.imageData;
        const format = data.data.format.toLowerCase();
        const imageUrl = `data:image/${format};base64,${imageData}`;
        
        // 创建图片元素并显示
        const img = document.createElement('img');
        img.src = imageUrl;
        document.body.appendChild(img);
    }
});

twainSDK.on('printersUpdated', (printers) => {
    console.log('打印机列表更新:', printers);
    
    // 更新打印机选择框
    const printerSelect = document.getElementById('printerSelect');
    printerSelect.innerHTML = '<option value="">选择打印机</option>';
    
    printers.forEach((printer, index) => {
        const option = document.createElement('option');
        option.value = index;
        option.textContent = printer.Name + (printer.IsDefault ? ' (默认)' : '');
        printerSelect.appendChild(option);
    });
});

// PDF打印事件监听
twainSDK.on('printStarted', (data) => {
    console.log('PDF打印开始:', data);
});

twainSDK.on('printProgress', (progress) => {
    console.log(`打印进度: ${progress.Percentage}%`);
    console.log(`状态: ${progress.Status}`);
    console.log(`当前页: ${progress.CurrentPage}/${progress.TotalPages}`);
    
    // 更新进度条
    const progressBar = document.getElementById('printProgressBar');
    if (progressBar) {
        progressBar.style.width = progress.Percentage + '%';
        progressBar.textContent = `${progress.Percentage}% (${progress.CurrentPage}/${progress.TotalPages})`;
    }
});

twainSDK.on('printCompleted', (result) => {
    console.log('PDF打印完成:', result);
    
    if (result.Success) {
        console.log(`打印成功: 总页数 ${result.TotalPages}, 已打印 ${result.PrintedPages} 页`);
        alert('PDF打印完成！');
    } else {
        console.error('打印失败:', result.Message);
        alert('PDF打印失败: ' + result.Message);
    }
});

twainSDK.on('error', (error) => {
    console.error('SDK错误:', error);
});
```

#### 移除事件监听器

```javascript
const errorHandler = (error) => {
    console.error('错误:', error);
};

// 添加监听器
twainSDK.on('error', errorHandler);

// 移除监听器
twainSDK.off('error', errorHandler);
```

## 使用示例

### 完整的扫描应用示例

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TWAIN扫描仪应用</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 20px;
        }
        .controls {
            margin-bottom: 20px;
        }
        .controls > * {
            margin-right: 10px;
            margin-bottom: 10px;
        }
        .status {
            padding: 10px;
            border-radius: 5px;
            margin-bottom: 20px;
        }
        .status.connected {
            background-color: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }
        .status.disconnected {
            background-color: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }
        .scanner-image {
            max-width: 100%;
            border: 1px solid #ddd;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <h1>TWAIN扫描仪应用</h1>
    
    <div id="status" class="status disconnected">未连接</div>
    
    <div class="controls">
        <button id="connectBtn">连接</button>
        <button id="disconnectBtn" disabled>断开连接</button>
        <button id="getScannersBtn" disabled>获取扫描仪列表</button>
        <button id="scanBtn" disabled>开始扫描</button>
    </div>
    
    <div class="controls">
        <label>
            扫描仪:
            <select id="scannerSelect" disabled>
                <option value="">选择扫描仪</option>
            </select>
        </label>
        
        <label>
            分辨率:
            <select id="resolutionSelect">
                <option value="150">150 DPI</option>
                <option value="300" selected>300 DPI</option>
                <option value="600">600 DPI</option>
            </select>
        </label>
        
        <label>
            颜色模式:
            <select id="colorModeSelect">
                <option value="Color" selected>彩色</option>
                <option value="Gray">灰度</option>
                <option value="BlackWhite">黑白</option>
            </select>
        </label>
        
        <label>
            格式:
            <select id="formatSelect">
                <option value="PNG" selected>PNG</option>
                <option value="JPEG">JPEG</option>
                <option value="TIFF">TIFF</option>
                <option value="BMP">BMP</option>
            </select>
        </label>
    </div>
    
    <div id="scanResult"></div>
    
    <script src="TwainMiddlewareSDK.js"></script>
    <script>
        // 创建SDK实例
        const twainSDK = new TwainMiddlewareSDK({
            host: 'localhost',
            port: 5000,
            debug: true
        });
        
        // DOM元素
        const statusEl = document.getElementById('status');
        const connectBtn = document.getElementById('connectBtn');
        const disconnectBtn = document.getElementById('disconnectBtn');
        const getScannersBtn = document.getElementById('getScannersBtn');
        const scanBtn = document.getElementById('scanBtn');
        const scannerSelect = document.getElementById('scannerSelect');
        const resolutionSelect = document.getElementById('resolutionSelect');
        const colorModeSelect = document.getElementById('colorModeSelect');
        const formatSelect = document.getElementById('formatSelect');
        const scanResult = document.getElementById('scanResult');
        
        // 更新UI状态
        function updateUI(connected) {
            if (connected) {
                statusEl.textContent = '已连接';
                statusEl.className = 'status connected';
                connectBtn.disabled = true;
                disconnectBtn.disabled = false;
                getScannersBtn.disabled = false;
                scanBtn.disabled = false;
                scannerSelect.disabled = false;
            } else {
                statusEl.textContent = '未连接';
                statusEl.className = 'status disconnected';
                connectBtn.disabled = false;
                disconnectBtn.disabled = true;
                getScannersBtn.disabled = true;
                scanBtn.disabled = true;
                scannerSelect.disabled = true;
            }
        }
        
        // 事件监听器
        twainSDK.on('connected', () => {
            updateUI(true);
            // 自动获取扫描仪列表
            getScanners();
        });
        
        twainSDK.on('disconnected', () => {
            updateUI(false);
        });
        
        twainSDK.on('scanStarted', (data) => {
            scanResult.innerHTML = '<p>扫描进行中...</p>';
            scanBtn.disabled = true;
        });
        
        twainSDK.on('scanCompleted', (data) => {
            scanBtn.disabled = false;
            
            if (data.success) {
                const imageData = data.data.imageData;
                const format = data.data.format.toLowerCase();
                const width = data.data.width;
                const height = data.data.height;
                const imageUrl = `data:image/${format};base64,${imageData}`;
                
                scanResult.innerHTML = `
                    <p>扫描完成！</p>
                    <p>尺寸: ${width} x ${height}</p>
                    <p>格式: ${data.data.format}</p>
                    <img src="${imageUrl}" class="scanner-image" alt="扫描结果" />
                `;
            } else {
                scanResult.innerHTML = `<p style="color: red;">扫描失败: ${data.message}</p>`;
            }
        });
        
        twainSDK.on('error', (error) => {
            console.error('SDK错误:', error);
            scanResult.innerHTML = `<p style="color: red;">错误: ${error.message}</p>`;
        });
        
        // 按钮事件
        connectBtn.onclick = async () => {
            try {
                await twainSDK.connect();
            } catch (error) {
                alert('连接失败: ' + error.message);
            }
        };
        
        disconnectBtn.onclick = () => {
            twainSDK.disconnect();
        };
        
        getScannersBtn.onclick = getScanners;
        
        scanBtn.onclick = async () => {
            try {
                const scanOptions = {
                    scannerName: scannerSelect.value || '默认扫描仪',
                    resolution: parseInt(resolutionSelect.value),
                    colorMode: colorModeSelect.value,
                    format: formatSelect.value
                };
                
                await twainSDK.scan(scanOptions);
            } catch (error) {
                alert('扫描失败: ' + error.message);
            }
        };
        
        // 获取扫描仪列表
        async function getScanners() {
            try {
                const scanners = await twainSDK.getScanners();
                
                // 清空选项
                scannerSelect.innerHTML = '<option value="">选择扫描仪</option>';
                
                // 添加扫描仪选项
                scanners.forEach(scanner => {
                    const option = document.createElement('option');
                    option.value = scanner;
                    option.textContent = scanner;
                    scannerSelect.appendChild(option);
                });
                
                // 如果只有一个扫描仪，自动选择
                if (scanners.length === 1) {
                    scannerSelect.value = scanners[0];
                }
                
            } catch (error) {
                console.error('获取扫描仪列表失败:', error);
            }
        }
        
        // 初始化
        updateUI(false);
    </script>
</body>
</html>
```

### Node.js环境使用示例

```javascript
const TwainMiddlewareSDK = require('./TwainMiddlewareSDK');

async function main() {
    const twainSDK = new TwainMiddlewareSDK({
        host: 'localhost',
        port: 5000,
        debug: true
    });

    try {
        // 连接
        await twainSDK.connect();
        console.log('连接成功');

        // 获取扫描仪列表
        const scanners = await twainSDK.getScanners();
        console.log('可用扫描仪:', scanners);

        // 执行扫描
        const result = await twainSDK.scan({
            resolution: 300,
            colorMode: 'Color',
            format: 'PNG'
        });

        if (result.success) {
            console.log('扫描成功');
            console.log('图像尺寸:', result.data.width, 'x', result.data.height);
            
            // 保存图像到文件
            const fs = require('fs');
            const imageBuffer = Buffer.from(result.data.imageData, 'base64');
            fs.writeFileSync('scanned_image.png', imageBuffer);
            console.log('图像已保存到 scanned_image.png');
        }

    } catch (error) {
        console.error('操作失败:', error);
    } finally {
        twainSDK.disconnect();
    }
}

main();
```

## 错误处理

SDK提供了多层错误处理机制：

### 连接错误
```javascript
try {
    await twainSDK.connect();
} catch (error) {
    console.error('连接失败:', error.message);
    // 处理连接失败的情况
}
```

### 操作错误
```javascript
try {
    const result = await twainSDK.scan();
} catch (error) {
    console.error('扫描失败:', error.message);
    // 处理扫描失败的情况
}
```

### 事件错误
```javascript
twainSDK.on('error', (error) => {
    console.error('SDK错误:', error);
    // 处理各种运行时错误
});
```

## 调试

启用调试模式可以查看详细的日志信息：

```javascript
const twainSDK = new TwainMiddlewareSDK({
    debug: true
});
```

调试信息将输出到浏览器控制台，包括：
- WebSocket连接状态
- 发送和接收的消息
- 错误信息
- 重连尝试

## 注意事项

1. **网络连接**: 确保中间件服务正在运行并且网络连接正常
2. **端口配置**: 确认中间件服务的端口设置
3. **浏览器兼容性**: 需要支持WebSocket的现代浏览器
4. **扫描仪驱动**: 确保扫描仪设备和TWAIN驱动程序正确安装
5. **权限设置**: 某些系统可能需要管理员权限访问扫描仪设备

## 常见问题

### Q: 连接失败怎么办？
A: 检查中间件服务是否运行，端口是否正确，网络连接是否正常。

### Q: 扫描仪列表为空？
A: 确认扫描仪设备已连接并安装正确的TWAIN驱动程序。

### Q: 扫描失败？
A: 检查扫描参数是否正确，扫描仪是否可用，查看调试日志获取详细错误信息。

### Q: 如何处理大图片？
A: 对于大尺寸图片，建议降低分辨率或使用JPEG格式以减少数据传输量。

## 故障排除

### PDF打印问题

#### 问题：PDF打印功能不可用
**解决方案：**
1. 确认中间件版本为最新版本 (v1.2.0+)
2. 检查是否正确部署了以下文件：
   - `PdfiumViewer.dll`
   - `pdfium.dll` (64位版本，约15.8MB)
3. 确认打印机驱动程序正常工作

#### 问题：Base64数据解码错误
**解决方案：**
- 最新版本已修复此问题
- 确保PDF数据格式正确，支持以下格式：
  ```javascript
  // 1. File对象 (推荐)
  pdfData: fileInput.files[0]
  
  // 2. Base64字符串 (自动处理)
  pdfData: "data:application/pdf;base64,JVBERi0xLjQ..."
  
  // 3. 纯Base64 (自动检测)
  pdfData: "JVBERi0xLjQ..."
  ```

#### 问题：架构不匹配错误
**解决方案：**
- 最新版本自动选择正确的64位native库
- 如果仍有问题，确认系统架构并手动下载对应版本的pdfium.dll

### 连接问题

#### 问题：WebSocket连接失败
**解决方案：**
1. 检查中间件服务是否运行
2. 确认端口配置正确 (默认: 45677)
3. 检查防火墙设置
4. 尝试使用不同的端口

#### 问题：自动重连不工作
**解决方案：**
```javascript
const twainSDK = new TwainMiddlewareSDK({
    autoReconnect: true,
    reconnectInterval: 3000,  // 3秒
    maxReconnectAttempts: 5   // 最多5次
});
```

### 扫描问题

#### 问题：扫描仪无法识别
**解决方案：**
1. 确认扫描仪已正确连接
2. 安装最新的TWAIN驱动程序
3. 在设备管理器中检查设备状态
4. 重启扫描仪设备

#### 问题：扫描图像质量问题
**解决方案：**
```javascript
// 调整扫描参数
const result = await twainSDK.scan({
    resolution: 600,        // 提高分辨率
    colorMode: 'Color',     // 使用彩色模式
    brightness: 100,        // 调整亮度
    contrast: 50,          // 调整对比度
    autoCrop: true,        // 启用自动裁剪
    autoRotate: true       // 启用自动旋转
});
```

### 编译问题

#### 问题：C# 5语法错误
**解决方案：**
- 最新版本已完全兼容C# 5
- 确保使用最新的源代码
- 所有字符串插值和空条件运算符已替换为兼容语法

#### 问题：依赖库缺失
**解决方案：**
1. 运行 `build-simple.bat` 自动下载依赖
2. 手动下载缺失的NuGet包：
   - Newtonsoft.Json
   - websocket-sharp
   - NTwain
   - PdfiumViewer
   - PdfiumViewer.Native.x86_64.v8-xfa

### 调试技巧

#### 启用详细日志
```javascript
const twainSDK = new TwainMiddlewareSDK({
    debug: true  // 启用调试模式
});

// 监听所有事件
twainSDK.on('error', console.error);
twainSDK.on('connected', () => console.log('已连接'));
twainSDK.on('disconnected', () => console.log('已断开'));
```

#### 检查中间件状态
```javascript
// 检查健康状态
const isHealthy = await twainSDK.checkHealth();
console.log('服务健康状态:', isHealthy);

// 检查连接状态
const isConnected = twainSDK.getConnectionState();
console.log('连接状态:', isConnected);
```

## 技术支持

如果您在使用过程中遇到问题，请：
1. 启用调试模式查看详细日志
2. 检查中间件服务的运行状态
3. 参考上述故障排除指南
4. 查看项目的Issues页面
5. 确认使用的是最新版本 (v1.2.0+) 