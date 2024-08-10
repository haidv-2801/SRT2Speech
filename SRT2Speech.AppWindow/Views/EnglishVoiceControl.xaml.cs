using AutoApp.Utility;
using Microsoft.Win32;
using Newtonsoft.Json;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.Socket.Client;
using SRT2Speech.Socket.Methods;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
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
using SRT2Speech.Core.Extensions;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Interaction logic for EnglishVoiceControl.xaml
    /// </summary>
    public partial class EnglishVoiceControl : UserControl
    {
        string fileInputContent;
        VbeeConfig _vbeeConfig;
        SignalRConfig _signalR;
        MessageClient _messageClient;

        public EnglishVoiceControl()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }
        private void InitDefaultValue()
        {
            _signalR = YamlUtility.Deserialize<SignalRConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SignalRConfig.yaml")));
            _vbeeConfig = YamlUtility.Deserialize<VbeeConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "ConfigVbee.yaml")));
            _messageClient = new MessageClient(_signalR.HubUrl, SignalMethods.SIGNAL_LOG_EN_VOICE);
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
                    string apiKey = "AIzaSyDCtKbU-BouZiOqBGNvuSdpVaNCfqD0a64";
                    string url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";
                    using (var client = new HttpClient())
                    {
                        var chunks = texts.ChunkBy<string>(_vbeeConfig.MaxThreads);
                        foreach (var item in chunks)
                        {
                            var tasks = item.Select(async f =>
                            {
                                var requestBody = new
                                {
                                    input = new { text = f},
                                    voice = new
                                    {
                                        languageCode = "en-US",
                                        name = "en-US-Studio-O"
                                    },
                                    audioConfig = new { audioEncoding = "MP3", speakingRate = "1" }
                                };

                                var jsonContent = JsonConvert.SerializeObject(requestBody);
                                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                                var response = await client.PostAsync(url, content);

                                var responseContent = await response.Content.ReadAsStringAsync();
                                if (response.IsSuccessStatusCode)
                                {
                                    var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                                    var audioContent = result.audioContent;

                                    // Lưu tệp MP3
                                    byte[] audioBytes = Convert.FromBase64String((string)audioContent);
                                    await System.IO.File.WriteAllBytesAsync($"OUTPUT/{Guid.NewGuid()}_output.mp3", audioBytes);
                                    WriteLog("Audio file saved as output.mp3");
                                }
                                else
                                {
                                    WriteLog("Error: " + responseContent);
                                }
                            });
                            await Task.WhenAll(tasks);
                            await Task.Delay(TimeSpan.FromSeconds(_vbeeConfig.SleepTime));
                        }

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    WriteLog($"Xuất hiện lỗi gọi sang Google");
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
