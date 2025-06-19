# 快速开始指南

## 🚀 5分钟快速上手

### 第一步：引入SDK
```html
<script src="sdk/TwainMiddlewareSDK.js"></script>
```

### 第二步：创建实例并连接
```javascript
// 创建SDK实例
const twainSDK = new TwainMiddlewareSDK({
    host: 'localhost',
    port: 45677,
    debug: true
});

// 连接到中间件
await twainSDK.connect();
```

### 第三步：获取设备列表
```javascript
// 获取扫描仪列表
const scanners = await twainSDK.getScanners();
console.log('可用扫描仪:', scanners);

// 获取打印机列表
const printers = await twainSDK.getPrinters();
console.log('可用打印机:', printers);
```

### 第四步：扫描测试
```javascript
// 配置扫描参数
const scanResult = await twainSDK.scan({
    resolution: 300,
    colorMode: 'Color',
    format: 'PNG'
});

if (scanResult.success) {
    // 显示扫描结果
    const imageUrl = `data:image/png;base64,${scanResult.data.imageData}`;
    const img = document.createElement('img');
    img.src = imageUrl;
    document.body.appendChild(img);
}
```

### 第五步：PDF打印测试
```javascript
// 选择PDF文件
const fileInput = document.getElementById('pdfFile');
const pdfFile = fileInput.files[0];

// 打印PDF
const printResult = await twainSDK.printPdf({
    printerName: printers[0].Name,
    pdfData: pdfFile,
    copies: 1,
    duplex: 0
});

if (printResult.Success) {
    console.log('打印成功！');
}
```

## 🎯 完整示例

```html
<!DOCTYPE html>
<html>
<head>
    <title>TWAIN SDK 快速示例</title>
</head>
<body>
    <h1>TWAIN扫描仪中间件测试</h1>
    
    <!-- 连接按钮 -->
    <button onclick="connect()">连接中间件</button>
    
    <!-- 扫描按钮 -->
    <button onclick="scan()">开始扫描</button>
    
    <!-- PDF打印 -->
    <input type="file" id="pdfFile" accept=".pdf">
    <button onclick="printPDF()">打印PDF</button>
    
    <!-- 结果显示 -->
    <div id="result"></div>

    <script src="sdk/TwainMiddlewareSDK.js"></script>
    <script>
        const twainSDK = new TwainMiddlewareSDK({
            host: 'localhost',
            port: 45677,
            debug: true
        });

        async function connect() {
            try {
                await twainSDK.connect();
                alert('连接成功！');
            } catch (error) {
                alert('连接失败: ' + error.message);
            }
        }

        async function scan() {
            try {
                const result = await twainSDK.scan({
                    resolution: 300,
                    colorMode: 'Color',
                    format: 'PNG'
                });
                
                if (result.success) {
                    const imageUrl = `data:image/png;base64,${result.data.imageData}`;
                    document.getElementById('result').innerHTML = 
                        `<img src="${imageUrl}" style="max-width: 500px;">`;
                }
            } catch (error) {
                alert('扫描失败: ' + error.message);
            }
        }

        async function printPDF() {
            try {
                const fileInput = document.getElementById('pdfFile');
                if (!fileInput.files[0]) {
                    alert('请先选择PDF文件');
                    return;
                }

                const printers = await twainSDK.getPrinters();
                if (printers.length === 0) {
                    alert('未找到可用的打印机');
                    return;
                }

                const result = await twainSDK.printPdf({
                    printerName: printers[0].Name,
                    pdfData: fileInput.files[0],
                    copies: 1
                });

                if (result.Success) {
                    alert('打印成功！');
                } else {
                    alert('打印失败: ' + result.Message);
                }
            } catch (error) {
                alert('打印出错: ' + error.message);
            }
        }
    </script>
</body>
</html>
```

## ⚠️ 重要提示

1. **确保中间件服务已启动** - 运行TwainMiddleware.exe
2. **检查端口配置** - 默认端口为45677
3. **安装扫描仪驱动** - 确保TWAIN驱动程序正确安装
4. **测试网络连接** - 确保防火墙允许WebSocket连接

## 🔍 故障排除

### 连接失败
- 检查中间件服务是否运行
- 确认端口号是否正确
- 检查防火墙设置

### 扫描失败
- 确认扫描仪已连接并开机
- 检查TWAIN驱动是否正确安装
- 尝试在其他软件中测试扫描仪

### PDF打印失败
- 确认打印机驱动正常
- 检查PDF文件是否有效
- 确认PDF文件大小不超过限制

## 📚 更多资源

- [完整API文档](README.md)
- [详细示例](example.html)
- [故障排除指南](README.md#故障排除)
- [更新日志](CHANGELOG.md) 