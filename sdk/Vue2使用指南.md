# TWAIN扫描仪中间件 Vue2 使用指南

## 📋 功能完整性确认

✅ **完整功能已可用**，包括：

### 🖨️ 打印功能
- `getPrinters()` - 获取打印机列表
- `printPdf()` - 同步打印PDF
- `printPdfAsync()` - 异步打印PDF（带进度回调）

### 🖼️ 扫描功能
- `getScanners()` - 获取扫描仪列表
- `scan()` - 执行扫描操作
- `checkTwainStatus()` - 检查TWAIN状态

### 🔗 连接管理
- `connect()` - 连接到中间件
- `disconnect()` - 断开连接
- `ping()` - 心跳检测
- `checkHealth()` - 健康检查

### 📡 事件系统
- 完整的事件监听器支持 (`on`, `off`)
- 自动重连机制
- 实时进度回调

## 🚀 Vue2 项目集成步骤

### 1. 复制SDK文件到项目

```bash
# 将SDK文件复制到Vue项目的public目录
cp TwainMiddlewareSDK.js /your-vue-project/public/js/
cp TwainMiddlewareSDK.d.ts /your-vue-project/src/types/ # 如果使用TypeScript
```

### 2. 在HTML中引入SDK

在 `public/index.html` 中添加：

```html
<!DOCTYPE html>
<html>
<head>
    <!-- 其他内容 -->
</head>
<body>
    <div id="app"></div>
    
    <!-- 在Vue应用挂载前引入SDK -->
    <script src="/js/TwainMiddlewareSDK.js"></script>
    <!-- built files will be auto injected -->
</body>
</html>
```

### 3. Vue组件中使用SDK

#### 3.1 基础使用示例

