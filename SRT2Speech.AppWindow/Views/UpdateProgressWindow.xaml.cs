using System;
using System.Windows;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Window hiển thị tiến trình tải về cập nhật
    /// </summary>
    public partial class UpdateProgressWindow : Window
    {
        public UpdateProgressWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Cập nhật tiến trình tải về
        /// </summary>
        /// <param name="progress">Phần trăm hoàn thành (0-100)</param>
        public void UpdateProgress(double progress)
        {
            if (Dispatcher.CheckAccess())
            {
                progressBar.Value = progress;
                txtProgress.Text = $"{progress:F1}%";
                
                if (progress < 100)
                {
                    txtStatus.Text = "Đang tải xuống...";
                }
                else
                {
                    txtStatus.Text = "Hoàn tất!";
                }
            }
            else
            {
                Dispatcher.Invoke(() => UpdateProgress(progress));
            }
        }

        /// <summary>
        /// Cập nhật thông tin kích thước đã tải
        /// </summary>
        /// <param name="downloadedBytes">Số bytes đã tải</param>
        /// <param name="totalBytes">Tổng số bytes</param>
        public void UpdateDownloadSize(long downloadedBytes, long totalBytes)
        {
            if (Dispatcher.CheckAccess())
            {
                var downloadedMB = downloadedBytes / 1024.0 / 1024.0;
                var totalMB = totalBytes / 1024.0 / 1024.0;
                txtDownloadedSize.Text = $"{downloadedMB:F2} MB / {totalMB:F2} MB";
            }
            else
            {
                Dispatcher.Invoke(() => UpdateDownloadSize(downloadedBytes, totalBytes));
            }
        }

        /// <summary>
        /// Cập nhật trạng thái
        /// </summary>
        public void UpdateStatus(string status)
        {
            if (Dispatcher.CheckAccess())
            {
                txtStatus.Text = status;
            }
            else
            {
                Dispatcher.Invoke(() => UpdateStatus(status));
            }
        }

        /// <summary>
        /// Hiển thị lỗi
        /// </summary>
        public void ShowError(string errorMessage)
        {
            if (Dispatcher.CheckAccess())
            {
                txtStatus.Text = "Lỗi!";
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show(
                    errorMessage,
                    "Lỗi tải về",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Close();
            }
            else
            {
                Dispatcher.Invoke(() => ShowError(errorMessage));
            }
        }
    }
}