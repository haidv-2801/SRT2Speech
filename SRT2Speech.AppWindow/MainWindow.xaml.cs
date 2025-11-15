using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.AppWindow.Views;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SubtitlesParser.Classes;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using SRT2Speech.ProxyService.Interfaces;

namespace SRT2Speech.AppWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UpdateService? _updateService;

        public MainWindow()
        {
            InitializeComponent();
            InitContent();
            InitializeUpdateService();
        }
      
       

        private void InitContent()
        {
            CenterForm();
            InitWindow();
        }

        private void InitWindow()
        {
            var proxyManager = ServiceLocator.GetService<IProxyManager>();
            if (proxyManager == null)
            {
                MessageBox.Show("Error: IProxyManager service not available", "Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ElevenlabVoiceControl elevenLabControl = new ElevenlabVoiceControl(proxyManager);
            TabItem newTab4 = new TabItem();
            newTab4.Header = "Elevenlab";
            newTab4.Content = elevenLabControl;
            newTab4.IsEnabled = true;
            tabControl.Items.Add(newTab4);

            // Set focus to Elevenlab tab
            tabControl.SelectedItem = newTab4;
        }

        private void CenterForm()
        {
            // Get the screen dimensions
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            // Get the window dimensions
            double windowWidth = this.Width;
            double windowHeight = this.Height;

            // Calculate the left and top positions to center the window
            double left = (screenWidth - windowWidth) / 2;
            double top = (screenHeight - windowHeight) / 2;

            // Set the window's position
            this.Left = left;
            this.Top = top;
        }

        private void InitializeUpdateService()
        {
            // Khởi tạo UpdateService
            _updateService = new UpdateService();

            // Kiểm tra cập nhật khi khởi động (background, silent)
            _ = Task.Run(async () =>
            {
                await _updateService.CheckOnStartupAsync(updateInfo =>
                {
                    // Hiển thị notification nếu có update
                    Dispatcher.Invoke(() =>
                    {
                        ShowUpdateNotification(updateInfo);
                    });
                });
            });
        }

        private void ShowUpdateNotification(UpdateInfo updateInfo)
        {
            var result = MessageBox.Show(
                $"Phiên bản mới {updateInfo.LatestVersion} đã có!\n\n" +
                $"Vào Menu > Trợ giúp > Kiểm tra cập nhật để xem chi tiết.",
                "Cập nhật mới",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information
            );

            if (result == MessageBoxResult.Yes)
            {
                MenuItemCheckUpdate_Click(this, new RoutedEventArgs());
            }
        }

        private async void MenuItemCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_updateService == null)
            {
                MessageBox.Show(
                    "Dịch vụ cập nhật chưa sẵn sàng!",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            try
            {
                // Disable menu while checking
                var menuItem = sender as MenuItem;
                if (menuItem != null)
                {
                    menuItem.IsEnabled = false;
                }

                await _updateService.PromptAndUpdateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi kiểm tra cập nhật:\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                // Re-enable menu
                var menuItem = sender as MenuItem;
                if (menuItem != null)
                {
                    menuItem.IsEnabled = true;
                }
            }
        }

        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            var version = _updateService?.GetCurrentVersion() ?? new Version(1, 0, 0);
            var aboutMessage = $"SRT2Speech\n\n" +
                             $"Phiên bản: {version}\n" +
                             $"Copyright © 2024\n\n" +
                             $"Ứng dụng chuyển đổi file phụ đề SRT thành giọng nói\n" +
                             $"sử dụng các dịch vụ Text-to-Speech (TTS).\n\n" +
                             $"Hỗ trợ: ElevenLabs, FPT, Vbee, Google";

            MessageBox.Show(
                aboutMessage,
                "Về SRT2Speech",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _updateService?.Dispose();
        }
    }
}