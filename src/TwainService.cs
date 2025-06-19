using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using NTwain;
using NTwain.Data;
using System.Management;
using PdfiumViewer;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwainMiddleware
{
    /// <summary>
    /// Base64字符串到byte[]数组的JSON转换器
    /// </summary>
    public class Base64JsonConverter : JsonConverter<byte[]>
    {
        public override byte[] ReadJson(JsonReader reader, Type objectType, byte[] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.String)
            {
                try
                {
                    string base64String = (string)reader.Value;
                    if (string.IsNullOrEmpty(base64String))
                        return null;

                    // 移除可能的data URL前缀
                    if (base64String.Contains(","))
                    {
                        base64String = base64String.Split(',')[1];
                    }

                    return Convert.FromBase64String(base64String);
                }
                catch (Exception ex)
                {
                    Logger.Error("Base64字符串解码失败: " + ex.Message, ex);
                    throw new JsonException("Base64字符串解码失败: " + ex.Message, ex);
                }
            }

            throw new JsonException("无法将 " + reader.TokenType + " 转换为 byte[]");
        }

        public override void WriteJson(JsonWriter writer, byte[] value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(Convert.ToBase64String(value));
            }
        }
    }

    /// <summary>
    /// TWAIN兼容性检测结果
    /// </summary>
    public class TwainCompatibilityResult
    {
        /// <summary>
        /// NTwain库是否可用
        /// </summary>
        public bool NTwainLibraryAvailable { get; set; }

        /// <summary>
        /// NTwain库错误信息
        /// </summary>
        public string NTwainLibraryError { get; set; }

        /// <summary>
        /// TWAIN DSM是否可用
        /// </summary>
        public bool TwainDsmAvailable { get; set; }

        /// <summary>
        /// TWAIN DSM错误信息
        /// </summary>
        public string TwainDsmError { get; set; }

        /// <summary>
        /// TWAIN会话是否可用
        /// </summary>
        public bool TwainSessionAvailable { get; set; }

        /// <summary>
        /// TWAIN会话错误信息
        /// </summary>
        public string TwainSessionError { get; set; }

        /// <summary>
        /// 是否完全兼容
        /// </summary>
        public bool IsFullyCompatible { get; set; }

        /// <summary>
        /// 一般错误信息
        /// </summary>
        public string GeneralError { get; set; }

        /// <summary>
        /// 兼容性建议
        /// </summary>
        public List<string> Recommendations { get; set; }

        public TwainCompatibilityResult()
        {
            Recommendations = new List<string>();
        }
    }

    /// <summary>
    /// TWAIN扫描仪服务（真实TWAIN操作）
    /// </summary>
    public class TwainService : IDisposable
    {
        private TwainSession session;
        private DataSource currentDataSource;
        private readonly object lockObject = new object();
        private bool isInitialized = false;
        private bool isTwainAvailable = false;
        private string twainUnavailableReason = "";

        /// <summary>
        /// 获取TWAIN是否可用
        /// </summary>
        public bool IsTwainAvailable 
        { 
            get { return isTwainAvailable; } 
        }

        /// <summary>
        /// 获取TWAIN不可用的原因
        /// </summary>
        public string TwainUnavailableReason 
        { 
            get { return twainUnavailableReason; } 
        }

        /// <summary>
        /// 检测TWAIN环境是否可用
        /// </summary>
        /// <returns>返回检测结果和详细信息</returns>
        public TwainCompatibilityResult CheckTwainCompatibility()
        {
            var result = new TwainCompatibilityResult();
            
            try
            {
                Logger.Info("正在检测TWAIN环境兼容性...");

                // 1. 检查NTwain库是否可用
                try
                {
                    var testIdentity = TWIdentity.CreateFromAssembly(DataGroups.Image, 
                        System.Reflection.Assembly.GetExecutingAssembly());
                    result.NTwainLibraryAvailable = true;
                    Logger.Debug("✅ NTwain库可用");
                }
                catch (Exception ex)
                {
                    result.NTwainLibraryAvailable = false;
                    result.NTwainLibraryError = ex.Message;
                    Logger.Warning("❌ NTwain库不可用: " + ex.Message);
                }

                // 2. 检查TWAIN DSM是否可用
                result.TwainDsmAvailable = IsTwainDsmAvailable();
                if (!result.TwainDsmAvailable)
                {
                    result.TwainDsmError = "未找到 twaindsm.dll";
                    Logger.Warning("❌ TWAIN DSM不可用");
                }
                else
                {
                    Logger.Debug("✅ TWAIN DSM可用");
                }

                // 3. 尝试创建TWAIN会话（仅在DSM可用时）
                if (result.TwainDsmAvailable && result.NTwainLibraryAvailable)
                {
                    TwainSession testSession = null;
                    try
                    {
                        testSession = new TwainSession(TWIdentity.CreateFromAssembly(DataGroups.Image, 
                            System.Reflection.Assembly.GetExecutingAssembly()));
                        
                        var openResult = testSession.Open();
                        if (openResult == ReturnCode.Success)
                        {
                            result.TwainSessionAvailable = true;
                            testSession.Close();
                            Logger.Debug("✅ TWAIN会话创建成功");
                        }
                        else
                        {
                            result.TwainSessionAvailable = false;
                            result.TwainSessionError = "无法打开TWAIN会话: " + openResult.ToString();
                            Logger.Warning("❌ TWAIN会话创建失败: " + openResult.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        result.TwainSessionAvailable = false;
                        result.TwainSessionError = ex.Message;
                        Logger.Warning("❌ TWAIN会话测试失败: " + ex.Message);
                    }
                    finally
                    {
                        // 确保测试会话被正确清理
                        if (testSession != null)
                        {
                            try
                            {
                                // 尝试关闭会话，忽略状态检查
                                testSession.Close();
                            }
                            catch (Exception)
                            {
                                // 忽略清理时的异常
                            }
                        }
                    }
                }
                else
                {
                    result.TwainSessionAvailable = false;
                    result.TwainSessionError = "前置条件不满足";
                }

                // 4. 设置整体兼容性状态
                result.IsFullyCompatible = result.NTwainLibraryAvailable && 
                                         result.TwainDsmAvailable && 
                                         result.TwainSessionAvailable;

                // 5. 生成建议
                result.Recommendations = GenerateCompatibilityRecommendations(result);

                if (result.IsFullyCompatible)
                {
                    Logger.Info("✅ TWAIN环境完全兼容");
                    isTwainAvailable = true;
                    twainUnavailableReason = "";
                }
                else
                {
                    var reasons = new List<string>();
                    if (!result.NTwainLibraryAvailable) reasons.Add("NTwain库不可用");
                    if (!result.TwainDsmAvailable) reasons.Add("TWAIN DSM不可用");
                    if (!result.TwainSessionAvailable) reasons.Add("TWAIN会话不可用");
                    
                    twainUnavailableReason = string.Join(", ", reasons);
                    isTwainAvailable = false;
                    Logger.Warning("⚠️ TWAIN环境不完全兼容: " + twainUnavailableReason);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("TWAIN兼容性检测失败: " + ex.Message, ex);
                result.IsFullyCompatible = false;
                result.GeneralError = ex.Message;
                isTwainAvailable = false;
                twainUnavailableReason = "检测失败: " + ex.Message;
                return result;
            }
        }

        /// <summary>
        /// 生成兼容性建议
        /// </summary>
        private List<string> GenerateCompatibilityRecommendations(TwainCompatibilityResult result)
        {
            var recommendations = new List<string>();

            if (!result.TwainDsmAvailable)
            {
                recommendations.Add("安装TWAIN DSM: 运行 setup.bat 或从 https://www.twain.org/downloads/ 下载");
                recommendations.Add("将 twaindsm.dll 复制到程序目录或系统目录");
            }

            if (!result.NTwainLibraryAvailable)
            {
                recommendations.Add("检查NTwain库依赖是否正确部署");
                recommendations.Add("确保程序运行在正确的.NET Framework版本上");
            }

            if (!result.TwainSessionAvailable && result.TwainDsmAvailable)
            {
                recommendations.Add("检查是否有扫描仪驱动已安装");
                recommendations.Add("确保扫描仪设备正确连接并开机");
                recommendations.Add("尝试重新安装扫描仪驱动程序");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("TWAIN环境运行正常，扫描功能可用");
            }
            else
            {
                recommendations.Insert(0, "程序将在无扫描功能模式下运行，仅提供打印功能");
            }

            return recommendations;
        }

        /// <summary>
        /// 初始化TWAIN服务（支持优雅降级）
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
                        
                        // 首先进行兼容性检测
                        var compatibilityResult = CheckTwainCompatibility();
                        
                        if (!compatibilityResult.IsFullyCompatible)
                        {
                            Logger.Warning("TWAIN环境不完全兼容，程序将在受限模式下运行");
                            Logger.Info("建议:");
                            foreach (var recommendation in compatibilityResult.Recommendations)
                            {
                                Logger.Info("  - " + recommendation);
                            }
                            
                            // 即使TWAIN不可用，我们也标记为已初始化，这样程序可以继续运行其他功能
                            isInitialized = true;
                            isTwainAvailable = false;
                            Logger.Info("✅ TWAIN服务已初始化（受限模式 - 无扫描功能）");
                            return;
                        }
                        
                        // TWAIN环境可用，进行正常初始化
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
                        isTwainAvailable = true;
                        Logger.Info("✅ TWAIN服务初始化成功（完整模式 - 支持扫描功能）");
                    }
                }
                catch (Exception ex)
                {
                    isInitialized = true; // 标记为已初始化，但功能受限
                    isTwainAvailable = false;
                    
                    // 检查是否是TWAIN DSM相关错误
                    if (ex.Message.Contains("twaindsm") || ex.Message.Contains("0x8007007E"))
                    {
                        twainUnavailableReason = "TWAIN DSM不可用";
                        Logger.Warning("❌ TWAIN DSM 加载失败，程序将在受限模式下运行");
                        Logger.Info("建议解决方案:");
                        Logger.Info("  1. 运行程序目录中的 setup.bat");
                        Logger.Info("  2. 或手动安装 TWAIN DSM");
                        Logger.Info("  详细说明请查看 docs/部署说明.md");
                    }
                    else
                    {
                        twainUnavailableReason = ex.Message;
                        Logger.Warning("❌ TWAIN服务初始化失败: " + ex.Message);
                    }
                    
                    Logger.Info("✅ 程序已启动（受限模式 - 无扫描功能，支持打印功能）");
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
                    
                    // 如果TWAIN不可用，返回空列表
                    if (!isTwainAvailable)
                    {
                        Logger.Warning("TWAIN不可用，无法获取扫描仪列表: " + twainUnavailableReason);
                        return new List<string>();
                    }
                    
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
        /// 获取系统中安装的打印机列表（真实打印机，非虚拟打印机）
        /// </summary>
        /// <returns>打印机信息列表</returns>
        public List<PrinterInfo> GetPrinters()
        {
            var printers = new List<PrinterInfo>();
            
            try
            {
                Logger.Info("正在获取系统打印机列表...");
                
                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    try
                    {
                        var printerSettings = new PrinterSettings();
                        printerSettings.PrinterName = printerName;
                        
                        // 检查打印机是否有效且可用
                        if (printerSettings.IsValid)
                        {
                            // 过滤掉虚拟打印机（如PDF打印机、传真等）
                            if (!IsVirtualPrinter(printerName))
                            {
                                var printerInfo = new PrinterInfo
                                {
                                    Name = printerName,
                                    IsDefault = printerSettings.IsDefaultPrinter,
                                    Status = GetPrinterStatus(printerSettings),
                                    CanDuplex = printerSettings.CanDuplex,
                                    MaximumCopies = printerSettings.MaximumCopies,
                                    SupportsColor = printerSettings.SupportsColor,
                                    PaperSizes = GetPaperSizes(printerSettings)
                                };
                                
                                // 获取打印机技术类型
                                GetPrinterTypeInfo(printerName, printerInfo);
                                
                                printers.Add(printerInfo);
                                Logger.Debug("找到打印机: " + printerName + " (默认: " + printerInfo.IsDefault + ", 状态: " + printerInfo.Status + ", 类型: " + printerInfo.PrinterType + ")");
                            }
                            else
                            {
                                Logger.Debug("跳过虚拟打印机: " + printerName);
                            }
                        }
                        else
                        {
                            Logger.Debug("跳过无效打印机: " + printerName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("处理打印机 " + printerName + " 时发生错误: " + ex.Message);
                    }
                }
                
                Logger.Info("找到 " + printers.Count + " 个可用的真实打印机");
                
                if (printers.Count == 0)
                {
                    Logger.Warning("未找到任何可用的真实打印机");
                }
                
                return printers;
            }
            catch (Exception ex)
            {
                Logger.Error("获取打印机列表失败: " + ex.Message, ex);
                return new List<PrinterInfo> 
                { 
                    new PrinterInfo 
                    { 
                        Name = "获取失败: " + ex.Message,
                        Status = "错误"
                    } 
                };
            }
        }
        
        /// <summary>
        /// 检查是否为虚拟打印机
        /// </summary>
        private bool IsVirtualPrinter(string printerName)
        {
            string[] virtualPrinterKeywords = {
                "PDF", "XPS", "OneNote", "Fax", "传真", "Microsoft Print to PDF", 
                "Microsoft XPS Document Writer", "Send To OneNote", "Snagit",
                "CutePDF", "PrimoPDF", "Virtual", "虚拟", "打印到文件"
            };
            
            string upperPrinterName = printerName.ToUpper();
            
            foreach (string keyword in virtualPrinterKeywords)
            {
                if (upperPrinterName.Contains(keyword.ToUpper()))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取打印机状态
        /// </summary>
        private string GetPrinterStatus(PrinterSettings printerSettings)
        {
            try
            {
                // 这里可以根据需要扩展状态检查逻辑
                if (printerSettings.IsValid)
                {
                    return "就绪";
                }
                else
                {
                    return "离线";
                }
            }
            catch
            {
                return "未知";
            }
        }
        
        /// <summary>
        /// 获取支持的纸张尺寸
        /// </summary>
        private List<string> GetPaperSizes(PrinterSettings printerSettings)
        {
            var paperSizes = new List<string>();
            
            try
            {
                foreach (PaperSize paperSize in printerSettings.PaperSizes)
                {
                    paperSizes.Add(paperSize.PaperName + " (" + paperSize.Width + "x" + paperSize.Height + ")");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("获取纸张尺寸失败: " + ex.Message);
            }
            
            return paperSizes;
        }

        /// <summary>
        /// 获取打印机技术类型信息
        /// </summary>
        private void GetPrinterTypeInfo(string printerName, PrinterInfo printerInfo)
        {
            try
            {
                Logger.Info("正在获取打印机 " + printerName + " 的技术类型信息...");
                
                // 方法1：尝试从Win32_Printer获取MarkingTechnology
                bool foundMarkingTech = TryGetMarkingTechnologyFromWMI(printerName, printerInfo);
                
                if (!foundMarkingTech)
                {
                    // 方法2：尝试从打印机名称推断类型
                    Logger.Info("WMI查询失败，尝试从打印机名称推断类型...");
                    InferPrinterTypeFromName(printerName, printerInfo);
                }
                
                Logger.Info("打印机 " + printerName + " 类型检测完成：" + printerInfo.PrinterType);
            }
            catch (Exception ex)
            {
                Logger.Error("获取打印机 " + printerName + " 技术类型时发生异常: " + ex.Message, ex);
                SetUnknownPrinterType(printerInfo);
            }
        }
        
        /// <summary>
        /// 尝试从WMI获取MarkingTechnology信息
        /// </summary>
        private bool TryGetMarkingTechnologyFromWMI(string printerName, PrinterInfo printerInfo)
        {
            try
            {
                // 转义打印机名称中的特殊字符
                string escapedName = printerName.Replace("'", "''").Replace("\\", "\\\\");
                string query = "SELECT MarkingTechnology, DriverName, Local, Network FROM Win32_Printer WHERE Name='" + escapedName + "'";
                
                Logger.Info("执行WMI查询: " + query);
                
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    var results = searcher.Get();
                    Logger.Info("WMI查询返回 " + results.Count + " 个结果");
                    
                    foreach (ManagementObject printer in results)
                    {
                        Logger.Info("找到打印机WMI对象，检查MarkingTechnology属性...");
                        
                        var markingTech = printer["MarkingTechnology"];
                        var driverName = printer["DriverName"];
                        var isLocal = printer["Local"];
                        var isNetwork = printer["Network"];
                        
                        Logger.Info("MarkingTechnology: " + (markingTech != null ? markingTech.ToString() : "null"));
                        Logger.Info("DriverName: " + (driverName != null ? driverName.ToString() : "null"));
                        Logger.Info("Local: " + (isLocal != null ? isLocal.ToString() : "null"));
                        Logger.Info("Network: " + (isNetwork != null ? isNetwork.ToString() : "null"));
                        
                        if (markingTech != null && markingTech != DBNull.Value)
                        {
                            uint techValue = Convert.ToUInt32(markingTech);
                            printerInfo.MarkingTechnology = techValue;
                            printerInfo.PrinterType = ConvertMarkingTechnologyToType(techValue);
                            printerInfo.PrinterTypeDescription = GetMarkingTechnologyDescription(techValue);
                            
                            Logger.Info("成功获取MarkingTechnology: " + techValue + " -> " + printerInfo.PrinterType);
                            return true;
                        }
                        else
                        {
                            Logger.Info("MarkingTechnology属性为空，尝试从驱动名称推断...");
                            if (driverName != null)
                            {
                                InferPrinterTypeFromDriverName(driverName.ToString(), printerInfo);
                                return true;
                            }
                        }
                        
                        break; // 只处理第一个结果
                    }
                }
                
                // 如果上面的查询没有结果，尝试列出所有打印机
                Logger.Info("特定查询无结果，尝试列出所有打印机进行匹配...");
                return TryGetMarkingTechFromAllPrinters(printerName, printerInfo);
            }
            catch (Exception ex)
            {
                Logger.Info("WMI查询异常: " + ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// 尝试从所有打印机中匹配并获取信息
        /// </summary>
        private bool TryGetMarkingTechFromAllPrinters(string targetPrinterName, PrinterInfo printerInfo)
        {
            try
            {
                string query = "SELECT Name, MarkingTechnology, DriverName FROM Win32_Printer";
                Logger.Info("执行全局打印机查询: " + query);
                
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject printer in searcher.Get())
                    {
                        var name = printer["Name"];
                        string nameStr = name != null ? name.ToString() : null;
                        if (nameStr != null && nameStr.Equals(targetPrinterName, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Info("找到匹配的打印机: " + nameStr);
                            
                            var markingTech = printer["MarkingTechnology"];
                            if (markingTech != null && markingTech != DBNull.Value)
                            {
                                uint techValue = Convert.ToUInt32(markingTech);
                                printerInfo.MarkingTechnology = techValue;
                                printerInfo.PrinterType = ConvertMarkingTechnologyToType(techValue);
                                printerInfo.PrinterTypeDescription = GetMarkingTechnologyDescription(techValue);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("全局打印机查询异常: " + ex.Message);
            }
            
            return false;
        }
        
        /// <summary>
        /// 从驱动名称推断打印机类型
        /// </summary>
        private void InferPrinterTypeFromDriverName(string driverName, PrinterInfo printerInfo)
        {
            string upperDriver = driverName.ToUpper();
            
            if (upperDriver.Contains("LASER") || upperDriver.Contains("激光"))
            {
                printerInfo.PrinterType = "激光打印机";
                printerInfo.PrinterTypeDescription = "根据驱动名称推断为激光打印机";
            }
            else if (upperDriver.Contains("INKJET") || upperDriver.Contains("喷墨") || upperDriver.Contains("INK"))
            {
                printerInfo.PrinterType = "喷墨打印机";
                printerInfo.PrinterTypeDescription = "根据驱动名称推断为喷墨打印机";
            }
            else if (upperDriver.Contains("DOT") || upperDriver.Contains("MATRIX") || upperDriver.Contains("针式") || upperDriver.Contains("24PIN") || upperDriver.Contains("9PIN"))
            {
                printerInfo.PrinterType = "针式打印机";
                printerInfo.PrinterTypeDescription = "根据驱动名称推断为针式打印机";
            }
            else if (upperDriver.Contains("THERMAL") || upperDriver.Contains("热敏") || upperDriver.Contains("热转印"))
            {
                printerInfo.PrinterType = "热敏打印机";
                printerInfo.PrinterTypeDescription = "根据驱动名称推断为热敏打印机";
            }
            else
            {
                printerInfo.PrinterType = "其他类型";
                printerInfo.PrinterTypeDescription = "无法从驱动名称确定类型: " + driverName;
            }
            
            printerInfo.MarkingTechnology = 0;
            Logger.Info("从驱动名称推断类型: " + driverName + " -> " + printerInfo.PrinterType);
        }
        
        /// <summary>
        /// 从打印机名称推断类型
        /// </summary>
        private void InferPrinterTypeFromName(string printerName, PrinterInfo printerInfo)
        {
            string upperName = printerName.ToUpper();
            
            // 激光打印机关键词
            if (upperName.Contains("LASER") || upperName.Contains("激光") || 
                upperName.Contains("LASERJET") || upperName.Contains("LJ"))
            {
                printerInfo.PrinterType = "激光打印机";
                printerInfo.PrinterTypeDescription = "根据设备名称推断为激光打印机";
            }
            // 喷墨打印机关键词
            else if (upperName.Contains("INKJET") || upperName.Contains("喷墨") || 
                     upperName.Contains("DESKJET") || upperName.Contains("PIXMA") || 
                     upperName.Contains("STYLUS") || upperName.Contains("INK"))
            {
                printerInfo.PrinterType = "喷墨打印机";
                printerInfo.PrinterTypeDescription = "根据设备名称推断为喷墨打印机";
            }
            // 针式打印机关键词
            else if (upperName.Contains("DOT") || upperName.Contains("MATRIX") || 
                     upperName.Contains("针式") || upperName.Contains("24PIN") || 
                     upperName.Contains("9PIN") || upperName.Contains("LQ") || 
                     upperName.Contains("FX"))
            {
                printerInfo.PrinterType = "针式打印机";
                printerInfo.PrinterTypeDescription = "根据设备名称推断为针式打印机";
            }
            // 热敏打印机关键词 (包括POS机、收据打印机等)
            else if (upperName.Contains("THERMAL") || upperName.Contains("热敏") || 
                     upperName.Contains("RECEIPT") || upperName.Contains("POS") ||
                     upperName.Contains("STONE") || upperName.Contains("CITIZEN") ||
                     upperName.Contains("ZEBRA") || upperName.Contains("TSC") ||
                     upperName.Contains("ARGOX") || upperName.Contains("GODEX") ||
                     upperName.Contains("DATAMAX") || upperName.Contains("SATO") ||
                     upperName.Contains("TOSHIBA TEC") || upperName.Contains("BIXOLON") ||
                     upperName.Contains("STAR") || upperName.Contains("EPSON TM") ||
                     upperName.Contains("80MM") || upperName.Contains("58MM") ||
                     (upperName.Contains("PRINTER") && (upperName.Contains("58") || upperName.Contains("80"))))
            {
                printerInfo.PrinterType = "热敏打印机";
                printerInfo.PrinterTypeDescription = "根据设备名称推断为热敏打印机";
            }
            // 多功能一体机
            else if (upperName.Contains("MFC") || upperName.Contains("一体机") || 
                     upperName.Contains("MULTIFUNCTION") || upperName.Contains("ALL-IN-ONE"))
            {
                printerInfo.PrinterType = "多功能一体机";
                printerInfo.PrinterTypeDescription = "根据设备名称推断为多功能一体机";
            }
            // 品牌特定识别
            else if (upperName.Contains("HP") || upperName.Contains("HEWLETT"))
            {
                if (upperName.Contains("DESKJET") || upperName.Contains("ENVY") || upperName.Contains("OFFICEJET"))
                {
                    printerInfo.PrinterType = "喷墨打印机";
                    printerInfo.PrinterTypeDescription = "根据HP品牌型号推断为喷墨打印机";
                }
                else
                {
                    printerInfo.PrinterType = "激光打印机";
                    printerInfo.PrinterTypeDescription = "根据HP品牌型号推断为激光打印机";
                }
            }
            else if (upperName.Contains("CANON"))
            {
                if (upperName.Contains("PIXMA") || upperName.Contains("G") || upperName.Contains("E"))
                {
                    printerInfo.PrinterType = "喷墨打印机";
                    printerInfo.PrinterTypeDescription = "根据Canon品牌型号推断为喷墨打印机";
                }
                else
                {
                    printerInfo.PrinterType = "激光打印机";
                    printerInfo.PrinterTypeDescription = "根据Canon品牌型号推断为激光打印机";
                }
            }
            else if (upperName.Contains("EPSON"))
            {
                if (upperName.Contains("L") || upperName.Contains("WORKFORCE") || upperName.Contains("EXPRESSION"))
                {
                    printerInfo.PrinterType = "喷墨打印机";
                    printerInfo.PrinterTypeDescription = "根据Epson品牌型号推断为喷墨打印机";
                }
                else if (upperName.Contains("LQ") || upperName.Contains("FX"))
                {
                    printerInfo.PrinterType = "针式打印机";
                    printerInfo.PrinterTypeDescription = "根据Epson品牌型号推断为针式打印机";
                }
                else if (upperName.Contains("TM"))
                {
                    printerInfo.PrinterType = "热敏打印机";
                    printerInfo.PrinterTypeDescription = "根据Epson TM系列推断为热敏打印机";
                }
                else
                {
                    printerInfo.PrinterType = "喷墨打印机";
                    printerInfo.PrinterTypeDescription = "根据Epson品牌推断为喷墨打印机";
                }
            }
            else if (upperName.Contains("BROTHER"))
            {
                if (upperName.Contains("DCP") || upperName.Contains("MFC"))
                {
                    printerInfo.PrinterType = "多功能一体机";
                    printerInfo.PrinterTypeDescription = "根据Brother品牌型号推断为多功能一体机";
                }
                else
                {
                    printerInfo.PrinterType = "激光打印机";
                    printerInfo.PrinterTypeDescription = "根据Brother品牌推断为激光打印机";
                }
            }
            else if (upperName.Contains("SAMSUNG") || upperName.Contains("XEROX") || 
                     upperName.Contains("KYOCERA") || upperName.Contains("RICOH"))
            {
                printerInfo.PrinterType = "激光打印机";
                printerInfo.PrinterTypeDescription = "根据品牌推断为激光打印机";
            }
            else
            {
                printerInfo.PrinterType = "未知类型";
                printerInfo.PrinterTypeDescription = "无法从设备名称确定类型";
            }
            
            printerInfo.MarkingTechnology = 0;
            Logger.Info("从设备名称推断类型: " + printerName + " -> " + printerInfo.PrinterType);
        }
        
        /// <summary>
        /// 设置为未知打印机类型
        /// </summary>
        private void SetUnknownPrinterType(PrinterInfo printerInfo)
        {
            printerInfo.PrinterType = "未知";
            printerInfo.PrinterTypeDescription = "无法确定打印机技术类型";
            printerInfo.MarkingTechnology = 0;
        }

        /// <summary>
        /// 转换标记技术代码为打印机类型
        /// </summary>
        private string ConvertMarkingTechnologyToType(uint markingTech)
        {
            switch (markingTech)
            {
                case 4: // Electrophotographic Laser
                    return "激光打印机";
                case 6: // Impact Moving Head Dot Matrix 9pin
                case 7: // Impact Moving Head Dot Matrix 24pin
                case 8: // Impact Moving Head Dot Matrix Other
                case 9: // Impact Moving Head Fully Formed
                case 10: // Impact Band
                case 11: // Impact Other
                    return "针式打印机";
                case 12: // Inkjet Aqueous
                case 13: // Inkjet Solid
                case 14: // Inkjet Other
                    return "喷墨打印机";
                case 16: // Thermal Transfer
                case 17: // Thermal Sensitive
                case 18: // Thermal Diffusion
                case 19: // Thermal Other
                    return "热敏打印机";
                case 3: // Electrophotographic LED
                    return "LED打印机";
                case 5: // Electrophotographic Other
                    return "激光打印机";
                case 15: // Pen
                    return "绘图仪";
                case 20: // Electroerosion
                case 21: // Electrostatic
                    return "静电打印机";
                case 22: // Photographic Microfiche
                case 23: // Photographic Imagesetter
                case 24: // Photographic Other
                    return "照相打印机";
                case 25: // Ion Deposition
                    return "离子沉积打印机";
                case 26: // eBeam
                    return "电子束打印机";
                case 27: // Typesetter
                    return "排版机";
                case 1: // Other
                case 2: // Unknown
                default:
                    return "其他类型";
            }
        }
        
        /// <summary>
        /// 获取标记技术的详细描述
        /// </summary>
        private string GetMarkingTechnologyDescription(uint markingTech)
        {
            switch (markingTech)
            {
                case 1: return "其他打印技术";
                case 2: return "未知打印技术";
                case 3: return "电子照相LED技术";
                case 4: return "电子照相激光技术";
                case 5: return "其他电子照相技术";
                case 6: return "9针点阵撞击式";
                case 7: return "24针点阵撞击式";
                case 8: return "其他点阵撞击式";
                case 9: return "撞击式全字符";
                case 10: return "带式撞击打印";
                case 11: return "其他撞击式打印";
                case 12: return "水性喷墨技术";
                case 13: return "固体喷墨技术";
                case 14: return "其他喷墨技术";
                case 15: return "绘图笔技术";
                case 16: return "热转印技术";
                case 17: return "热敏技术";
                case 18: return "热扩散技术";
                case 19: return "其他热敏技术";
                case 20: return "电蚀技术";
                case 21: return "静电技术";
                case 22: return "照相缩微技术";
                case 23: return "照相排版技术";
                case 24: return "其他照相技术";
                case 25: return "离子沉积技术";
                case 26: return "电子束技术";
                case 27: return "排版技术";
                default: return "未知技术类型";
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
                    
                    // 如果TWAIN不可用，返回错误结果
                    if (!isTwainAvailable)
                    {
                        Logger.Warning("TWAIN不可用，无法执行扫描: " + twainUnavailableReason);
                        return new ScanResult
                        {
                            Success = false,
                            Message = "扫描功能不可用: " + twainUnavailableReason + "\n\n建议:\n1. 检查是否安装了TWAIN驱动\n2. 运行 setup.bat 安装TWAIN DSM\n3. 确保扫描仪正确连接",
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        };
                    }
                    
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
        /// 打印PDF文件
        /// </summary>
        /// <param name="options">打印选项</param>
        /// <returns>打印结果</returns>
        public PrintResult PrintPdf(PrintOptions options)
        {
            var result = new PrintResult
            {
                Success = false,
                Message = "",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            try
            {
                Logger.Info("开始打印PDF，打印机: " + options.PrinterName + ", 文件大小: " + options.PdfData.Length + " 字节");

                // 验证参数
                if (string.IsNullOrEmpty(options.PrinterName))
                {
                    result.Message = "打印机名称不能为空";
                    return result;
                }

                if (options.PdfData == null || options.PdfData.Length == 0)
                {
                    result.Message = "PDF数据不能为空";
                    return result;
                }

                // 检查打印机是否存在
                var printerSettings = new PrinterSettings { PrinterName = options.PrinterName };
                if (!printerSettings.IsValid)
                {
                    result.Message = "打印机 '" + options.PrinterName + "' 不存在或不可用";
                    return result;
                }

                // 使用临时文件保存PDF数据
                var tempPdfPath = Path.GetTempFileName() + ".pdf";
                File.WriteAllBytes(tempPdfPath, options.PdfData);

                try
                {
                    // 使用PdfiumViewer加载PDF
                    using (var document = PdfDocument.Load(tempPdfPath))
                    {
                        result.TotalPages = document.PageCount;
                        Logger.Info("PDF文档页数: " + result.TotalPages);

                        // 创建打印文档
                        var printDocument = new PrintDocument();
                        printDocument.PrinterSettings.PrinterName = options.PrinterName;
                        
                        // 设置打印参数
                        printDocument.PrinterSettings.Copies = (short)Math.Max(1, options.Copies);
                        
                        if (options.Duplex != DuplexMode.Default)
                        {
                            printDocument.PrinterSettings.Duplex = (System.Drawing.Printing.Duplex)options.Duplex;
                        }

                        // 设置纸张尺寸
                        if (!string.IsNullOrEmpty(options.PaperSize))
                        {
                            foreach (PaperSize paperSize in printDocument.PrinterSettings.PaperSizes)
                            {
                                if (paperSize.PaperName.Contains(options.PaperSize))
                                {
                                    printDocument.DefaultPageSettings.PaperSize = paperSize;
                                    break;
                                }
                            }
                        }

                        var currentPage = 0;
                        var totalPages = document.PageCount;

                        // 确定要打印的页面范围
                        var startPage = Math.Max(0, options.StartPage - 1);
                        var endPage = options.EndPage > 0 ? Math.Min(options.EndPage - 1, totalPages - 1) : totalPages - 1;

                        if (startPage > endPage)
                        {
                            result.Message = "起始页不能大于结束页";
                            return result;
                        }

                        currentPage = startPage;

                        printDocument.PrintPage += (sender, e) =>
                        {
                            try
                            {
                                if (currentPage <= endPage)
                                {
                                    // 渲染PDF页面到图像
                                    using (var image = document.Render(currentPage, (int)e.PageBounds.Width, (int)e.PageBounds.Height, 96, 96, false))
                                    {
                                        // 绘制到打印页面
                                        e.Graphics.DrawImage(image, e.PageBounds);
                                    }

                                    currentPage++;
                                    result.PrintedPages = currentPage - startPage;

                                    // 如果还有页面要打印
                                    e.HasMorePages = currentPage <= endPage;

                                    Logger.Info("已打印第 " + currentPage + " 页，共 " + (endPage - startPage + 1) + " 页");
                                }
                                else
                                {
                                    e.HasMorePages = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("打印第 " + (currentPage + 1) + " 页时出错: " + ex.Message);
                                e.HasMorePages = false;
                                throw;
                            }
                        };

                        // 开始打印
                        printDocument.Print();
                        
                        result.Success = true;
                        result.Message = "成功打印 " + result.PrintedPages + " 页到打印机 '" + options.PrinterName + "'";
                        Logger.Info(result.Message);
                    }
                }
                finally
                {
                    // 清理临时文件
                    try
                    {
                        if (File.Exists(tempPdfPath))
                        {
                            File.Delete(tempPdfPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("清理临时文件失败: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "打印失败: " + ex.Message;
                Logger.Error("PDF打印失败: " + ex.Message, ex);
            }

            return result;
        }

        /// <summary>
        /// 异步打印PDF文件（带进度回调）
        /// </summary>
        /// <param name="options">打印选项</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>打印结果</returns>
        public async Task<PrintResult> PrintPdfAsync(PrintOptions options, Action<PrintProgress> progressCallback = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await Task.Run(async () =>
            {
                var result = new PrintResult
                {
                    Success = false,
                    Message = "",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                try
                {
                    if (progressCallback != null)
                    {
                        progressCallback(new PrintProgress
                        {
                            Status = "开始",
                            Message = "正在准备打印...",
                            CurrentPage = 0,
                            TotalPages = 0,
                            Percentage = 0
                        });
                    }

                    Logger.Info("开始异步打印PDF，打印机: " + options.PrinterName + ", 文件大小: " + options.PdfData.Length + " 字节");

                    // 验证参数
                    if (string.IsNullOrEmpty(options.PrinterName))
                    {
                        result.Message = "打印机名称不能为空";
                        return result;
                    }

                    if (options.PdfData == null || options.PdfData.Length == 0)
                    {
                        result.Message = "PDF数据不能为空";
                        return result;
                    }

                    // 检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();

                    // 检查打印机是否存在
                    var printerSettings = new PrinterSettings { PrinterName = options.PrinterName };
                    if (!printerSettings.IsValid)
                    {
                        result.Message = "打印机 '" + options.PrinterName + "' 不存在或不可用";
                        return result;
                    }

                    // 使用临时文件保存PDF数据
                    var tempPdfPath = Path.GetTempFileName() + ".pdf";
                    File.WriteAllBytes(tempPdfPath, options.PdfData);

                    try
                    {
                        // 使用PdfiumViewer加载PDF
                        using (var document = PdfDocument.Load(tempPdfPath))
                        {
                            result.TotalPages = document.PageCount;
                            
                            if (progressCallback != null)
                            {
                                progressCallback(new PrintProgress
                                {
                                    Status = "准备中",
                                    Message = "PDF文档页数: " + result.TotalPages,
                                    CurrentPage = 0,
                                    TotalPages = result.TotalPages,
                                    Percentage = 10
                                });
                            }

                            Logger.Info("PDF文档页数: " + result.TotalPages);

                            // 检查取消令牌
                            cancellationToken.ThrowIfCancellationRequested();

                            // 创建打印文档
                            var printDocument = new PrintDocument();
                            printDocument.PrinterSettings.PrinterName = options.PrinterName;
                            
                            // 设置打印参数
                            printDocument.PrinterSettings.Copies = (short)Math.Max(1, options.Copies);
                            
                            if (options.Duplex != DuplexMode.Default)
                            {
                                printDocument.PrinterSettings.Duplex = (System.Drawing.Printing.Duplex)options.Duplex;
                            }

                            // 设置纸张尺寸
                            if (!string.IsNullOrEmpty(options.PaperSize))
                            {
                                foreach (PaperSize paperSize in printDocument.PrinterSettings.PaperSizes)
                                {
                                    if (paperSize.PaperName.Contains(options.PaperSize))
                                    {
                                        printDocument.DefaultPageSettings.PaperSize = paperSize;
                                        break;
                                    }
                                }
                            }

                            var currentPage = 0;
                            var totalPages = document.PageCount;

                            // 确定要打印的页面范围
                            var startPage = Math.Max(0, options.StartPage - 1);
                            var endPage = options.EndPage > 0 ? Math.Min(options.EndPage - 1, totalPages - 1) : totalPages - 1;

                            if (startPage > endPage)
                            {
                                result.Message = "起始页不能大于结束页";
                                return result;
                            }

                            currentPage = startPage;
                            var pagesToPrint = endPage - startPage + 1;

                            if (progressCallback != null)
                            {
                                progressCallback(new PrintProgress
                                {
                                    Status = "打印中",
                                    Message = "开始打印，共 " + pagesToPrint + " 页",
                                    CurrentPage = 0,
                                    TotalPages = pagesToPrint,
                                    Percentage = 20
                                });
                            }

                            printDocument.PrintPage += (sender, e) =>
                            {
                                try
                                {
                                    // 检查取消令牌
                                    cancellationToken.ThrowIfCancellationRequested();

                                    if (currentPage <= endPage)
                                    {
                                        // 渲染PDF页面到图像
                                        using (var image = document.Render(currentPage, (int)e.PageBounds.Width, (int)e.PageBounds.Height, 96, 96, false))
                                        {
                                            // 绘制到打印页面
                                            e.Graphics.DrawImage(image, e.PageBounds);
                                        }

                                        currentPage++;
                                        result.PrintedPages = currentPage - startPage;

                                        // 计算进度
                                        var percentage = 20 + (int)((double)(currentPage - startPage) / pagesToPrint * 70);

                                        if (progressCallback != null)
                                        {
                                            progressCallback(new PrintProgress
                                            {
                                                Status = "打印中",
                                                Message = "正在打印第 " + (currentPage - startPage) + " 页，共 " + pagesToPrint + " 页",
                                                CurrentPage = currentPage - startPage,
                                                TotalPages = pagesToPrint,
                                                Percentage = percentage
                                            });
                                        }

                                        // 如果还有页面要打印
                                        e.HasMorePages = currentPage <= endPage;

                                        Logger.Info("已打印第 " + currentPage + " 页，共 " + (endPage - startPage + 1) + " 页");
                                    }
                                    else
                                    {
                                        e.HasMorePages = false;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error("打印第 " + (currentPage + 1) + " 页时出错: " + ex.Message);
                                    e.HasMorePages = false;
                                    throw;
                                }
                            };

                            // 开始打印并监控打印作业状态
                            await PrintWithJobMonitoring(printDocument, options.PrinterName, result, progressCallback, cancellationToken);
                            
                            Logger.Info(result.Message);
                        }
                    }
                    finally
                    {
                        // 清理临时文件
                        try
                        {
                            if (File.Exists(tempPdfPath))
                            {
                                File.Delete(tempPdfPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning("清理临时文件失败: " + ex.Message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    result.Success = false;
                    result.Message = "打印操作已取消";
                    if (progressCallback != null)
                    {
                        progressCallback(new PrintProgress
                        {
                            Status = "已取消",
                            Message = result.Message,
                            CurrentPage = result.PrintedPages,
                            TotalPages = result.TotalPages,
                            Percentage = 0
                        });
                    }
                    Logger.Info("PDF打印操作已取消");
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = "打印失败: " + ex.Message;
                    if (progressCallback != null)
                    {
                        progressCallback(new PrintProgress
                        {
                            Status = "失败",
                            Message = result.Message,
                            CurrentPage = result.PrintedPages,
                            TotalPages = result.TotalPages,
                            Percentage = 0
                        });
                    }
                    Logger.Error("PDF打印失败: " + ex.Message, ex);
                }

                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// 打印文档并监控打印作业直到真正完成
        /// </summary>
        private async Task PrintWithJobMonitoring(PrintDocument printDocument, string printerName, PrintResult result, Action<PrintProgress> progressCallback, CancellationToken cancellationToken)
        {
            try
            {
                // 获取打印前的作业列表（用于识别新作业）
                var jobsBefore = GetPrintJobs(printerName);
                var jobIdsBefore = new HashSet<int>();
                if (jobsBefore != null)
                {
                    foreach (var job in jobsBefore)
                    {
                        jobIdsBefore.Add(job.JobId);
                    }
                }

                // 开始打印
                Logger.Info("正在提交打印作业到系统...");
                printDocument.Print();
                
                // 立即开始查找打印作业，减少等待时间
                await Task.Delay(200, cancellationToken); // 给系统200ms时间创建打印作业
                
                if (progressCallback != null)
                {
                    progressCallback(new PrintProgress
                    {
                        Status = "正在处理",
                        Message = "打印作业已提交到系统，正在监控打印状态...",
                        CurrentPage = result.PrintedPages,
                        TotalPages = result.PrintedPages,
                        Percentage = 90
                    });
                }

                // 查找新创建的打印作业
                var printJobId = await FindNewPrintJob(printerName, jobIdsBefore, cancellationToken);
                
                if (printJobId.HasValue)
                {
                    Logger.Info("找到打印作业 ID: " + printJobId.Value + "，开始监控状态");
                    
                    if (progressCallback != null)
                    {
                        progressCallback(new PrintProgress
                        {
                            Status = "监控中",
                            Message = "找到打印作业，正在监控打印进度...",
                            CurrentPage = result.PrintedPages,
                            TotalPages = result.PrintedPages,
                            Percentage = 95
                        });
                    }

                    // 监控打印作业直到完成
                    await MonitorPrintJobCompletion(printerName, printJobId.Value, result, progressCallback, cancellationToken);
                }
                else
                {
                    Logger.Info("未找到打印作业（可能已快速完成），假设打印成功完成");
                    // 对于快速打印的情况，作业可能已经完成并从队列中移除
                    // 等待一小段时间确保打印真正完成，然后发送完成通知
                    await Task.Delay(1000, cancellationToken);
                    
                    result.Success = true;
                    result.Message = "成功打印 " + result.PrintedPages + " 页到打印机 '" + printerName + "'";
                    
                    if (progressCallback != null)
                    {
                        progressCallback(new PrintProgress
                        {
                            Status = "完成", 
                            Message = result.Message,
                            CurrentPage = result.PrintedPages,
                            TotalPages = result.PrintedPages,
                            Percentage = 100
                        });
                    }
                    
                    Logger.Info("打印完成通知已发送");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "打印监控失败: " + ex.Message;
                
                if (progressCallback != null)
                {
                    progressCallback(new PrintProgress
                    {
                        Status = "失败",
                        Message = result.Message,
                        CurrentPage = result.PrintedPages,
                        TotalPages = result.PrintedPages,
                        Percentage = 0
                    });
                }
                throw;
            }
        }

        /// <summary>
        /// 查找新创建的打印作业
        /// </summary>
        private async Task<int?> FindNewPrintJob(string printerName, HashSet<int> existingJobIds, CancellationToken cancellationToken)
        {
            // 最多等待5秒查找新作业（对于快速打印减少等待时间）
            var timeout = DateTime.Now.AddSeconds(5);
            var checkCount = 0;
            
            while (DateTime.Now < timeout && !cancellationToken.IsCancellationRequested)
            {
                checkCount++;
                Logger.Info("第 " + checkCount + " 次查找打印作业...");
                
                var currentJobs = GetPrintJobs(printerName);
                Logger.Info("当前找到 " + (currentJobs != null ? currentJobs.Count : 0) + " 个打印作业");
                
                if (currentJobs != null)
                {
                    foreach (var job in currentJobs)
                    {
                        Logger.Info("检查作业 ID: " + job.JobId + ", 状态: " + job.Status);
                        if (!existingJobIds.Contains(job.JobId))
                        {
                            Logger.Info("找到新作业 ID: " + job.JobId);
                            return job.JobId;
                        }
                    }
                }
                
                await Task.Delay(200, cancellationToken); // 每200ms检查一次，更频繁
            }
            
            Logger.Info("查找超时，共尝试 " + checkCount + " 次");
            return null;
        }

        /// <summary>
        /// 监控打印作业直到完成
        /// </summary>
        private async Task MonitorPrintJobCompletion(string printerName, int jobId, PrintResult result, Action<PrintProgress> progressCallback, CancellationToken cancellationToken)
        {
            var timeout = DateTime.Now.AddMinutes(10); // 最多等待10分钟
            var lastStatus = "";
            
            while (DateTime.Now < timeout && !cancellationToken.IsCancellationRequested)
            {
                var jobs = GetPrintJobs(printerName);
                PrintJob job = null;
                if (jobs != null)
                {
                    foreach (var j in jobs)
                    {
                        if (j.JobId == jobId)
                        {
                            job = j;
                            break;
                        }
                    }
                }
                
                if (job == null)
                {
                    // 作业不存在了，可能已经完成
                    Logger.Info("打印作业 " + jobId + " 已完成（作业已从队列中移除）");
                    
                    result.Success = true;
                    result.Message = "成功打印 " + result.PrintedPages + " 页到打印机 '" + printerName + "'";
                    
                    if (progressCallback != null)
                    {
                        progressCallback(new PrintProgress
                        {
                            Status = "完成",
                            Message = result.Message,
                            CurrentPage = result.PrintedPages,
                            TotalPages = result.PrintedPages,
                            Percentage = 100
                        });
                    }
                    return;
                }
                
                // 检查作业状态
                var statusChanged = job.Status != lastStatus;
                if (statusChanged)
                {
                    lastStatus = job.Status;
                    Logger.Info("打印作业 " + jobId + " 状态: " + job.Status + ", 页数: " + job.PagesPrinted + "/" + job.TotalPages);
                    
                    if (progressCallback != null)
                    {
                        var percentage = 95;
                        if (job.TotalPages > 0)
                        {
                            percentage = Math.Min(99, 95 + (int)((double)job.PagesPrinted / job.TotalPages * 4));
                        }
                        
                        progressCallback(new PrintProgress
                        {
                            Status = "打印中",
                            Message = "打印状态: " + job.Status + " (" + job.PagesPrinted + "/" + job.TotalPages + " 页)",
                            CurrentPage = job.PagesPrinted,
                            TotalPages = Math.Max(job.TotalPages, result.PrintedPages),
                            Percentage = percentage
                        });
                    }
                }
                
                // 检查是否出现错误状态
                if (job.Status.Contains("错误") || job.Status.Contains("Error") || job.Status.Contains("失败"))
                {
                    result.Success = false;
                    result.Message = "打印失败: " + job.Status;
                    
                    if (progressCallback != null)
                    {
                        progressCallback(new PrintProgress
                        {
                            Status = "失败",
                            Message = result.Message,
                            CurrentPage = job.PagesPrinted,
                            TotalPages = job.TotalPages,
                            Percentage = 0
                        });
                    }
                    return;
                }
                
                await Task.Delay(1000, cancellationToken); // 每秒检查一次
            }
            
            // 超时处理
            if (DateTime.Now >= timeout)
            {
                Logger.Warning("打印作业监控超时");
                result.Success = false;
                result.Message = "打印监控超时，无法确认打印是否完成";
                
                if (progressCallback != null)
                {
                    progressCallback(new PrintProgress
                    {
                        Status = "超时",
                        Message = result.Message,
                        CurrentPage = result.PrintedPages,
                        TotalPages = result.PrintedPages,
                        Percentage = 0
                    });
                }
            }
        }

        /// <summary>
        /// 获取指定打印机的打印作业列表
        /// </summary>
        private List<PrintJob> GetPrintJobs(string printerName)
        {
            var jobs = new List<PrintJob>();
            
            try
            {
                // 使用System.Management查询打印作业
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_PrintJob WHERE Name LIKE '%" + printerName.Replace("'", "''") + "%'"))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (System.Management.ManagementObject job in collection)
                        {
                            try
                            {
                                var jobInfo = new PrintJob
                                {
                                    JobId = Convert.ToInt32(job["JobId"] ?? 0),
                                    Status = job["Status"] != null ? job["Status"].ToString() : "未知",
                                    Document = job["Document"] != null ? job["Document"].ToString() : "",
                                    PagesPrinted = Convert.ToInt32(job["PagesPrinted"] ?? 0),
                                    TotalPages = Convert.ToInt32(job["TotalPages"] ?? 0),
                                    Size = Convert.ToInt64(job["Size"] ?? 0)
                                };
                                jobs.Add(jobInfo);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warning("解析打印作业信息失败: " + ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("获取打印作业列表失败: " + ex.Message);
            }
            
            return jobs;
        }

        /// <summary>
        /// 确保服务已初始化
        /// </summary>
        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (currentDataSource != null)
                {
                    try
                    {
                        currentDataSource.Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("关闭数据源失败: " + ex.Message);
                    }
                    finally
                    {
                        currentDataSource = null;
                    }
                }

                if (session != null)
                {
                    try
                    {
                        session.Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("关闭TWAIN会话失败: " + ex.Message);
                    }
                    finally
                    {
                        session = null;
                    }
                }

                isInitialized = false;
                Logger.Info("TWAIN服务资源已释放");
            }
            catch (Exception ex)
            {
                Logger.Error("释放资源时发生错误: " + ex.Message, ex);
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
            ScannerName = "";
            Resolution = 300;
            ColorMode = "Color";
            Format = "PNG";
            ShowUI = false;
            Brightness = 0;
            Contrast = 0;
            AutoRotate = false;
            AutoCrop = false;
        }

        public override string ToString()
        {
            return "ScanOptions{Scanner=" + ScannerName + ", Resolution=" + Resolution + ", ColorMode=" + ColorMode + 
                   ", Format=" + Format + ", ShowUI=" + ShowUI + ", Brightness=" + Brightness + 
                   ", Contrast=" + Contrast + ", AutoRotate=" + AutoRotate + ", AutoCrop=" + AutoCrop + "}";
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

    /// <summary>
    /// 打印机信息
    /// </summary>
    public class PrinterInfo
    {
        /// <summary>
        /// 打印机名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 是否为默认打印机
        /// </summary>
        public bool IsDefault { get; set; }
        
        /// <summary>
        /// 打印机状态
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// 是否支持双面打印
        /// </summary>
        public bool CanDuplex { get; set; }
        
        /// <summary>
        /// 最大份数
        /// </summary>
        public int MaximumCopies { get; set; }
        
        /// <summary>
        /// 是否支持彩色打印
        /// </summary>
        public bool SupportsColor { get; set; }
        
        /// <summary>
        /// 支持的纸张尺寸列表
        /// </summary>
        public List<string> PaperSizes { get; set; }
        
        /// <summary>
        /// 打印机技术类型（激光打印机、喷墨打印机、针式打印机等）
        /// </summary>
        public string PrinterType { get; set; }
        
        /// <summary>
        /// 标记技术代码
        /// </summary>
        public uint MarkingTechnology { get; set; }
        
        /// <summary>
        /// 打印机技术类型详细描述
        /// </summary>
        public string PrinterTypeDescription { get; set; }
        
        public PrinterInfo()
        {
            PaperSizes = new List<string>();
            PrinterType = "未知";
            PrinterTypeDescription = "未知技术类型";
            MarkingTechnology = 0;
        }
    }

    /// <summary>
    /// 打印选项
    /// </summary>
    public class PrintOptions
    {
        /// <summary>
        /// 打印机名称
        /// </summary>
        public string PrinterName { get; set; }

        /// <summary>
        /// PDF文件数据（字节数组）
        /// </summary>
        [JsonConverter(typeof(Base64JsonConverter))]
        public byte[] PdfData { get; set; }

        /// <summary>
        /// 打印份数
        /// </summary>
        public int Copies { get; set; }

        /// <summary>
        /// 双面打印模式
        /// </summary>
        public DuplexMode Duplex { get; set; }

        /// <summary>
        /// 纸张尺寸
        /// </summary>
        public string PaperSize { get; set; }

        /// <summary>
        /// 起始页（1开始）
        /// </summary>
        public int StartPage { get; set; }

        /// <summary>
        /// 结束页（0表示到最后一页）
        /// </summary>
        public int EndPage { get; set; }

        public PrintOptions()
        {
            Copies = 1;
            Duplex = DuplexMode.Default;
            PaperSize = "";
            StartPage = 1;
            EndPage = 0; // 0表示到最后一页
        }
    }

    /// <summary>
    /// 双面打印模式
    /// </summary>
    public enum DuplexMode
    {
        /// <summary>
        /// 默认（使用打印机默认设置）
        /// </summary>
        Default = 0,
        /// <summary>
        /// 单面打印
        /// </summary>
        Simplex = 1,
        /// <summary>
        /// 双面打印（长边翻转）
        /// </summary>
        Vertical = 2,
        /// <summary>
        /// 双面打印（短边翻转）
        /// </summary>
        Horizontal = 3
    }

    /// <summary>
    /// 打印结果
    /// </summary>
    public class PrintResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// 已打印页数
        /// </summary>
        public int PrintedPages { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public string Timestamp { get; set; }

        public PrintResult()
        {
            Success = false;
            Message = "";
            TotalPages = 0;
            PrintedPages = 0;
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// 打印进度
    /// </summary>
    public class PrintProgress
    {
        /// <summary>
        /// 状态（开始、准备中、打印中、完成、失败、已取消）
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 当前页
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// 进度百分比（0-100）
        /// </summary>
        public int Percentage { get; set; }

        public PrintProgress()
        {
            Status = "";
            Message = "";
            CurrentPage = 0;
            TotalPages = 0;
            Percentage = 0;
        }
    }

    /// <summary>
    /// 打印作业信息
    /// </summary>
    public class PrintJob
    {
        /// <summary>
        /// 作业ID
        /// </summary>
        public int JobId { get; set; }

        /// <summary>
        /// 作业状态
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 文档名称
        /// </summary>
        public string Document { get; set; }

        /// <summary>
        /// 已打印页数
        /// </summary>
        public int PagesPrinted { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// 作业大小（字节）
        /// </summary>
        public long Size { get; set; }

        public PrintJob()
        {
            JobId = 0;
            Status = "";
            Document = "";
            PagesPrinted = 0;
            TotalPages = 0;
            Size = 0;
        }
    }
}
