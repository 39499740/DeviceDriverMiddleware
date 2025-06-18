# TWAIN扫描仪中间件 JavaScript SDK 使用指南

## 概述

TWAIN扫描仪中间件 JavaScript SDK 提供了一个简单易用的接口，让Web应用程序能够通过WebSocket与TWAIN扫描仪中间件服务进行通信，实现扫描仪设备的控制和图像获取功能。

## 功能特性

- 🔗 自动WebSocket连接管理
- 🔄 自动重连机制
- 📷 扫描仪设备列表获取
- 🖼️ 图像扫描功能
- ⚙️ 丰富的扫描参数配置
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

        // 执行扫描
        const result = await twainSDK.scan({
            resolution: 300,
            colorMode: 'Color',
            format: 'PNG'
        });
        
        console.log('扫描完成:', result);
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

## 技术支持

如果您在使用过程中遇到问题，请：
1. 启用调试模式查看详细日志
2. 检查中间件服务的运行状态
3. 参考项目文档中的故障排除指南 