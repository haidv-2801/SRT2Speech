# SRT2Speech

Dự án chuyển đổi file phụ đề SRT thành giọng nói sử dụng các dịch vụ Text-to-Speech (TTS) như ElevenLabs, FPT, Vbee, và Google.

## 📋 Mục Lục

- [Yêu Cầu Hệ Thống](#yêu-cầu-hệ-thống)
- [Cài Đặt](#cài-đặt)
- [Build và Đóng Gói](#build-và-đóng-gói)
- [Cấu Trúc Dự Án](#cấu-trúc-dự-án)
- [Sử Dụng](#sử-dụng)
- [Tài Liệu Bổ Sung](#tài-liệu-bổ-sung)

## 🔧 Yêu Cầu Hệ Thống

- .NET 8.0 SDK
- Windows 10/11 (x64)
- FFMPEG (cho xử lý media)
- Visual Studio 2022 hoặc VS Code (khuyến nghị)

## 📦 Cài Đặt

### Clone Repository

```bash
git clone https://github.com/your-repo/SRT2Speech.git
cd SRT2Speech
```

### Restore Dependencies

```bash
dotnet restore
```

## 🚀 Build và Đóng Gói

### Build Toàn Bộ Solution

```bash
dotnet build SRT2Speech.sln -c Release
```

### Build Từng Thành Phần

#### 1. AppWindow (Ứng dụng Desktop Chính)

Build ứng dụng WPF desktop cho người dùng cuối:

```bash
dotnet publish SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj -c Release -o ../Production/ReleaseStartApp/RelaseWindowApp --runtime win-x64 --self-contained true
```

**Đặc điểm:**

- Self-contained (bao gồm .NET runtime)
- Target: Windows x64
- Output: `../Production/ReleaseStartApp/RelaseWindowApp/`
- File chạy: `SRT2Speech.AppWindow.exe`

#### 2. GenKey (Công Cụ Tạo License)

Build công cụ tạo license key (dành cho quản trị viên):

```bash
dotnet publish SRT2Speech.GenKey/SRT2Speech.GenKey.csproj -c Release -o ../Production/Genkey --runtime win-x64 --self-contained true
```

**Đặc điểm:**

- Self-contained
- Target: Windows x64
- Output: `../Production/Genkey/`
- File chạy: `SRT2Speech.GenKey.exe`

#### 3. WebAPI (API Server)

Build API server với SignalR cho real-time communication:

```bash
dotnet publish SRT2Speech.WebAPI/SRT2Speech.WebAPI.csproj -c Release -o ../Production/WebAPI --runtime win-x64 --self-contained true
```

**Đặc điểm:**

- Self-contained
- Target: Windows x64
- Output: `../Production/WebAPI/`
- File chạy: `SRT2Speech.WebAPI.exe`

**Chạy WebAPI trong Development:**

```bash
dotnet run --project SRT2Speech.WebAPI/SRT2Speech.WebAPI.csproj
```

### Build Scripts Nhanh

Các project có sẵn build scripts:

**AppWindow:**

```bash
cd SRT2Speech.AppWindow
build.bat
```

**GenKey:**

```bash
cd SRT2Speech.GenKey
build.bat
```

### Build Tất Cả Cùng Lúc

Tạo file `build-all.bat` trong thư mục root:

```batch
@echo off
echo Building SRT2Speech Solution...

echo.
echo [1/3] Building AppWindow...
dotnet publish SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj -c Release -o ../Production/ReleaseStartApp/RelaseWindowApp --runtime win-x64 --self-contained true

echo.
echo [2/3] Building GenKey...
dotnet publish SRT2Speech.GenKey/SRT2Speech.GenKey.csproj -c Release -o ../Production/Genkey --runtime win-x64 --self-contained true

echo.
echo [3/3] Building WebAPI...
dotnet publish SRT2Speech.WebAPI/SRT2Speech.WebAPI.csproj -c Release -o ../Production/WebAPI --runtime win-x64 --self-contained true

echo.
echo Build completed! Check ../Production folder for outputs.
pause
```

## 📁 Cấu Trúc Output Sau Build

```
Production/
├── ReleaseStartApp/
│   └── RelaseWindowApp/              # Ứng dụng chính cho người dùng
│       ├── SRT2Speech.AppWindow.exe
│       ├── appsettings.json
│       ├── Configs/
│       │   ├── ElevenlabKeyState.yaml
│       │   ├── proxies.yaml
│       │   └── proxy-state.yaml
│       └── [.NET runtime files]
│
├── Genkey/                            # Công cụ tạo license (nội bộ)
│   └── SRT2Speech.GenKey.exe
│
└── WebAPI/                            # API Server
    ├── SRT2Speech.WebAPI.exe
    ├── appsettings.json
    └── [.NET runtime files]
```

## 🏗️ Cấu Trúc Dự Án

### Projects

- **SRT2Speech.AppWindow** - Ứng dụng WPF desktop chính
- **SRT2Speech.Core** - Thư viện core chứa logic xử lý
- **SRT2Speech.GenKey** - Công cụ tạo license key
- **SRT2Speech.WebAPI** - ASP.NET Core Web API với SignalR
- **SRT2Speech.LicenseManager** - Quản lý license và xác thực
- **SRT2Speech.ProxyService** - Dịch vụ proxy SOCKS5
- **SRT2Speech.Packaging** - Windows App Packaging

### Thư Viện và Dependencies

- **WPF** - UI framework cho desktop app
- **ASP.NET Core** - Web API framework
- **SignalR** - Real-time communication
- **Polly** - Retry policies
- **YamlDotNet** - YAML configuration
- **FFMPEG** - Media processing

## 🎯 Sử Dụng

### Khởi Động Ứng Dụng

1. **Chạy AppWindow:**

   ```bash
   cd ../Production/ReleaseStartApp/RelaseWindowApp
   SRT2Speech.AppWindow.exe
   ```

2. **Chạy WebAPI (nếu cần):**
   ```bash
   cd ../Production/WebAPI
   SRT2Speech.WebAPI.exe
   ```

### Cấu Hình

Chỉnh sửa các file cấu hình trong thư mục `Configs/`:

- `appsettings.json` - Cấu hình chung
- `ElevenlabKeyState.yaml` - Quản lý API keys ElevenLabs
- `proxies.yaml` - Cấu hình proxy
- `proxy-state.yaml` - Trạng thái proxy

## 🔄 Auto-Update System

### Tính Năng

Ứng dụng hỗ trợ tự động kiểm tra và cập nhật phiên bản mới:

- ✅ Tự động kiểm tra cập nhật khi khởi động
- ✅ Kiểm tra thủ công qua Menu > Trợ giúp > Kiểm tra cập nhật
- ✅ Hiển thị changelog trước khi cập nhật
- ✅ Tải về và cài đặt tự động
- ✅ One-click update process

### Sử Dụng

#### Kiểm Tra Cập Nhật Thủ Công

1. Mở ứng dụng
2. Vào **Menu > Trợ giúp > Kiểm tra cập nhật** (hoặc nhấn `Ctrl+U`)
3. Nếu có phiên bản mới, chọn **Yes** để tải về
4. Sau khi tải xong, chọn **Yes** để cài đặt
5. Ứng dụng sẽ tự động khởi động lại

#### Kiểm Tra Tự Động

- Ứng dụng tự động kiểm tra cập nhật mỗi khi khởi động (background)
- Nếu có phiên bản mới, sẽ hiển thị thông báo
- Bạn có thể chọn cập nhật ngay hoặc bỏ qua

### Version Management

Phiên bản được quản lý theo chuẩn **Semantic Versioning** (SemVer):

```
Major.Minor.Patch
  │     │     │
  │     │     └── Bug fixes (1.0.0 → 1.0.1)
  │     └──────── New features, backward compatible (1.0.0 → 1.1.0)
  └────────────── Breaking changes (1.0.0 → 2.0.0)
```

**Ví dụ:**

- `1.0.0` → `1.0.1`: Sửa lỗi nhỏ
- `1.0.0` → `1.1.0`: Thêm tính năng mới
- `1.0.0` → `2.0.0`: Thay đổi lớn, không tương thích ngược

### Deployment Server Setup

#### 1. Chuẩn Bị File `version.json`

Tạo file `version.json` trên server của bạn (xem [`docs/version.json.example`](docs/version.json.example)):

```json
{
  "latestVersion": "1.1.0",
  "releaseDate": "2024-11-15T00:00:00Z",
  "downloadUrl": "https://yourdomain.com/downloads/SRT2Speech-1.1.0.exe",
  "fileSize": 157286400,
  "changelog": ["✨ Thêm tính năng X", "🐛 Sửa lỗi Y", "⚡ Cải thiện Z"],
  "minimumVersion": "1.0.0",
  "forceUpdate": false,
  "releaseNotes": "Mô tả chi tiết..."
}
```

#### 2. Upload Files

Upload các file sau lên server:

```
your-server/
├── api/
│   └── version.json          # Thông tin phiên bản
└── downloads/
    └── SRT2Speech-1.1.0.exe  # File cài đặt
```

#### 3. Cấu Hình URL

Trong [`UpdateService.cs`](SRT2Speech.AppWindow/Services/UpdateService.cs), cập nhật URL:

```csharp
private const string VERSION_CHECK_URL = "https://yourdomain.com/api/version.json";
```

#### 4. Options Deploy

**Option A: GitHub Releases (FREE)**

```csharp
// Sử dụng GitHub Releases API
_updateCheckUrl = "https://api.github.com/repos/username/repo/releases/latest";
```

**Option B: Custom Server**

```bash
# Upload lên web server
scp version.json user@server:/var/www/html/api/
scp SRT2Speech-1.1.0.exe user@server:/var/www/html/downloads/
```

**Option C: Cloud Storage**

- Azure Blob Storage
- AWS S3
- Google Cloud Storage

### Build & Release Process

#### 1. Cập Nhật Version

Chỉnh sửa [`SRT2Speech.AppWindow.csproj`](SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj):

```xml
<Version>1.1.0</Version>
<AssemblyVersion>1.1.0.0</AssemblyVersion>
<FileVersion>1.1.0.0</FileVersion>
```

#### 2. Build Release

```bash
# Chạy build script
build-all.bat

# Hoặc build thủ công
dotnet publish SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj `
  -c Release `
  -o ./Release `
  --runtime win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:Version=1.1.0
```

#### 3. Tạo Changelog

Cập nhật [`docs/changelog.md`](docs/changelog.md) với các thay đổi mới.

#### 4. Upload & Deploy

1. Upload file `.exe` lên server
2. Cập nhật `version.json` với thông tin phiên bản mới
3. Test kiểm tra cập nhật từ phiên bản cũ

### Troubleshooting

#### Lỗi "Không có quyền truy cập"

**Giải pháp:** Chạy ứng dụng với quyền Administrator:

```
Right-click > Run as Administrator
```

#### Lỗi "Cannot connect to update server"

**Kiểm tra:**

- Kết nối internet
- Firewall/Antivirus không block
- URL server đúng trong code
- Server đang hoạt động

#### File tải về bị lỗi

**Kiểm tra:**

- Checksum/Hash của file
- Dung lượng file khớp với `fileSize` trong `version.json`
- Quyền ghi vào thư mục Temp

### Security Notes

- ⚠️ Luôn sử dụng HTTPS cho update server
- ⚠️ Xác thực digital signature của file EXE
- ⚠️ Kiểm tra checksum trước khi cài đặt
- ⚠️ Không tự động cài đặt mà không hỏi user

## 📚 Tài Liệu Bổ Sung

Xem thêm tài liệu chi tiết trong thư mục [`docs/`](docs/):

- [Changelog](docs/changelog.md) - Lịch sử thay đổi
- [Checklist](docs/checklist.md) - Danh sách kiểm tra
- [Dead Keys Feature](docs/dead-keys-feature.md) - Tính năng dead keys
- [SOCKS5 Proxy](docs/socks5-proxy-implementation.md) - Hướng dẫn proxy
- [Proxy Service](docs/proxy-service-implementation.md) - Chi tiết dịch vụ proxy

## 🔐 License Management

Dự án sử dụng hệ thống license dựa trên hardware ID. Xem [`SRT2Speech.LicenseManager`](SRT2Speech.LicenseManager/) để biết thêm chi tiết.

## 🤝 Đóng Góp

Contributions are welcome! Please read our contributing guidelines first.

## 📝 License

[Thêm thông tin license của bạn ở đây]

## 📧 Liên Hệ

[Thêm thông tin liên hệ của bạn ở đây]

---

**Lưu ý:**

- Tất cả builds đều sử dụng `--self-contained true` nên không cần người dùng cài đặt .NET Runtime
- Đảm bảo có FFMPEG trong PATH hoặc cùng thư mục với executable
- Kiểm tra file [`AGENTS.md`](AGENTS.md) cho hướng dẫn chi tiết về code style và patterns
