using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using NTwain;
using NTwain.Data;

namespace TwainMiddleware
{
    /// <summary>
    /// TWAIN扫描仪服务（真实TWAIN操作）
    /// </summary>
    public class TwainService : IDisposable
    {
        private TwainSession session;
        private DataSource currentDataSource;
        private readonly object lockObject = new object();
        private bool isInitialized = false;

        /// <summary>
        /// 初始化TWAIN服务
        /// </summary>
        public void Initialize()
        {
            lock (lockObject)
            {
                try
                {
                    if (!isInitialized)
                    {
                        Logger.Info("正在初始化TWAIN服务...");
                        
                        // 检查TWAIN DSM是否可用
                        if (!IsTwainDsmAvailable())
                        {
                            var errorMsg = "TWAIN Data Source Manager (DSM) 未找到。\n\n" +
                                         "解决方案：\n" +
                                         "1. 运行程序目录中的 setup.bat 进行自动配置\n" +
                                         "2. 从 https://www.twain.org/downloads/ 下载并安装 TWAIN DSM\n" +
                                         "3. 将 twaindsm.dll 复制到程序目录\n\n" +
                                         "详细说明请查看 docs/部署说明.md";
                            Logger.Error(errorMsg);
                            throw new Exception(errorMsg);
                        }
                        
                        // 创建TWAIN会话
                        session = new TwainSession(TWIdentity.CreateFromAssembly(DataGroups.Image, 
                            System.Reflection.Assembly.GetExecutingAssembly()));
                        
                        // 打开TWAIN会话
                        var result = session.Open();
                        if (result != ReturnCode.Success)
                        {
                            throw new Exception("打开TWAIN会话失败: " + result.ToString());
                        }
                        
                        // 配置TWAIN事件处理
                        session.TransferReady += Session_TransferReady;
                        session.DataTransferred += Session_DataTransferred;
                        session.TransferError += Session_TransferError;
                        
                        isInitialized = true;
                        Logger.Info("✅ TWAIN服务初始化成功");
                    }
                }
                catch (Exception ex)
                {
                    isInitialized = false;
                    
                    // 检查是否是TWAIN DSM相关错误
                    if (ex.Message.Contains("twaindsm") || ex.Message.Contains("0x8007007E"))
                    {
                        var enhancedMsg = "❌ TWAIN DSM 加载失败\n\n" +
                                        "错误详情: " + ex.Message + "\n\n" +
                                        "这通常是因为目标电脑缺少 TWAIN Data Source Manager。\n\n" +
                                        "快速解决方案：\n" +
                                        "1. 运行程序目录中的 setup.bat\n" +
                                        "2. 或手动安装 TWAIN DSM\n\n" +
                                        "详细说明请查看 docs/部署说明.md";
                        Logger.Error(enhancedMsg);
                        throw new Exception(enhancedMsg);
                    }
                    
                    Logger.Error("TWAIN服务初始化失败: " + ex.Message, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// 检查TWAIN DSM是否可用
        /// </summary>
        private bool IsTwainDsmAvailable()
        {
            try
            {
                // 检查当前目录
                if (System.IO.File.Exists("twaindsm.dll"))
                {
                    Logger.Debug("在当前目录找到 twaindsm.dll");
                    return true;
                }

                // 检查系统目录
                var systemPaths = new[]
                {
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "twaindsm.dll"),
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "twaindsm.dll")
                };

                foreach (var path in systemPaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        Logger.Debug("在系统目录找到 twaindsm.dll: " + path);
                        return true;
                    }
                }

                Logger.Warning("未找到 twaindsm.dll");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("检查 TWAIN DSM 时发生错误: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取可用扫描仪列表
        /// </summary>
        /// <returns>扫描仪名称列表</returns>
        public List<string> GetScanners()
        {
            lock (lockObject)
            {
                try
                {
                    EnsureInitialized();
                    
                    var scanners = new List<string>();
                    
                    // 获取所有数据源
                    var dataSources = session.GetSources();
                    
                    foreach (var dataSource in dataSources)
                    {
                        scanners.Add(dataSource.Name);
                    }
                    
                    if (scanners.Count == 0)
                    {
                        Logger.Warning("未找到任何TWAIN扫描仪设备");
                        return new List<string> { "未找到扫描仪设备" };
                    }
                    
                    Logger.Info("找到 " + scanners.Count.ToString() + " 个扫描仪设备: " + string.Join(", ", scanners));
                    return scanners;
                }
                catch (Exception ex)
                {
                    Logger.Error("获取扫描仪列表失败: " + ex.Message, ex);
                    return new List<string> { "获取扫描仪失败: " + ex.Message };
                }
            }
        }

        /// <summary>
        /// 执行扫描
        /// </summary>
        /// <param name="options">扫描选项</param>
        /// <returns>扫描结果</returns>
        public ScanResult Scan(ScanOptions options)
        {
            lock (lockObject)
            {
                try
                {
                    EnsureInitialized();
                    Logger.Info("开始扫描，参数: " + options.ToString());

                    // 获取扫描仪数据源
                    var dataSource = GetDataSource(options.ScannerName);
                    if (dataSource == null)
                    {
                        return new ScanResult
                        {
                            Success = false,
                            Message = "未找到扫描仪: " + options.ScannerName,
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        };
                    }

                    // 打开数据源
                    var openResult = dataSource.Open();
                    if (openResult != ReturnCode.Success)
                    {
                        return new ScanResult
                        {
                            Success = false,
                            Message = "打开扫描仪失败: " + openResult.ToString(),
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        };
                    }

                    currentDataSource = dataSource;

                    try
                    {
                        // 配置扫描参数
                        ConfigureScanSettings(dataSource, options);

                        // 启用数据源
                        var enableResult = dataSource.Enable(options.ShowUI ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI, false, IntPtr.Zero);
                        if (enableResult != ReturnCode.Success)
                        {
                            return new ScanResult
                            {
                                Success = false,
                                Message = "启用扫描仪失败: " + enableResult.ToString(),
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            };
                        }

                        // 等待扫描完成
                        var scanResult = WaitForScanCompletion();
                        return scanResult;
                    }
                    finally
                    {
                        // 关闭数据源
                        if (dataSource.IsOpen)
                        {
                            dataSource.Close();
                        }
                        currentDataSource = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("扫描失败: " + ex.Message, ex);
                    return new ScanResult
                    {
                        Success = false,
                        Message = ex.Message,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                }
            }
        }

        /// <summary>
        /// 获取数据源
        /// </summary>
        private DataSource GetDataSource(string scannerName)
        {
            try
            {
                var dataSources = session.GetSources();
                
                if (string.IsNullOrEmpty(scannerName) || scannerName == "默认扫描仪")
                {
                    return dataSources.FirstOrDefault();
                }
                
                return dataSources.FirstOrDefault(ds => ds.Name.Equals(scannerName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Logger.Error("获取数据源失败: " + ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 配置扫描设置
        /// </summary>
        private void ConfigureScanSettings(DataSource dataSource, ScanOptions options)
        {
            try
            {
                Logger.Debug("配置扫描参数...");

                // 设置分辨率
                if (options.Resolution > 0)
                {
                    var xRes = dataSource.Capabilities.ICapXResolution;
                    var yRes = dataSource.Capabilities.ICapYResolution;
                    
                    if (xRes.IsSupported)
                    {
                        xRes.SetValue(options.Resolution);
                    }
                    
                    if (yRes.IsSupported)
                    {
                        yRes.SetValue(options.Resolution);
                    }
                }

                // 设置颜色模式
                if (!string.IsNullOrEmpty(options.ColorMode))
                {
                    var pixelType = dataSource.Capabilities.ICapPixelType;
                    if (pixelType.IsSupported)
                    {
                        switch (options.ColorMode.ToLower())
                        {
                            case "color":
                                pixelType.SetValue(PixelType.RGB);
                                break;
                            case "gray":
                                pixelType.SetValue(PixelType.Gray);
                                break;
                            case "blackwhite":
                                pixelType.SetValue(PixelType.BlackWhite);
                                break;
                        }
                    }
                }

                // 设置亮度
                if (options.Brightness != 0)
                {
                    var brightness = dataSource.Capabilities.ICapBrightness;
                    if (brightness.IsSupported)
                    {
                        brightness.SetValue(options.Brightness);
                    }
                }

                // 设置对比度
                if (options.Contrast != 0)
                {
                    var contrast = dataSource.Capabilities.ICapContrast;
                    if (contrast.IsSupported)
                    {
                        contrast.SetValue(options.Contrast);
                    }
                }

                // 设置自动旋转（如果支持）
                if (options.AutoRotate)
                {
                    try
                    {
                        // 尝试设置自动旋转，不同厂商的属性名可能不同
                        var autoRotate = dataSource.Capabilities.ICapAutomaticRotate;
                        if (autoRotate.IsSupported)
                        {
                            autoRotate.SetValue(BoolType.True);
                        }
                    }
                    catch (Exception)
                    {
                        // 如果属性不支持，忽略错误
                        Logger.Debug("扫描仪不支持自动旋转功能");
                    }
                }

                Logger.Debug("扫描参数配置完成");
            }
            catch (Exception ex)
            {
                Logger.Warning("配置扫描参数时出现警告: " + ex.Message);
            }
        }

        private ScanResult lastScanResult;
        private bool scanCompleted = false;

        /// <summary>
        /// 等待扫描完成
        /// </summary>
        private ScanResult WaitForScanCompletion()
        {
            scanCompleted = false;
            lastScanResult = null;

            // 等待扫描完成（最多等待30秒）
            int timeout = 30000; // 30秒
            int elapsed = 0;
            int interval = 100;

            while (!scanCompleted && elapsed < timeout)
            {
                System.Threading.Thread.Sleep(interval);
                elapsed += interval;
                
                // 处理Windows消息
                System.Windows.Forms.Application.DoEvents();
            }

            if (!scanCompleted)
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "扫描超时",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }

            return lastScanResult ?? new ScanResult
            {
                Success = false,
                Message = "扫描结果未知",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        /// <summary>
        /// 传输准备事件
        /// </summary>
        private void Session_TransferReady(object sender, TransferReadyEventArgs e)
        {
            Logger.Debug("扫描传输准备就绪");
        }

        /// <summary>
        /// 数据传输事件
        /// </summary>
        private void Session_DataTransferred(object sender, DataTransferredEventArgs e)
        {
            try
            {
                Logger.Debug("接收到扫描数据");

                if (e.NativeData != IntPtr.Zero)
                {
                    // 从本机数据创建位图
                    var imageStream = e.GetNativeImageStream();
                    if (imageStream != null)
                    {
                        using (imageStream)
                        {
                            // 从流创建位图
                            using (var bitmap = new Bitmap(imageStream))
                            {
                                // 转换为Base64
                                string imageData = ConvertBitmapToBase64(bitmap, "png");
                                
                                lastScanResult = new ScanResult
                                {
                                    Success = true,
                                    Format = "png",
                                    Width = bitmap.Width,
                                    Height = bitmap.Height,
                                    ImageData = imageData,
                                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                    Message = "扫描完成"
                                };
                            }
                        }
                    }
                }
                else if (e.FileDataPath != null)
                {
                    // 从文件路径读取
                    if (File.Exists(e.FileDataPath))
                    {
                        byte[] fileBytes = File.ReadAllBytes(e.FileDataPath);
                        string imageData = Convert.ToBase64String(fileBytes);
                        
                        using (var bitmap = new Bitmap(e.FileDataPath))
                        {
                            lastScanResult = new ScanResult
                            {
                                Success = true,
                                Format = Path.GetExtension(e.FileDataPath).TrimStart('.').ToLower(),
                                Width = bitmap.Width,
                                Height = bitmap.Height,
                                ImageData = imageData,
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                Message = "扫描完成"
                            };
                        }
                    }
                }

                scanCompleted = true;
                Logger.Info("扫描数据处理完成");
            }
            catch (Exception ex)
            {
                Logger.Error("处理扫描数据失败: " + ex.Message, ex);
                lastScanResult = new ScanResult
                {
                    Success = false,
                    Message = "处理扫描数据失败: " + ex.Message,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                scanCompleted = true;
            }
        }

        /// <summary>
        /// 传输错误事件
        /// </summary>
        private void Session_TransferError(object sender, TransferErrorEventArgs e)
        {
            Logger.Error("扫描传输错误: " + (e.Exception != null ? e.Exception.Message : e.ReturnCode.ToString()));
            lastScanResult = new ScanResult
            {
                Success = false,
                Message = "扫描传输错误: " + (e.Exception != null ? e.Exception.Message : e.ReturnCode.ToString()),
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            scanCompleted = true;
        }

        /// <summary>
        /// 将位图转换为Base64字符串
        /// </summary>
        private string ConvertBitmapToBase64(Bitmap bitmap, string format)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    ImageFormat imageFormat = GetImageFormat(format);
                    bitmap.Save(ms, imageFormat);
                    byte[] imageBytes = ms.ToArray();
                    return Convert.ToBase64String(imageBytes);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("图像格式转换失败: " + ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// 获取图像格式
        /// </summary>
        private ImageFormat GetImageFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
                return ImageFormat.Png;

            string upperFormat = format.ToUpper();
            switch (upperFormat)
            {
                case "PNG":
                    return ImageFormat.Png;
                case "JPEG":
                case "JPG":
                    return ImageFormat.Jpeg;
                case "TIFF":
                case "TIF":
                    return ImageFormat.Tiff;
                case "BMP":
                    return ImageFormat.Bmp;
                default:
                    return ImageFormat.Png;
            }
        }

        /// <summary>
        /// 确保服务已初始化
        /// </summary>
        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("TWAIN服务尚未初始化");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (lockObject)
            {
                try
                {
                    if (currentDataSource != null && currentDataSource.IsOpen)
                    {
                        currentDataSource.Close();
                        currentDataSource = null;
                    }

                    if (session != null)
                    {
                        session.Close();
                        session = null;
                    }

                    isInitialized = false;
                    Logger.Info("TWAIN服务已释放");
                }
                catch (Exception ex)
                {
                    Logger.Error("释放TWAIN服务时出错: " + ex.Message, ex);
                }
            }
        }
    }

    /// <summary>
    /// 扫描选项
    /// </summary>
    public class ScanOptions
    {
        public string ScannerName { get; set; }
        public int Resolution { get; set; }
        public string ColorMode { get; set; }
        public string Format { get; set; }
        public bool ShowUI { get; set; }
        public int Brightness { get; set; }
        public int Contrast { get; set; }
        public bool AutoRotate { get; set; }
        public bool AutoCrop { get; set; }

        public ScanOptions()
        {
            ScannerName = "默认扫描仪";
            Resolution = 200;
            ColorMode = "color";
            Format = "png";
            ShowUI = false;
            Brightness = 0;
            Contrast = 0;
            AutoRotate = false;
            AutoCrop = false;
        }

        public override string ToString()
        {
            return "扫描仪=" + ScannerName + ", 分辨率=" + Resolution.ToString() + "DPI, 颜色=" + ColorMode + ", 格式=" + Format + ", 界面=" + ShowUI.ToString() + ", 亮度=" + Brightness.ToString() + ", 对比度=" + Contrast.ToString() + ", 自动旋转=" + AutoRotate.ToString() + ", 自动裁剪=" + AutoCrop.ToString();
        }
    }

    /// <summary>
    /// 扫描结果
    /// </summary>
    public class ScanResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Format { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ImageData { get; set; }
        public string Timestamp { get; set; }
    }
}