```vue
<template>
  <div class="twain-demo">
    <h2>TWAIN扫描仪中间件演示</h2>
    
    <!-- 连接状态 -->
    <div class="status-bar" :class="{ connected: isConnected }">
      状态: {{ isConnected ? '已连接' : '未连接' }}
    </div>
    
    <!-- 控制按钮 -->
    <div class="controls">
      <button @click="connect" :disabled="isConnected">连接</button>
      <button @click="disconnect" :disabled="!isConnected">断开</button>
      <button @click="getPrinters" :disabled="!isConnected">获取打印机</button>
      <button @click="getScanners" :disabled="!isConnected">获取扫描仪</button>
    </div>
    
    <!-- 打印机列表 -->
    <div v-if="printers.length > 0" class="printers">
      <h3>打印机列表</h3>
      <select v-model="selectedPrinter">
        <option v-for="printer in printers" :key="printer.Name" :value="printer">
          {{ printer.Name }} {{ printer.IsDefault ? '(默认)' : '' }}
        </option>
      </select>
    </div>
    
    <!-- PDF打印 -->
    <div class="print-section">
      <h3>PDF打印</h3>
      <input type="file" @change="onFileSelect" accept=".pdf" />
      <button @click="printPdf" :disabled="!canPrint">打印PDF</button>
      <button @click="printPdfAsync" :disabled="!canPrint">异步打印</button>
    </div>
    
    <!-- 进度显示 -->
    <div v-if="printProgress.visible" class="progress">
      <h4>打印进度</h4>
      <div class="progress-bar">
        <div class="progress-fill" :style="{ width: printProgress.percentage + '%' }"></div>
      </div>
      <p>{{ printProgress.status }} - {{ printProgress.percentage }}%</p>
      <p>第 {{ printProgress.currentPage }} 页 / 共 {{ printProgress.totalPages }} 页</p>
    </div>
    
    <!-- 日志 -->
    <div class="logs">
      <h3>操作日志</h3>
      <div class="log-container">
        <div v-for="(log, index) in logs" :key="index" :class="'log-' + log.type">
          [{{ log.time }}] {{ log.message }}
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'TwainDemo',
  data() {
    return {
      // SDK实例
      twainSDK: null,
      
      // 连接状态
      isConnected: false,
      
      // 设备列表
      printers: [],
      scanners: [],
      
      // 选中的设备
      selectedPrinter: null,
      selectedScanner: null,
      
      // 文件
      selectedFile: null,
      
      // 打印进度
      printProgress: {
        visible: false,
        status: '',
        percentage: 0,
        currentPage: 0,
        totalPages: 0
      },
      
      // 日志
      logs: []
    }
  },
  
  computed: {
    canPrint() {
      return this.isConnected && this.selectedPrinter && this.selectedFile;
    }
  },
  
  mounted() {
    this.initSDK();
  },
  
  beforeDestroy() {
    if (this.twainSDK) {
      this.twainSDK.disconnect();
    }
  },
  
  methods: {
    // 初始化SDK
    initSDK() {
      try {
        // 确保全局TwainMiddlewareSDK可用
        if (typeof TwainMiddlewareSDK === 'undefined') {
          this.addLog('错误：TwainMiddlewareSDK未加载', 'error');
          return;
        }
        
        // 创建SDK实例
        this.twainSDK = new TwainMiddlewareSDK({
          host: 'localhost',
          port: 45677,
          debug: true,
          autoReconnect: true,
          maxReconnectAttempts: 3
        });
        
        // 设置事件监听器
        this.setupEventListeners();
        
        this.addLog('SDK初始化成功', 'success');
      } catch (error) {
        this.addLog(`SDK初始化失败: ${error.message}`, 'error');
      }
    },
    
    // 设置事件监听器
    setupEventListeners() {
      // 连接事件
      this.twainSDK.on('connected', () => {
        this.isConnected = true;
        this.addLog('连接成功', 'success');
      });
      
      this.twainSDK.on('disconnected', (data) => {
        this.isConnected = false;
        this.addLog(`连接断开: ${data.reason}`, 'warning');
      });
      
      this.twainSDK.on('error', (error) => {
        this.addLog(`连接错误: ${error.message}`, 'error');
      });
      
      // 打印事件
      this.twainSDK.on('printStarted', (data) => {
        this.addLog(`开始打印: ${data.printerName}`, 'info');
        this.printProgress.visible = true;
      });
      
      this.twainSDK.on('printCompleted', (data) => {
        this.addLog('打印完成', 'success');
        this.printProgress.visible = false;
      });
      
      // 设备更新事件
      this.twainSDK.on('printersUpdated', (printers) => {
        this.addLog(`发现 ${printers.length} 个打印机`, 'info');
      });
      
      this.twainSDK.on('scannersUpdated', (scanners) => {
        this.addLog(`发现 ${scanners.length} 个扫描仪`, 'info');
      });
    },
    
    // 连接到中间件
    async connect() {
      try {
        await this.twainSDK.connect();
        // 连接成功后自动获取设备列表
        setTimeout(() => {
          this.getPrinters();
          this.getScanners();
        }, 500);
      } catch (error) {
        this.addLog(`连接失败: ${error.message}`, 'error');
      }
    },
    
    // 断开连接
    disconnect() {
      this.twainSDK.disconnect();
    },
    
    // 获取打印机列表
    async getPrinters() {
      try {
        this.printers = await this.twainSDK.getPrinters();
        this.addLog(`获取到 ${this.printers.length} 个打印机`, 'success');
        
        // 自动选择默认打印机
        const defaultPrinter = this.printers.find(p => p.IsDefault);
        if (defaultPrinter) {
          this.selectedPrinter = defaultPrinter;
        }
      } catch (error) {
        this.addLog(`获取打印机失败: ${error.message}`, 'error');
      }
    },
    
    // 获取扫描仪列表
    async getScanners() {
      try {
        this.scanners = await this.twainSDK.getScanners();
        this.addLog(`获取到 ${this.scanners.length} 个扫描仪`, 'success');
      } catch (error) {
        this.addLog(`获取扫描仪失败: ${error.message}`, 'error');
      }
    },
    
    // 文件选择
    onFileSelect(event) {
      const file = event.target.files[0];
      if (file && file.type === 'application/pdf') {
        this.selectedFile = file;
        this.addLog(`选择PDF文件: ${file.name}`, 'info');
      } else {
        this.addLog('请选择PDF文件', 'warning');
      }
    },
    
    // 同步打印PDF
    async printPdf() {
      try {
        const result = await this.twainSDK.printPdf({
          printerName: this.selectedPrinter.Name,
          pdfData: this.selectedFile,
          copies: 1,
          duplex: 0
        });
        
        if (result.Data && result.Data.Success) {
          this.addLog(`打印成功: ${result.Data.Message}`, 'success');
        } else {
          this.addLog(`打印失败: ${result.Data?.Message || result.Message}`, 'error');
        }
      } catch (error) {
        this.addLog(`打印错误: ${error.message}`, 'error');
      }
    },
    
    // 异步打印PDF（带进度）
    async printPdfAsync() {
      try {
        const result = await this.twainSDK.printPdfAsync({
          printerName: this.selectedPrinter.Name,
          pdfData: this.selectedFile,
          copies: 1,
          duplex: 0
        }, (progress) => {
          // 更新进度
          this.printProgress.status = progress.Status;
          this.printProgress.percentage = progress.Percentage;
          this.printProgress.currentPage = progress.CurrentPage;
          this.printProgress.totalPages = progress.TotalPages;
        });
        
        if (result.Success) {
          this.addLog(`异步打印成功: ${result.Message}`, 'success');
        } else {
          this.addLog(`异步打印失败: ${result.Message}`, 'error');
        }
      } catch (error) {
        this.addLog(`异步打印错误: ${error.message}`, 'error');
      } finally {
        this.printProgress.visible = false;
      }
    },
    
    // 添加日志
    addLog(message, type = 'info') {
      const now = new Date();
      this.logs.unshift({
        time: now.toLocaleTimeString(),
        message,
        type
      });
      
      // 限制日志数量
      if (this.logs.length > 100) {
        this.logs = this.logs.slice(0, 100);
      }
    }
  }
}
</script>

<style scoped>
.twain-demo {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
}

.status-bar {
  padding: 10px;
  border-radius: 5px;
  margin-bottom: 20px;
  background-color: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

.status-bar.connected {
  background-color: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.controls {
  margin-bottom: 20px;
}

.controls button {
  margin-right: 10px;
  margin-bottom: 10px;
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  background-color: #007bff;
  color: white;
  cursor: pointer;
}

.controls button:disabled {
  background-color: #6c757d;
  cursor: not-allowed;
}

.progress {
  margin: 20px 0;
  padding: 15px;
  background-color: #f8f9fa;
  border-radius: 5px;
}

.progress-bar {
  width: 100%;
  height: 20px;
  background-color: #e9ecef;
  border-radius: 10px;
  overflow: hidden;
  margin: 10px 0;
}

.progress-fill {
  height: 100%;
  background-color: #28a745;
  transition: width 0.3s ease;
}

.log-container {
  height: 200px;
  overflow-y: auto;
  border: 1px solid #ddd;
  padding: 10px;
  background-color: #f8f9fa;
  font-family: monospace;
  font-size: 12px;
}

.log-info { color: #0066cc; }
.log-success { color: #28a745; }
.log-warning { color: #ffc107; }
.log-error { color: #dc3545; }
</style>
```

