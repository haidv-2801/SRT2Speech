# AGENTS.md

This file provides guidance to agents when working with code in this repository.

## Build Commands

- Build AppWindow: `dotnet publish SRT2Speech.AppWindow/SRT2Speech.AppWindow.csproj -c Release -o ../Production/ReleaseStartApp/RelaseWindowApp --runtime win-x64 --self-contained true`
- Build GenKey: `dotnet publish SRT2Speech.GenKey/SRT2Speech.GenKey.csproj -c Release -o ../Production/Genkey --runtime win-x64 --self-contained true`
- Build WebAPI: `dotnet build SRT2Speech.WebAPI/SRT2Speech.WebAPI.csproj`
- Run WebAPI: `dotnet run --project SRT2Speech.WebAPI/SRT2Speech.WebAPI.csproj`

## Code Style

- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- Target .NET 8.0
- WPF for desktop app, ASP.NET Core for WebAPI

## Project-Specific Patterns

- Custom AES encryption in `SRT2Speech.Core/Utilitys/AESEncryption.cs` for secure data handling
- SRT utility in `SRT2Speech.Core/Utilitys/SRTUtility.cs` for subtitle processing
- FFMPEG integration via `SRT2Speech.Core/Ffmpeg/Ffmpeg.cs` for media processing
- Polly-based retry mechanism in `SRT2Speech.AppWindow/Services/RetryWithJitterAndPolly.cs`
- SignalR for real-time communication in WebAPI and Core
- YAML utility in `SRT2Speech.Core/Utilitys/YamlUtility.cs` for configuration
- ApiKeyManager in `SRT2Speech.AppWindow/Services/ApiKeyManager.cs` does NOT use periodic auto-save (timer commented out) - only saves on explicit calls or Dispose

ALL REPONSE MESSAGE IN VIETNAMESE
