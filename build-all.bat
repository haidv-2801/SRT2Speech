@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ============================================================================
:: SRT2Speech - Build Script
:: Tự động build toàn bộ dự án từ đầu đến cuối
:: ============================================================================

echo.
echo ========================================
echo  SRT2Speech - Build All Projects
echo ========================================
echo.

:: Kiểm tra .NET SDK
echo [*] Kiểm tra .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK không được tìm thấy!
    echo [ERROR] Vui lòng cài đặt .NET 8.0 SDK từ: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [OK] .NET SDK đã được cài đặt
for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo [OK] Phiên bản: %DOTNET_VERSION%
echo.

:: Lưu thư mục hiện tại
set SCRIPT_DIR=%~dp0
set OUTPUT_DIR=%SCRIPT_DIR%..\Production

:: Tạo thư mục output
echo [*] Tạo thư mục output...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
if not exist "%OUTPUT_DIR%\ReleaseStartApp" mkdir "%OUTPUT_DIR%\ReleaseStartApp"
if not exist "%OUTPUT_DIR%\Genkey" mkdir "%OUTPUT_DIR%\Genkey"
if not exist "%OUTPUT_DIR%\WebAPI" mkdir "%OUTPUT_DIR%\WebAPI"
echo [OK] Thư mục output đã sẵn sàng
echo.

:: Xóa các file build cũ (optional)
echo [*] Dọn dẹp các build cũ...
if exist "%OUTPUT_DIR%\ReleaseStartApp\RelaseWindowApp" (
    echo [*] Xóa AppWindow build cũ...
    rd /s /q "%OUTPUT_DIR%\ReleaseStartApp\RelaseWindowApp" 2>nul
)
if exist "%OUTPUT_DIR%\Genkey" (
    echo [*] Xóa GenKey build cũ...
    rd /s /q "%OUTPUT_DIR%\Genkey" 2>nul
    mkdir "%OUTPUT_DIR%\Genkey"
)
if exist "%OUTPUT_DIR%\WebAPI" (
    echo [*] Xóa WebAPI build cũ...
    rd /s /q "%OUTPUT_DIR%\WebAPI" 2>nul
    mkdir "%OUTPUT_DIR%\WebAPI"
)
echo [OK] Dọn dẹp hoàn tất
echo.

:: Restore dependencies
echo ========================================
echo [STEP 0/4] Restore Dependencies
echo ========================================
echo.
dotnet restore SRT2Speech.sln
if errorlevel 1 (
    echo [ERROR] Restore dependencies thất bại!
    pause
    exit /b 1
)
echo [OK] Restore dependencies thành công
echo.

:: Build 1: AppWindow
echo ========================================
echo [STEP 1/4] Building AppWindow
echo ========================================
echo.
echo [*] Đang build ứng dụng desktop chính...
echo [*] Project: SRT2Speech.AppWindow
echo [*] Output: %OUTPUT_DIR%\ReleaseStartApp\RelaseWindowApp
echo.

dotnet publish SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj -c Release -o "%OUTPUT_DIR%\ReleaseStartApp\RelaseWindowApp" --runtime win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

if errorlevel 1 (
    echo [ERROR] Build AppWindow thất bại!
    pause
    exit /b 1
)
echo.
echo [OK] AppWindow build thành công
echo.

:: Build 2: GenKey
echo ========================================
echo [STEP 2/4] Building GenKey
echo ========================================
echo.
echo [*] Đang build công cụ tạo license...
echo [*] Project: SRT2Speech.GenKey
echo [*] Output: %OUTPUT_DIR%\Genkey
echo.

dotnet publish SRT2Speech.GenKey/SRT2Speech.GenKey.csproj -c Release -o "%OUTPUT_DIR%\Genkey" --runtime win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

if errorlevel 1 (
    echo [ERROR] Build GenKey thất bại!
    pause
    exit /b 1
)
echo.
echo [OK] GenKey build thành công
echo.

:: Build 3: WebAPI
echo ========================================
echo [STEP 3/4] Building WebAPI
echo ========================================
echo.
echo [*] Đang build API server...
echo [*] Project: SRT2Speech.WebAPI
echo [*] Output: %OUTPUT_DIR%\WebAPI
echo.

