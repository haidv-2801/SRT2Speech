using AutoApp.Utility;
using Microsoft.Win32;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Views;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.Socket.Client;
using SRT2Speech.Socket.Methods;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;

namespace SRT2Speech.AppWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string fileInputContent;
        FptConfig _fptConfig;
        SignalRConfig _signalR;
        MessageClient _messageClient;
        VbeeConfig _vbeeConfig;

        public MainWindow()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }

        private void InitDefaultValue()
        {
            _fptConfig = YamlUtility.Deserialize<FptConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "ConfigFpt.yaml")));
            _signalR = YamlUtility.Deserialize<SignalRConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SignalRConfig.yaml")));
            _vbeeConfig = YamlUtility.Deserialize<VbeeConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "ConfigVbee.yaml")));

            _messageClient = new MessageClient(_signalR.HubUrl, SignalMethods.SIGNAL_LOG);
            _ = _messageClient.CreateConncetion(async (object message) =>
            {
                string msg = $"{message}";
                WriteLog(msg);
            });
            
            fileInputContent = string.Empty;
            txtLog.AppendText("Logging...");
        }

        private void InitContent()
        {
            FullWidthLog();
            CenterForm();
            InitWindow();
        }

        private void InitWindow()
        {
            VbeeUserControl vbeeControl = new VbeeUserControl();
            TabItem newTab = new TabItem();
            newTab.Header = "Vbee";
            newTab.Content = vbeeControl;
            tabControl.Items.Add(newTab);
        }

        private void FullWidthLog()
        {
            //txtLog.Width = SystemParameters.PrimaryScreenWidth - 24;
            txtLog.Height = SystemParameters.PrimaryScreenHeight - 480;
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

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.srt)|*.srt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                txtFile.Text = openFileDialog.FileName;
                if (string.IsNullOrEmpty(txtFile.Text))
                {
                    MessageBox.Show("File name empty.");
                }
                fileInputContent = File.ReadAllText(openFileDialog.FileName);
                if (string.IsNullOrEmpty(fileInputContent))
                {
                    MessageBox.Show("File no content.");
                }
                WriteLog("Read file done.");
            }
        }

        private void Button_Dowload(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(fileInputContent))
            {
                MessageBox.Show("Please choose file.");
                return;
            }
            WriteLog("Begin extract text from file.");
            var texts = SRTUtility.ExtractSrt(fileInputContent);
            WriteLog("Extract text from file done.");
            WriteLog("Begin dowload...");

            Task.Run(async () =>
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {

                        var uri = new Uri(_fptConfig.Url);
                        httpClient.DefaultRequestHeaders.Add("api-key", _fptConfig.ApiKey);
                        httpClient.DefaultRequestHeaders.Add("speed", "");
                        httpClient.DefaultRequestHeaders.Add("voice", _fptConfig.Voice);
                        httpClient.DefaultRequestHeaders.Add("callback_url", _fptConfig.CallbackUrl);
                        httpClient.DefaultRequestHeaders
                          .Accept
                          .Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header

                        var chunks = texts.ChunkBy<string>(_fptConfig.MaxThreads);
                        foreach (var item in chunks)
                        {
                            var tasks = item.Select(async f =>
                            {
                                var content = new FormUrlEncodedContent(new[]
                                {
                                    new KeyValuePair<string, string>(f, "")
                                });

                                var response = await httpClient.PostAsync(uri, content);
                                if (response.IsSuccessStatusCode)
                                {
                                    var responseContent = await response.Content.ReadAsStringAsync();
                                }
                                else
                                {
                                    WriteLog($"Lỗi gọi sang Fpt: {response.StatusCode} - {response.ReasonPhrase}");
                                }
                            });
                            await Task.WhenAll(tasks);
                            await Task.Delay(TimeSpan.FromSeconds(_fptConfig.SleepTime));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Hiển thị MessageBox với thông tin lỗi
                    MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    WriteLog($"Xuất hiện lỗi gọi sang FPT, có thể do tài khoản của bạn đã hết dung lượng, nếu chưa hết hãy thử giảm số luồng đồng thời xuống");
                }
            });
        }

        private bool WriteLog(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"{DateTime.Now} - {message}\n");
                txtLog.ScrollToEnd();
            });

            return true;
        }
    }
}