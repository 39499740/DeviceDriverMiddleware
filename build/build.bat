@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo TWAIN扫描仪中间件 构建脚本
echo ========================================

:: 设置构建参数
set "PROJECT_DIR=%~dp0.."
set "SRC_DIR=%PROJECT_DIR%\src"
set "BIN_DIR=%PROJECT_DIR%\bin"
set "BUILD_CONFIG=Release"

echo 项目目录: %PROJECT_DIR%
echo 源码目录: %SRC_DIR%
echo 输出目录: %BIN_DIR%
echo 构建配置: %BUILD_CONFIG%
echo.

:: 检查MSBuild
echo 正在检查MSBuild...
where msbuild >nul 2>&1
if errorlevel 1 (
    echo 错误: 未找到MSBuild，请安装.NET Framework SDK或Visual Studio Build Tools
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('where msbuild') do (
    echo 找到MSBuild: %%i
    goto :found_msbuild
)

:found_msbuild
echo.

:: 清理输出目录
echo 正在清理输出目录...
if exist "%BIN_DIR%" (
    rmdir /s /q "%BIN_DIR%"
)
mkdir "%BIN_DIR%" 2>nul

:: 创建NuGet包目录（模拟）
echo 正在创建依赖包目录...
set "PACKAGES_DIR=%PROJECT_DIR%\packages"
if not exist "%PACKAGES_DIR%" mkdir "%PACKAGES_DIR%"

:: 创建模拟的NuGet包结构
set "JSON_DIR=%PACKAGES_DIR%\Newtonsoft.Json.13.0.3\lib\net45"
set "NTWAIN_DIR=%PACKAGES_DIR%\NTwain.3.8.0\lib\net45"  
set "WEBSOCKET_DIR=%PACKAGES_DIR%\WebSocketSharp.1.0.3-rc11\lib"

if not exist "%JSON_DIR%" mkdir "%JSON_DIR%"
if not exist "%NTWAIN_DIR%" mkdir "%NTWAIN_DIR%"
if not exist "%WEBSOCKET_DIR%" mkdir "%WEBSOCKET_DIR%"

:: 创建占位符DLL文件（实际项目中应从NuGet下载）
echo 注意: 需要手动下载以下NuGet包:
echo - Newtonsoft.Json 13.0.3
echo - NTwain 3.8.0  
echo - WebSocketSharp 1.0.3-rc11
echo.

:: 执行构建
echo 正在构建项目...
pushd "%SRC_DIR%"

msbuild TwainMiddleware.csproj /p:Configuration=%BUILD_CONFIG% /p:Platform="Any CPU" /verbosity:minimal /nologo

if errorlevel 1 (
    echo 错误: 构建失败
    popd
    pause
    exit /b 1
)

popd

:: 检查构建结果
set "OUTPUT_EXE=%BIN_DIR%\%BUILD_CONFIG%\TwainMiddleware.exe"
if exist "%OUTPUT_EXE%" (
    echo.
    echo ========================================
    echo 构建成功！
    echo ========================================
    echo 输出文件: %OUTPUT_EXE%
    
    :: 复制依赖文件
    echo 正在复制依赖文件...
    copy "%SRC_DIR%\App.config" "%BIN_DIR%\%BUILD_CONFIG%\TwainMiddleware.exe.config" >nul
    
    :: 复制SDK文件夹
    if exist "%PROJECT_DIR%\sdk" (
        xcopy "%PROJECT_DIR%\sdk" "%BIN_DIR%\%BUILD_CONFIG%\sdk\" /E /I /Y >nul
        echo SDK文件已复制
    )
    
    :: 显示文件信息
    echo.
    echo 文件信息:
    dir "%BIN_DIR%\%BUILD_CONFIG%\TwainMiddleware.exe" | findstr TwainMiddleware.exe
    
    echo.
    echo 构建完成！可执行文件位于: %OUTPUT_EXE%
) else (
    echo 错误: 构建输出文件未找到
    exit /b 1
)

pause 