#### 3.2 Vuex 状态管理集成

```javascript
// store/modules/twain.js
const state = {
  sdk: null,
  isConnected: false,
  printers: [],
  scanners: [],
  logs: []
}

const mutations = {
  SET_SDK(state, sdk) {
    state.sdk = sdk
  },
  SET_CONNECTION_STATUS(state, status) {
    state.isConnected = status
  },
  SET_PRINTERS(state, printers) {
    state.printers = printers
  },
  SET_SCANNERS(state, scanners) {
    state.scanners = scanners
  },
  ADD_LOG(state, log) {
    state.logs.unshift({
      ...log,
      time: new Date().toLocaleTimeString()
    })
    if (state.logs.length > 100) {
      state.logs = state.logs.slice(0, 100)
    }
  }
}

const actions = {
  async initSDK({ commit, dispatch }) {
    try {
      const sdk = new TwainMiddlewareSDK({
        host: 'localhost',
        port: 45677,
        debug: true
      })
      
      // 设置事件监听器
      sdk.on('connected', () => {
        commit('SET_CONNECTION_STATUS', true)
        commit('ADD_LOG', { message: '连接成功', type: 'success' })
      })
      
      sdk.on('disconnected', () => {
        commit('SET_CONNECTION_STATUS', false)
        commit('ADD_LOG', { message: '连接断开', type: 'warning' })
      })
      
      commit('SET_SDK', sdk)
      commit('ADD_LOG', { message: 'SDK初始化成功', type: 'success' })
      
      return sdk
    } catch (error) {
      commit('ADD_LOG', { message: `SDK初始化失败: ${error.message}`, type: 'error' })
      throw error
    }
  },
  
  async connect({ state, commit }) {
    try {
      await state.sdk.connect()
    } catch (error) {
      commit('ADD_LOG', { message: `连接失败: ${error.message}`, type: 'error' })
      throw error
    }
  },
  
  async getPrinters({ state, commit }) {
    try {
      const printers = await state.sdk.getPrinters()
      commit('SET_PRINTERS', printers)
      commit('ADD_LOG', { message: `获取到 ${printers.length} 个打印机`, type: 'success' })
      return printers
    } catch (error) {
      commit('ADD_LOG', { message: `获取打印机失败: ${error.message}`, type: 'error' })
      throw error
    }
  }
}

export default {
  namespaced: true,
  state,
  mutations,
  actions
}
```

