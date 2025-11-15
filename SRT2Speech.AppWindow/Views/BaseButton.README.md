# BaseButton - Custom WPF Button Control

BaseButton là một custom UserControl có thể tái sử dụng cho WPF với nhiều style variants được xây dựng sẵn.

## Tính năng

- ✅ Nhiều style variants: Primary, Success, Warning, Danger, Secondary, Info
- ✅ Hỗ trợ icon emoji
- ✅ Có thể tùy chỉnh kích thước, padding, corner radius
- ✅ Hỗ trợ Command binding
- ✅ Tooltip
- ✅ Trạng thái Enabled/Disabled
- ✅ Hover và pressed effects

## Cách sử dụng

### 1. Sử dụng cơ bản

```xml
<local:BaseButton Text="Click Me"
                  ButtonWidth="120"
                  ButtonHeight="32"/>
```

### 2. Với Icon

```xml
<local:BaseButton Text="Download"
                  Icon="▶️"
                  ButtonWidth="140"/>
```

### 3. Với Command Binding

```xml
<local:BaseButton Text="Save"
                  Icon="💾"
                  Command="{Binding SaveCommand}"
                  Tooltip="Lưu thay đổi"/>
```

### 4. Các Style khác nhau

#### Primary Button (Mặc định - Xanh dương)

```xml
<local:BaseButton Text="Primary"/>
```

#### Success Button (Xanh lá)

```xml
<local:BaseButton x:Name="btnSuccess" Text="Success"/>
```

```csharp
btnSuccess.SetSuccessStyle();
```

#### Warning Button (Vàng)

```xml
<local:BaseButton x:Name="btnWarning" Text="Warning"/>
```

```csharp
btnWarning.SetWarningStyle();
```

#### Danger Button (Đỏ)

```xml
<local:BaseButton x:Name="btnDanger" Text="Delete"/>
```

```csharp
btnDanger.SetDangerStyle();
```

#### Secondary Button (Xám)

```xml
<local:BaseButton x:Name="btnSecondary" Text="Cancel"/>
```

```csharp
btnSecondary.SetSecondaryStyle();
```

#### Info Button (Xanh nhạt)

```xml
<local:BaseButton x:Name="btnInfo" Text="Info"/>
```

```csharp
btnInfo.SetInfoStyle();
```

### 5. Custom Style

```csharp
btnCustom.SetCustomStyle(
    background: "#8E44AD",
    hover: "#732D91",
    pressed: "#5B2472",
    foreground: "#FFFFFF"
);
```

### 6. Kích thước khác nhau

```xml
<!-- Small -->
<local:BaseButton Text="Small"
                  ButtonWidth="80"
                  ButtonHeight="24"/>

<!-- Medium (Default) -->
<local:BaseButton Text="Medium"
                  ButtonWidth="120"
                  ButtonHeight="32"/>

<!-- Large -->
<local:BaseButton Text="Large"
                  ButtonWidth="160"
                  ButtonHeight="40"/>
```

### 7. Icon Only Button

```xml
<local:BaseButton Icon="🔄"
                  ButtonWidth="40"
                  ButtonHeight="40"
                  IconSize="20"
                  Tooltip="Refresh"/>
```

### 8. Custom Corner Radius

```xml
<!-- No radius -->
<local:BaseButton Text="No Radius" CornerRadius="0"/>

<!-- Pill shape -->
<local:BaseButton Text="Pill" CornerRadius="20"/>
```

### 9. Disabled Button

```xml
<local:BaseButton Text="Disabled"
                  IsButtonEnabled="False"/>
```

## Properties

| Property           | Type         | Default   | Description                       |
| ------------------ | ------------ | --------- | --------------------------------- |
| `Text`             | string       | ""        | Text hiển thị trên button         |
| `Icon`             | string       | ""        | Icon emoji hiển thị bên trái text |
| `IconSize`         | double       | 16        | Kích thước icon                   |
| `Command`          | ICommand     | null      | Command để bind                   |
| `CommandParameter` | object       | null      | Parameter cho command             |
| `Tooltip`          | string       | ""        | Tooltip text                      |
| `IsButtonEnabled`  | bool         | true      | Trạng thái enabled/disabled       |
| `ButtonWidth`      | double       | 120       | Chiều rộng button                 |
| `ButtonHeight`     | double       | 32        | Chiều cao button                  |
| `CornerRadius`     | CornerRadius | 4         | Bo góc button                     |
| `ButtonPadding`    | Thickness    | 10,5,10,5 | Padding bên trong                 |

## Methods

| Method                                   | Description                    |
| ---------------------------------------- | ------------------------------ |
| `SetPrimaryStyle()`                      | Đặt style Primary (xanh dương) |
| `SetSuccessStyle()`                      | Đặt style Success (xanh lá)    |
| `SetWarningStyle()`                      | Đặt style Warning (vàng)       |
| `SetDangerStyle()`                       | Đặt style Danger (đỏ)          |
| `SetSecondaryStyle()`                    | Đặt style Secondary (xám)      |
| `SetInfoStyle()`                         | Đặt style Info (xanh nhạt)     |
| `SetCustomStyle(bg, hover, pressed, fg)` | Đặt custom colors              |

## Ví dụ thực tế

### Trong XAML

```xml
<StackPanel Orientation="Horizontal" Margin="10">
    <local:BaseButton x:Name="btnDownload"
                      Text="▶️ Download MP3"
                      ButtonWidth="130"
                      Command="{Binding DownloadCommand}"
                      Tooltip="Bắt đầu tải xuống"/>

    <local:BaseButton x:Name="btnRetry"
                      Text="🔄 Retry Errors"
                      ButtonWidth="110"
                      Margin="5,0,0,0"
                      Command="{Binding RetryCommand}"/>

    <local:BaseButton x:Name="btnStop"
                      Text="⏹️ Stop"
                      ButtonWidth="80"
                      Margin="5,0,0,0"
                      Command="{Binding StopCommand}"/>
</StackPanel>
```

### Trong Code-behind

```csharp
public partial class MyControl : UserControl
{
    public MyControl()
    {
        InitializeComponent();

        // Set styles
        btnDownload.SetSuccessStyle();
        btnRetry.SetWarningStyle();
        btnStop.SetDangerStyle();
    }
}
```

## Build Project

Sau khi tạo các file, bạn cần build project để WPF compiler tạo `InitializeComponent()` method:

```bash
dotnet build SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj
```

## Xem Demo

Để xem các ví dụ về BaseButton, mở file `BaseButtonExample.xaml` trong Designer hoặc chạy application và add control này vào MainWindow.

## Notes

- Tất cả các lỗi `InitializeComponent` sẽ biến mất sau khi build project
- Icon sử dụng emoji Unicode, bạn có thể dùng bất kỳ emoji nào
- Corner radius có thể set riêng cho từng góc: `CornerRadius="10,0,10,0"`
- Màu sắc có thể dùng hex color hoặc named colors