dotnet publish SRT2Speech.WebAPI/SRT2Speech.WebAPI.csproj -c Release -o "%OUTPUT_DIR%\WebAPI" --runtime win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

if errorlevel 1 (
    echo [ERROR] Build WebAPI thất bại!
    pause
    exit /b 1
)
echo.
echo [OK] WebAPI build thành công
echo.

:: Tạo README cho thư mục Production
echo ========================================
echo [STEP 4/4] Tạo Documentation
echo ========================================
echo.

(
echo # SRT2Speech - Production Build
echo.
echo Build Date: %date% %time%
echo .NET Version: %DOTNET_VERSION%
echo.
echo ## Built Components
echo.
echo 1. **AppWindow** - Ứng dụng Desktop Chính
echo    - Path: ReleaseStartApp\RelaseWindowApp\SRT2Speech.AppWindow.exe
echo    - Mô tả: Ứng dụng WPF cho người dùng cuối (Single File)
echo.
echo 2. **GenKey** - Công Cụ Tạo License
echo    - Path: Genkey\SRT2Speech.GenKey.exe
echo    - Mô tả: Công cụ generate license key - Single File (nội bộ)
echo.
echo 3. **WebAPI** - API Server
echo    - Path: WebAPI\SRT2Speech.WebAPI.exe
echo    - Mô tả: ASP.NET Core Web API với SignalR (Single File)
echo.
echo ## Hướng Dẫn Sử Dụng
echo.
echo ### Chạy Ứng Dụng Desktop
echo ```
echo cd ReleaseStartApp\RelaseWindowApp
echo SRT2Speech.AppWindow.exe
echo ```
echo.
echo ### Chạy WebAPI
echo ```
echo cd WebAPI
echo SRT2Speech.WebAPI.exe
echo ```
echo.
echo ## Yêu Cầu
echo.
echo - Windows 10/11 (x64)
echo - FFMPEG (đặt trong PATH hoặc cùng thư mục)
echo.
echo ## Lưu Ý
echo.
echo - Tất cả builds đều là SINGLE FILE (chỉ 1 file EXE duy nhất)
echo - Self-contained (không cần cài .NET Runtime)
echo - Compressed (tiết kiệm dung lượng)
echo - Đảm bảo có đủ quyền Administrator nếu cần
echo - Kiểm tra Windows Defender/Antivirus nếu có lỗi
) > "%OUTPUT_DIR%\README.txt"

echo [OK] Đã tạo README.txt
echo.

:: Tổng kết
echo ========================================
echo  BUILD COMPLETED SUCCESSFULLY!
echo ========================================
echo.
echo [OK] Tất cả projects đã được build thành công!
echo.
echo Output Directory: %OUTPUT_DIR%
echo.
echo Các file thực thi:
echo   1. %OUTPUT_DIR%\ReleaseStartApp\RelaseWindowApp\SRT2Speech.AppWindow.exe
echo   2. %OUTPUT_DIR%\Genkey\SRT2Speech.GenKey.exe
echo   3. %OUTPUT_DIR%\WebAPI\SRT2Speech.WebAPI.exe
echo.
echo Để kiểm tra, chạy:
echo   - AppWindow: cd "%OUTPUT_DIR%\ReleaseStartApp\RelaseWindowApp" ^&^& SRT2Speech.AppWindow.exe
echo   - GenKey:    cd "%OUTPUT_DIR%\Genkey" ^&^& SRT2Speech.GenKey.exe
echo   - WebAPI:    cd "%OUTPUT_DIR%\WebAPI" ^&^& SRT2Speech.WebAPI.exe
echo.

:: Hỏi có muốn mở thư mục output không
set /p OPEN_FOLDER="Mở thư mục output? (Y/N): "
if /i "%OPEN_FOLDER%"=="Y" (
    explorer "%OUTPUT_DIR%"
)

echo.
echo Cảm ơn bạn đã sử dụng SRT2Speech Build Script!
echo.
pause