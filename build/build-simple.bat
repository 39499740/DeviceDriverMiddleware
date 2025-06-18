@echo off
chcp 65001 > nul
echo ========================================
echo TWAIN扫描仪中间件 简单构建脚本
echo ========================================

set PROJECT_DIR=%~dp0..
set SRC_DIR=%PROJECT_DIR%\src
set OUTPUT_DIR=%PROJECT_DIR%\bin
set PACKAGES_DIR=%PROJECT_DIR%\packages
set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

echo 项目目录: %PROJECT_DIR%
echo 源码目录: %SRC_DIR%
echo 输出目录: %OUTPUT_DIR%
echo 包目录: %PACKAGES_DIR%
echo 编译器: %CSC_PATH%
echo.

REM 检查编译器
if not exist "%CSC_PATH%" (
    echo 错误: 未找到C#编译器 %CSC_PATH%
    pause
    exit /b 1
)

REM 创建输出目录
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

REM 清理旧文件
if exist "%OUTPUT_DIR%\TwainMiddleware.exe" del "%OUTPUT_DIR%\TwainMiddleware.exe"

echo 正在编译项目...

REM 编译项目 (包含NTwain库，使用真实TWAIN功能)
"%CSC_PATH%" ^
    /target:winexe ^
    /platform:anycpu ^
    /optimize+ ^
    /out:"%OUTPUT_DIR%\TwainMiddleware.exe" ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Data.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Web.dll ^
    /reference:System.Configuration.dll ^
    /reference:Microsoft.VisualBasic.dll ^
    /reference:System.Xml.dll ^
    /reference:System.Xml.Linq.dll ^
    /reference:System.Data.DataSetExtensions.dll ^
    /reference:Microsoft.CSharp.dll ^
    /reference:"%PACKAGES_DIR%\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll" ^
    /reference:"%PACKAGES_DIR%\WebSocketSharp-NonPreRelease.1.0.0\lib\net35\websocket-sharp.dll" ^
    /reference:"%PACKAGES_DIR%\NTwain.3.7.5\lib\net40\NTwain.dll" ^
    "%SRC_DIR%\*.cs" ^
    "%SRC_DIR%\Properties\*.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ✅ 编译成功！
    echo 📁 输出文件: %OUTPUT_DIR%\TwainMiddleware.exe
    
    REM 复制依赖DLL到输出目录
    echo.
    echo 正在复制依赖库...
    copy "%PACKAGES_DIR%\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll" "%OUTPUT_DIR%\" >nul
    copy "%PACKAGES_DIR%\WebSocketSharp-NonPreRelease.1.0.0\lib\net35\websocket-sharp.dll" "%OUTPUT_DIR%\" >nul
    copy "%PACKAGES_DIR%\NTwain.3.7.5\lib\net40\NTwain.dll" "%OUTPUT_DIR%\" >nul
    
    REM 复制配置文件
    copy "%SRC_DIR%\App.config" "%OUTPUT_DIR%\TwainMiddleware.exe.config" >nul
    
    echo ✅ 依赖库复制完成！
    echo.
    echo 🚀 可以运行：%OUTPUT_DIR%\TwainMiddleware.exe
    
) else (
    echo.
    echo ❌ 编译失败！
    echo 错误代码: %ERRORLEVEL%
)

echo.
pause 