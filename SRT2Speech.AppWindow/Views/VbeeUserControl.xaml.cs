using AutoApp.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Models;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.Socket.Client;
using SRT2Speech.Socket.Methods;
using SRT2Speech.Socket.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Interaction logic for VbeeUserControl.xaml
    /// </summary>
    public partial class VbeeUserControl : UserControl
    {
        string fileInputContent;
        string localtion = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        VbeeConfig _vbeeConfig;
        SignalRConfig _signalR;
        MessageClient _messageClient;
        ConcurrentDictionary<string, SubtitleEntry> _trackError;
        private static readonly HttpClient _httpClient = new HttpClient();

        public VbeeUserControl()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }

        private void InitDefaultValue()
        {
            _trackError = new ConcurrentDictionary<string, SubtitleEntry>();
            _signalR = YamlUtility.Deserialize<SignalRConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SignalRConfig.yaml")));
            _vbeeConfig = YamlUtility.Deserialize<VbeeConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "ConfigVbee.yaml")));
            WriteLog($"Thông tin cấu hình Vbee Thread={_vbeeConfig.MaxThreads}, SleepTime={_vbeeConfig.SleepTime}, Callback={_vbeeConfig.CallbackUrl}");
            try
            {
                //_messageClient = new MessageClient(_signalR.HubUrl, SignalMethods.SIGNAL_LOG_VBEE);
                //_ = _messageClient.CreateConncetion((SignalItem item) =>
                //{
                //    var data = JObject.Parse(item.Data.ToString()!);
                //    bool success = data.GetSafeValue<bool>("success");
                //    string message = data.GetSafeValue<string>("message");
                //    if (success)
                //    {
                //        _trackError.Remove(item.Id, out SubtitleEntry? _);
                //    }
                //    WriteLog($"{message}");
                //});
            }
            catch (Exception ex)
            {

                WriteLog($"Lỗi connect {ex.Message}");
            }

            btnDowloadError.IsEnabled = false;
            fileInputContent = string.Empty;
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

        private async Task StartT2S(List<SubtitleEntry> texts)
        {
            string directoryPath = "Files/Vbee";
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            WriteLog($"Tổng số bản ghi mp3 cần được dowload là {texts.Count}");
            _trackError.Clear();
            await Task.Run(async () =>
            {
                try
                {
                    var chunks = texts.ChunkBy(_vbeeConfig.MaxThreads);

                    foreach (var item in chunks)
                    {
                        var tasks = item.Select(async (f, index) =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Post, _vbeeConfig.Url);
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _vbeeConfig.Token);
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            var body = new
                            {
                                voice_code = _vbeeConfig.VoiceCode,
                                speed_rate = _vbeeConfig.SpeedRate,
                                input_text = f.Text,
                                app_id = _vbeeConfig.AppId,
                                callback_url = _vbeeConfig.CallbackUrl
                            };
                            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                            var response = await _httpClient.SendAsync(request);
                            _trackError.AddOrUpdate(f.Index.ToString(), f, (_, _) => f);
                            if (response.IsSuccessStatusCode)
                            {
                                var responseContent = await response.Content.ReadAsStringAsync();
                                string requestId = JObject.Parse(responseContent).GetSafeValue<dynamic>("result").request_id;
                                _trackError.Remove(f.Index.ToString(), out SubtitleEntry? _);
                                _trackError.AddOrUpdate(requestId, f, (_, _) => f);
                                _ = Task.Run(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(3));
                                    var dowload = await RetryWithJitterAndPolly1.ExecuteWithRetryAndJitterAsync(async () => await _httpClient.SendAsync(GetRqMessage(requestId)), (res) =>
                                    {
                                        bool success = res.IsSuccessStatusCode;
                                        if (success)
                                        {
                                            var dowloadContent = res.Content.ReadAsStringAsync().Result;
                                            success = dowloadContent.Contains("\"status\":\"SUCCESS\"");
                                        }
                                        return success;
                                    });
                                    var dowloadContent = await dowload.Content.ReadAsStringAsync();
                                    string audioLink = JObject.Parse(dowloadContent).GetSafeValue<dynamic>("result").audio_link;
                                    string filePath = Path.Combine(localtion, $"Files/Vbee/{f.Index}.mp3");

                                    var r = await _httpClient.GetAsync(audioLink);
                                    var stream = await r.Content.ReadAsStreamAsync();
                                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                                    {
                                        await stream.CopyToAsync(fileStream);
                                    }
                                    _trackError.Remove(requestId, out SubtitleEntry? _);
                                    WriteLog($"[DOWLOADED] Dowload thành công {f.Index}.mp3, link = {audioLink}");

                                });
                                f.RequestId = requestId;

                                WriteLog($"[SUCCESS] Gửi request - Callback: {_vbeeConfig.CallbackUrl} - {response.StatusCode} - {responseContent}");
                            }
                            else
                            {
                                WriteLog($"[ERROR] Lỗi gửi request - {response.StatusCode} - {response.ReasonPhrase}");
                            }
                        });
                        await Task.WhenAll(tasks);
                        WriteLog($"Bắt đầu nghỉ {_vbeeConfig.SleepTime} giây");
                        await Task.Delay(TimeSpan.FromSeconds(_vbeeConfig.SleepTime));
                    }
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.btnDowloadError.IsEnabled = true;
                    }));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    WriteLog($"Xuất hiện lỗi gọi sang Vbee {ex.Message}");
                }
            });
        }

        public HttpRequestMessage GetRqMessage(string requestId)
        {
            var rqGet = new HttpRequestMessage(HttpMethod.Get, $"{_vbeeConfig.Url}/{requestId}");
            rqGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _vbeeConfig.Token);
            rqGet.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return rqGet;
        }

        private void Button_Dowload(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(fileInputContent))
            {
                MessageBox.Show("Please choose file.");
                return;
            }
            WriteLog("Begin extract text from file.");
            var texts = SRTUtility.ParseSubtitleString(fileInputContent);
            WriteLog("Extract text from file done.");
            WriteLog("Begin dowload...");
            _ = StartT2S(texts);
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

        private void Button_DowloadError(object sender, RoutedEventArgs e)
        {
            if (!_trackError.Any())
            {
                MessageBox.Show("Không có bản ghi lỗi nào!!");
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Còn {_trackError.Count} bản ghi chưa được tải về. Dowload tiếp?",
                "Tải bản ghi lỗi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _ = StartT2S(new List<SubtitleEntry>(_trackError.Values));
            }
        }
    }
}
