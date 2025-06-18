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
    /// TWAIN扫描仪服务类
    /// </summary>
    public class TwainService : IDisposable
    {
        private TwainSession session;
        private DataSource currentDataSource;
        private bool isInitialized = false;
        private readonly object lockObject = new object();

        /// <summary>
        /// 初始化TWAIN服务
        /// </summary>
        public void Initialize()
        {
            lock (lockObject)
            {
                if (isInitialized)
                    return;

                try
                {
                    // 初始化TWAIN会话
                    session = new TwainSession(TWIdentity.CreateFromAssembly(DataGroups.Image, 
                        System.Reflection.Assembly.GetExecutingAssembly()));
                    
                    session.Open();
                    Logger.Info("TWAIN会话已打开");

                    isInitialized = true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"TWAIN服务初始化失败: {ex.Message}", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// 获取可用的扫描仪列表
        /// </summary>
        /// <returns>扫描仪名称列表</returns>
        public List<string> GetScanners()
        {
            lock (lockObject)
            {
                EnsureInitialized();

                try
                {
                    var scanners = new List<string>();
                    
                    foreach (var source in session.GetSources())
                    {
                        scanners.Add(source.Name);
                    }

                    Logger.Debug($"发现 {scanners.Count} 个扫描仪设备");
                    return scanners;
                }
                catch (Exception ex)
                {
                    Logger.Error($"获取扫描仪列表失败: {ex.Message}", ex);
                    return new List<string>();
                }
            }
        }

        /// <summary>
        /// 执行扫描操作
        /// </summary>
        /// <param name="options">扫描选项</param>
        /// <returns>扫描结果</returns>
        public ScanResult Scan(ScanOptions options)
        {
            lock (lockObject)
            {
                EnsureInitialized();

                try
                {
                    Logger.Info($"开始扫描，参数: {options}");

                    // 选择数据源
                    SelectDataSource(options.ScannerName);

                    // 配置扫描参数
                    ConfigureDataSource(options);

                    // 执行扫描
                    var result = PerformScan(options);

                    Logger.Info("扫描完成");
                    return result;
                }
                catch (Exception ex)
                {
                    Logger.Error($"扫描失败: {ex.Message}", ex);
                    throw;
                }
                finally
                {
                    // 关闭数据源
                    if (currentDataSource != null && currentDataSource.IsOpen)
                    {
                        currentDataSource.Close();
                    }
                }
            }
        }

        /// <summary>
        /// 选择数据源
        /// </summary>
        private void SelectDataSource(string scannerName)
        {
            try
            {
                var sources = session.GetSources().ToList();
                
                if (string.IsNullOrEmpty(scannerName) || scannerName == "默认扫描仪")
                {
                    currentDataSource = sources.FirstOrDefault();
                }
                else
                {
                    currentDataSource = sources.FirstOrDefault(s => s.Name.Contains(scannerName));
                    if (currentDataSource == null)
                    {
                        currentDataSource = sources.FirstOrDefault();
                    }
                }

                if (currentDataSource == null)
                {
                    throw new InvalidOperationException("未找到可用的扫描仪设备");
                }

                var rc = currentDataSource.Open();
                if (rc != ReturnCode.Success)
                {
                    throw new InvalidOperationException($"打开扫描仪失败: {rc}");
                }

                Logger.Debug($"已选择扫描仪: {currentDataSource.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"选择数据源失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 配置数据源参数
        /// </summary>
        private void ConfigureDataSource(ScanOptions options)
        {
            try
            {
                // 设置分辨率
                if (options.Resolution > 0)
                {
                    currentDataSource.Capabilities.XResolution.SetValue(options.Resolution);
                    currentDataSource.Capabilities.YResolution.SetValue(options.Resolution);
                }

                // 设置颜色模式
                switch (options.ColorMode?.ToLower())
                {
                    case "color":
                        currentDataSource.Capabilities.PixelType.SetValue(PixelType.RGB);
                        break;
                    case "gray":
                        currentDataSource.Capabilities.PixelType.SetValue(PixelType.Gray);
                        break;
                    case "blackwhite":
                        currentDataSource.Capabilities.PixelType.SetValue(PixelType.BlackWhite);
                        break;
                }

                // 设置亮度
                if (options.Brightness != 0)
                {
                    var brightness = Math.Max(-1000, Math.Min(1000, options.Brightness));
                    currentDataSource.Capabilities.Brightness.SetValue(brightness);
                }

                // 设置对比度
                if (options.Contrast != 0)
                {
                    var contrast = Math.Max(-1000, Math.Min(1000, options.Contrast));
                    currentDataSource.Capabilities.Contrast.SetValue(contrast);
                }

                // 设置是否显示UI
                currentDataSource.Capabilities.ShowUI.SetValue(options.ShowUI);

                Logger.Debug("扫描参数配置完成");
            }
            catch (Exception ex)
            {
                Logger.Warning($"配置扫描参数时发生警告: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行扫描
        /// </summary>
        private ScanResult PerformScan(ScanOptions options)
        {
            try
            {
                var rc = currentDataSource.Enable(SourceEnableMode.NoUI, false, IntPtr.Zero);
                if (rc != ReturnCode.Success)
                {
                    throw new InvalidOperationException($"启用数据源失败: {rc}");
                }

                // 获取图像数据
                var transfers = currentDataSource.GetDataTransfers().ToList();
                if (!transfers.Any())
                {
                    throw new InvalidOperationException("未获取到扫描图像");
                }

                var imageData = transfers.First();
                
                // 转换为所需格式
                string base64Data = ConvertImageToBase64(imageData, options.Format);

                return new ScanResult
                {
                    Success = true,
                    Format = options.Format,
                    Width = imageData.ImageInfo.ImageWidth,
                    Height = imageData.ImageInfo.ImageLength,
                    ImageData = base64Data,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"执行扫描失败: {ex.Message}", ex);
                return new ScanResult
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
        }

        /// <summary>
        /// 将图像转换为Base64字符串
        /// </summary>
        private string ConvertImageToBase64(ImageData imageData, string format)
        {
            try
            {
                using (var bitmap = imageData.GetNativeImage())
                {
                    using (var ms = new MemoryStream())
                    {
                        ImageFormat imageFormat = GetImageFormat(format);
                        bitmap.Save(ms, imageFormat);
                        byte[] imageBytes = ms.ToArray();
                        return Convert.ToBase64String(imageBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"图像转换失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 获取图像格式
        /// </summary>
        private ImageFormat GetImageFormat(string format)
        {
            switch (format?.ToUpper())
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
                throw new InvalidOperationException("TWAIN服务未初始化");
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

                    if (session != null && session.IsOpened)
                    {
                        session.Close();
                        session = null;
                    }

                    isInitialized = false;
                    Logger.Info("TWAIN服务已释放");
                }
                catch (Exception ex)
                {
                    Logger.Error($"释放TWAIN服务失败: {ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>
    /// 扫描选项
    /// </summary>
    public class ScanOptions
    {
        public string ScannerName { get; set; } = "默认扫描仪";
        public int Resolution { get; set; } = 300;
        public string ColorMode { get; set; } = "Color";
        public string Format { get; set; } = "PNG";
        public bool ShowUI { get; set; } = false;
        public int Brightness { get; set; } = 0;
        public int Contrast { get; set; } = 0;
        public bool AutoRotate { get; set; } = false;
        public bool AutoCrop { get; set; } = false;

        public override string ToString()
        {
            return $"Scanner={ScannerName}, Resolution={Resolution}, ColorMode={ColorMode}, Format={Format}";
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