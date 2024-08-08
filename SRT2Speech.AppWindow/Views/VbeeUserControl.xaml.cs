using AutoApp.Utility;
using Microsoft.Win32;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.Socket.Client;
using SRT2Speech.Socket.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using SRT2Speech.Core.Extensions;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Interaction logic for VbeeUserControl.xaml
    /// </summary>
    public partial class VbeeUserControl : UserControl
    {
        string fileInputContent;
        VbeeConfig _vbeeConfig;
        SignalRConfig _signalR;
        MessageClient _messageClient;

        public VbeeUserControl()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }

        private void InitDefaultValue()
        {
            _signalR = YamlUtility.Deserialize<SignalRConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SignalRConfig.yaml")));
            _vbeeConfig = YamlUtility.Deserialize<VbeeConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "ConfigVbee.yaml")));
            _messageClient = new MessageClient(_signalR.HubUrl, SignalMethods.SIGNAL_LOG_VBEE);
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
        }

        private void FullWidthLog()
        {
            //txtLog.Width = SystemParameters.PrimaryScreenWidth - 24;
            txtLog.Height = SystemParameters.PrimaryScreenHeight - 480;
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
                       
                        var chunks = texts.ChunkBy<string>(_vbeeConfig.MaxThreads);
                        foreach (var item in chunks)
                        {
                            var tasks = item.Select(async f =>
                            {
                                var request = new HttpRequestMessage(HttpMethod.Post, _vbeeConfig.Url);


                                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _vbeeConfig.Token);
                                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                var body = new
                                {
                                    voice_code = _vbeeConfig.VoiceCode,
                                    speed_rate = _vbeeConfig.SpeedRate,
                                    input_text = f,
                                    app_id = _vbeeConfig.AppId,
                                    callback_url = _vbeeConfig.CallbackUrl
                                };
                                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                                var response = await httpClient.SendAsync(request);
                                if (response.IsSuccessStatusCode)
                                {
                                    var responseContent = await response.Content.ReadAsStringAsync();
                                    WriteLog($"gọi sang Vbee res: {response.StatusCode} - {responseContent}");
                                }
                                else
                                {
                                    WriteLog($"Lỗi gọi sang Vbee: {response.StatusCode} - {response.ReasonPhrase}");
                                }
                            });
                            await Task.WhenAll(tasks);
                            await Task.Delay(TimeSpan.FromSeconds(_vbeeConfig.SleepTime));
                        }

                    }
                }
                catch(Exception ex) {
                    MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    WriteLog($"Xuất hiện lỗi gọi sang Vbee");
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
