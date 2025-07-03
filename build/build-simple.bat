@echo off
chcp 65001 > nul
echo ========================================
echo TWAIN Scanner Middleware Simple Build Script
echo ========================================

set PROJECT_DIR=%~dp0..
set SRC_DIR=%PROJECT_DIR%\src
set OUTPUT_DIR=%PROJECT_DIR%\bin
set PACKAGES_DIR=%PROJECT_DIR%\packages
set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

echo Project Directory: %PROJECT_DIR%
echo Source Directory: %SRC_DIR%
echo Output Directory: %OUTPUT_DIR%
echo Packages Directory: %PACKAGES_DIR%
echo Compiler: %CSC_PATH%
echo.

REM Check compiler
if not exist "%CSC_PATH%" (
    echo Error: C# compiler not found at %CSC_PATH%
    pause
    exit /b 1
)

REM Create output directory
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

REM Clean old files
if exist "%OUTPUT_DIR%\TwainMiddleware.exe" del "%OUTPUT_DIR%\TwainMiddleware.exe"

REM Clean DEBUG folder
if exist "%OUTPUT_DIR%\Debug\" (
    echo Cleaning DEBUG folder...
    rd /s /q "%OUTPUT_DIR%\Debug\"
)

REM Clean SDK folder
if exist "%OUTPUT_DIR%\sdk\" (
    echo Cleaning SDK folder...
    rd /s /q "%OUTPUT_DIR%\sdk\"
)

echo Compiling project...

REM Compile project
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
    /reference:"%PACKAGES_DIR%\PdfiumViewer.2.13.0.0\lib\net20\PdfiumViewer.dll" ^
    "%SRC_DIR%\*.cs" ^
    "%SRC_DIR%\Properties\*.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo Build successful!
    echo Output file: %OUTPUT_DIR%\TwainMiddleware.exe
    
    REM Copy dependency DLLs to output directory
    echo.
    echo Copying dependency libraries...
    copy "%PACKAGES_DIR%\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll" "%OUTPUT_DIR%\" >nul
    copy "%PACKAGES_DIR%\WebSocketSharp-NonPreRelease.1.0.0\lib\net35\websocket-sharp.dll" "%OUTPUT_DIR%\" >nul
    copy "%PACKAGES_DIR%\NTwain.3.7.5\lib\net40\NTwain.dll" "%OUTPUT_DIR%\" >nul
    copy "%PACKAGES_DIR%\PdfiumViewer.2.13.0.0\lib\net20\PdfiumViewer.dll" "%OUTPUT_DIR%\" >nul
    copy "%PACKAGES_DIR%\PdfiumViewer.Native.x86_64.v8-xfa.2018.4.8.256\Build\x64\pdfium.dll" "%OUTPUT_DIR%\" >nul
    
    REM Copy config file
    copy "%SRC_DIR%\App.config" "%OUTPUT_DIR%\TwainMiddleware.exe.config" >nul
    
    REM Copy web resources
    if not exist "%OUTPUT_DIR%\web" mkdir "%OUTPUT_DIR%\web"
    if exist "%PROJECT_DIR%\web\*" copy "%PROJECT_DIR%\web\*" "%OUTPUT_DIR%\web\" >nul 2>&1
    
    REM Copy standalone test page
    if exist "%PROJECT_DIR%\standalone-test.html" (
        copy "%PROJECT_DIR%\standalone-test.html" "%OUTPUT_DIR%\" >nul
        echo Standalone test page copied
    )
    
    echo Dependencies, web resources and test page copied successfully!
    echo.
    echo You can run: %OUTPUT_DIR%\TwainMiddleware.exe
    echo Right-click tray icon and select "Show test page" to test in browser
    
) else (
    echo.
    echo Build failed!
    echo Error code: %ERRORLEVEL%
)

echo.
pause