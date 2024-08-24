using Microsoft.Win32;
using Newtonsoft.Json;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.Socket.Client;
using SRT2Speech.Socket.Methods;
using SubtitlesParser.Classes;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Interaction logic for EnglishVoiceControl.xaml
    /// </summary>
    public partial class EnglishVoiceControl : UserControl
    {
        string fileInputContent;
        GoogleConfig _googleConfig;
        //SignalRConfig _signalR;
        MessageClient _messageClient;

        public EnglishVoiceControl()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }
        private void InitDefaultValue()
        {
            //_signalR = YamlUtility.Deserialize<SignalRConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SignalRConfig.yaml")));
            _googleConfig = YamlUtility.Deserialize<GoogleConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "GoogleConfig.yaml")));
            //_messageClient = new MessageClient(_signalR.HubUrl, SignalMethods.SIGNAL_LOG_EN_VOICE);
            //_ = _messageClient.CreateConncetion(async (object message) =>
            //{
            //    string msg = $"{message}";
            //    WriteLog(msg);
            //});
            WriteLog($"Thông tin cấu hình Google {JsonConvert.SerializeObject(_googleConfig)}");
            fileInputContent = string.Empty;
            //txtLog.AppendText("Logging...");
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

        private StringContent GetContent(string text)
        {
            var requestBody = new
            {
                input = new { text = text },
                voice = new
                {
                    languageCode = "en-US",
                    name = _googleConfig.VoiceName
                },
                audioConfig = new { audioEncoding = _googleConfig.AudioEncoding, speakingRate = _googleConfig.SpeedRate }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            return content;
        }

        private void Button_Dowload(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(fileInputContent))
            {
                MessageBox.Show("Please choose file.");
                return;
            }
            WriteLog("Begin extract text from file.");
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using var fileStream = File.OpenRead(txtFile.Text);
            var texts = parser.ParseStream(fileStream, Encoding.UTF8);
            WriteLog($"Tổng số bản ghi mp3 cần được dowload là {texts.Count}");
            WriteLog("Extract text from file done.");
            WriteLog("Begin dowload...");

            Task.Run(async () =>
            {
                try
                {
                    string apiKey = _googleConfig.ApiKey;
                    string url = _googleConfig.Url.Replace("#key#", apiKey); ;
                    using (var client = new HttpClient())
                    {
                        var chunks = texts.ChunkBy<SubtitleItem>(_googleConfig.MaxThreads);
                        foreach (var item in chunks)
                        {
                            var tasks = item.Select(async f =>
                            {
                               

                                var response = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(async () => await client.PostAsync(url, GetContent(f.Line)), (res) =>
                                {
                                    bool success = res.IsSuccessStatusCode;
                                    if (success)
                                    {
                                        var dowloadContent = res.Content.ReadAsStringAsync().Result;
                                        success = dowloadContent.Contains("audioContent");
                                    }
                                    return success;
                                });
                                WriteLog($"[SUCCESS] Gửi request thành công cho file {f.Index}.mp3");
                                var responseContent = await response.Content.ReadAsStringAsync();
                                if (response.IsSuccessStatusCode)
                                {
                                    var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                                    var audioContent = result.audioContent;
                                    byte[] audioBytes = Convert.FromBase64String((string)audioContent);
                                    await System.IO.File.WriteAllBytesAsync($"Files/English/{f.Index}.mp3", audioBytes);
                                    WriteLog($"[DOWLOADED] Dowload thành công Files/English/{f.Index}.mp3");
                                }
                                else
                                {
                                    WriteLog("[ERROR]: " + responseContent);
                                }
                            });
                            await Task.WhenAll(tasks);
                            WriteLog($"Bắt đầu nghỉ {_googleConfig.SleepTime} giây");
                            await Task.Delay(TimeSpan.FromSeconds(_googleConfig.SleepTime));
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
