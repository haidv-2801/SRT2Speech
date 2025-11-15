using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using SRT2Speech.AppWindow.Models;

namespace SRT2Speech.AppWindow.Services
{
    /// <summary>
    /// Service quản lý việc kiểm tra và cập nhật phiên bản ứng dụng
    /// </summary>
    public class UpdateService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _updateCheckUrl;
        private bool _disposed = false;

        /// <summary>
        /// Khởi tạo UpdateService
        /// </summary>
        /// <param name="updateCheckUrl">URL để kiểm tra phiên bản mới (mặc định: GitHub releases)</param>
        public UpdateService(string? updateCheckUrl = null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            // Mặc định sử dụng GitHub releases hoặc custom URL
            _updateCheckUrl = updateCheckUrl ?? "https://yourdomain.com/api/version.json";
        }

        /// <summary>
        /// Lấy phiên bản hiện tại của ứng dụng
        /// </summary>
        public Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetName().Version ?? new Version(1, 0, 0);
        }

        /// <summary>
        /// Kiểm tra có phiên bản mới không
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(_updateCheckUrl);
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (updateInfo == null)
                    return null;

                var currentVersion = GetCurrentVersion();
                var latestVersion = Version.Parse(updateInfo.LatestVersion);

                if (latestVersion > currentVersion)
                {
                    updateInfo.HasUpdate = true;
                    return updateInfo;
                }

                return null;
            }
            catch (HttpRequestException ex)
            {
                // Lỗi kết nối mạng
                System.Diagnostics.Debug.WriteLine($"[UPDATE] Lỗi kiểm tra cập nhật: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                // Lỗi parse JSON
                System.Diagnostics.Debug.WriteLine($"[UPDATE] Lỗi parse version info: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                // Lỗi khác
                System.Diagnostics.Debug.WriteLine($"[UPDATE] Lỗi không xác định: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Hiển thị dialog hỏi người dùng có muốn cập nhật không
        /// </summary>
        public async Task<bool> PromptAndUpdateAsync()
        {
            var updateInfo = await CheckForUpdatesAsync();
            
            if (updateInfo == null)
            {
                MessageBox.Show(
                    "Bạn đang sử dụng phiên bản mới nhất!",
                    "Không có cập nhật",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return false;
            }

            var currentVersion = GetCurrentVersion();
            var changelog = string.Join("\n", updateInfo.Changelog.ConvertAll(c => $"  • {c}"));
            
            var message = $"Phiên bản mới {updateInfo.LatestVersion} đã có!\n" +
                         $"Phiên bản hiện tại: {currentVersion}\n" +
                         $"Ngày phát hành: {updateInfo.ReleaseDate:dd/MM/yyyy}\n\n" +
                         $"Thay đổi:\n{changelog}\n\n" +
                         $"Bạn có muốn tải về và cập nhật không?";

            var result = MessageBox.Show(
                message,
                "Cập nhật mới",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information
            );

            if (result == MessageBoxResult.Yes)
            {
                return await DownloadAndInstallAsync(updateInfo);
            }

            return false;
        }

        /// <summary>
        /// Tải về và cài đặt bản cập nhật
        /// </summary>
        public async Task<bool> DownloadAndInstallAsync(UpdateInfo updateInfo)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"SRT2Speech-Update-{updateInfo.LatestVersion}.exe");

            try
            {
                // Hiển thị progress window (nếu có)
                // var progressWindow = new UpdateProgressWindow();
                // progressWindow.Show();

                // Tải file
                using (var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var downloadedBytes = 0L;

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                    
                    var buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        // Update progress
                        if (totalBytes > 0)
                        {
                            var progress = (double)downloadedBytes / totalBytes * 100;
                            System.Diagnostics.Debug.WriteLine($"[UPDATE] Progress: {progress:F1}%");
                            // progressWindow.UpdateProgress(progress);
                        }
                    }
                }

                // Đóng progress window
                // progressWindow.Close();

                // Hỏi xác nhận cài đặt
                var confirmResult = MessageBox.Show(
                    "Tải xuống hoàn tất!\n\n" +
                    "Ứng dụng sẽ đóng và khởi động installer.\n" +
                    "Bạn có muốn tiếp tục không?",
                    "Xác nhận cài đặt",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (confirmResult == MessageBoxResult.Yes)
                {
                    // Khởi chạy installer
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = tempFile,
                        UseShellExecute = true,
                        Verb = "runas" // Request admin rights
                    };

                    Process.Start(startInfo);

                    // Đóng ứng dụng hiện tại
                    Application.Current.Shutdown();
                    return true;
                }

                return false;
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    $"Lỗi tải về cập nhật:\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(
                    $"Không có quyền truy cập:\n{ex.Message}\n\nVui lòng chạy ứng dụng với quyền Administrator.",
                    "Lỗi quyền truy cập",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi không xác định:\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra cập nhật trong nền khi khởi động (silent)
        /// </summary>
        public async Task CheckOnStartupAsync(Action<UpdateInfo>? onUpdateAvailable = null)
        {
            try
            {
                var updateInfo = await CheckForUpdatesAsync();
                
                if (updateInfo != null)
                {
                    onUpdateAvailable?.Invoke(updateInfo);
                }
            }
            catch
            {
                // Silent fail - không hiển thị lỗi khi check on startup
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}