## 🔧 TypeScript 支持（可选）

如果项目使用TypeScript，可以这样配置：

### 1. 类型声明

```typescript
// src/types/twain.d.ts
declare class TwainMiddlewareSDK {
  constructor(options?: TwainSDKOptions);
  connect(): Promise<void>;
  disconnect(): void;
  getPrinters(): Promise<PrinterInfo[]>;
  printPdf(options: PrintOptions): Promise<PrintResponse>;
  printPdfAsync(options: PrintOptions, callback?: PrintProgressCallback): Promise<PrintResult>;
  on(event: string, callback: Function): void;
  off(event: string, callback: Function): void;
  // ... 其他方法
}

// 将TwainMiddlewareSDK声明为全局变量
declare global {
  const TwainMiddlewareSDK: typeof TwainMiddlewareSDK;
}
```

### 2. Vue组件中使用

```vue
<script lang="ts">
import { Component, Vue } from 'vue-property-decorator'

@Component
export default class TwainDemo extends Vue {
  private twainSDK: TwainMiddlewareSDK | null = null
  private isConnected: boolean = false
  private printers: PrinterInfo[] = []
  
  mounted() {
    this.initSDK()
  }
  
  private initSDK(): void {
    this.twainSDK = new TwainMiddlewareSDK({
      host: 'localhost',
      port: 45677,
      debug: true
    })
  }
  
  private async getPrinters(): Promise<void> {
    if (!this.twainSDK) return
    
    try {
      this.printers = await this.twainSDK.getPrinters()
    } catch (error) {
      console.error('获取打印机失败:', error)
    }
  }
}
</script>
```

## ✅ 功能确认清单

- ✅ **打印功能完整** - 同步/异步打印、进度监控
- ✅ **扫描功能完整** - 设备管理、参数设置、图像获取  
- ✅ **连接管理完整** - 自动重连、状态监控、错误处理
- ✅ **事件系统完整** - 完整的事件监听和回调机制
- ✅ **TypeScript支持** - 完整的类型定义文件
- ✅ **Vue2兼容** - 可直接在Vue2项目中使用
- ✅ **示例代码完整** - 包含详细的使用示例

## 📞 技术支持

如有问题请参考：
1. `example.html` - 完整的使用示例
2. `TwainMiddlewareSDK.d.ts` - TypeScript类型定义
3. 程序日志文件 - 运行时错误信息

---

**现在就可以在Vue2项目中愉快地使用所有功能了！** 🎉