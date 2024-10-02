using AutoApp.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Models;
using SRT2Speech.Core.Utilitys;
using SubtitlesParser.Classes;
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
        bool isValidKey = false;
        VbeeConfig _vbeeConfig;
        SignalRConfig _signalR;
        ConcurrentDictionary<string, SubtitleItem> _trackError;
        private static readonly HttpClient _httpClient = new HttpClient();

        public VbeeUserControl()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }

        private void InitDefaultValue()
        {
            ReadKey();
            if (!ThrowKeyValid())
            {
                return;
            }
            _trackError = new ConcurrentDictionary<string, SubtitleItem>();
            _vbeeConfig = YamlUtility.Deserialize<VbeeConfig>(File.ReadAllText(System.IO.Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ConfigVbee.yaml")));
            WriteLog($"Thông tin cấu hình Vbee {JsonConvert.SerializeObject(_vbeeConfig)}");
            btnDowloadError.IsEnabled = false;
            fileInputContent = string.Empty;
        }

        private bool ThrowKeyValid()
        {
            if (!isValidKey)
            {
                WriteLog($"Key app không hợp lệ!!!");
            }

            return isValidKey;
        }

        private void ReadKey()
        {
            try
            {
                string key = File.ReadAllText(System.IO.Path.Combine($@"{Directory.GetCurrentDirectory()}\Configs", "key.txt"));
                string decrypt = AESEncryption.DecryptAES(key);
                string date = decrypt.Split("__")[1];
                string mac = decrypt.Split("__")[0];
                if (string.IsNullOrEmpty(key))
                {
                    isValidKey = false;
                }
                isValidKey = EncodeUtility.IsValidKey(key);
                WriteLog($"Thông tin key: {key}, Valid key = {isValidKey}, Date = {date}, mac = {mac}");
            }
            catch (Exception ex)
            {
                isValidKey = false;
                WriteLog(ex.Message);
            }
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
            if (!ThrowKeyValid())
            {
                return;
            }
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

        private HttpRequestMessage GetVbeeRq(string text)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _vbeeConfig.Url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _vbeeConfig.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var body = new
            {
                voice_code = _vbeeConfig.VoiceCode,
                speed_rate = _vbeeConfig.SpeedRate,
                input_text = text,
                app_id = _vbeeConfig.AppId,
                callback_url = _vbeeConfig.CallbackUrl
            };
            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return request;
        }

        private async Task StartT2S(List<SubtitleItem> texts)
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            string directoryPath = "Files/Vbee";
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (texts.Any(f => f.Line == "##"))
            {
                WriteLog($"Tồn tại các dòng trống ở vị trí {string.Join(", ", texts.Where(f => f.Line == "##").Select(f => f.Index))}");
                return;
            }

            WriteLog($"Tổng số bản ghi mp3 cần được dowload là {texts.Count}");
            this.Dispatcher.Invoke(new Action(() =>
            {
                this.btnDowloadError.IsEnabled = false;
                this.btnDowload.IsEnabled = false;
            }));
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
                            var response = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(async () => await _httpClient.SendAsync(GetVbeeRq(f.Line)), (res) =>
                            {
                                if (!res.IsSuccessStatusCode)
                                {
                                    WriteLog($"Call {f.Index}.mp3 lỗi {res.ReasonPhrase}");
                                }
                                return res.IsSuccessStatusCode;
                            }, maxRetries: 3);
                            _trackError.AddOrUpdate(f.Index.ToString(), f, (_, _) => f);
                            if (response.IsSuccessStatusCode)
                            {
                                var responseContent = await response.Content.ReadAsStringAsync();
                                string requestId = JObject.Parse(responseContent).GetSafeValue<dynamic>("result").request_id;
                                _ = Task.Run(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(2));
                                    var dowload = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(async () => await _httpClient.SendAsync(GetRqMessage(requestId)), (res) =>
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
                                    _trackError.Remove(f.Index.ToString(), out SubtitleItem? _);
                                    WriteLog($"[DOWLOADED] Dowload thành công {f.Index}.mp3, link = {audioLink}");

                                });
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
                    if (_trackError.Any()) WriteLog($"Đã dowload xong còn {_trackError.Count} bản ghi chưa được dowload");
                    else WriteLog($"Đã dowload xong {texts.Count} bản ghi");
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.btnDowloadError.IsEnabled = true;
                        this.btnDowload.IsEnabled = true;
                    }));
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.btnDowloadError.IsEnabled = true;
                        this.btnDowload.IsEnabled = true;
                    }));
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
            if (!ThrowKeyValid())
            {
                return;
            }
            if (string.IsNullOrEmpty(fileInputContent))
            {
                MessageBox.Show("Please choose file.");
                return;
            }
            WriteLog("Begin extract text from file.");
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using var fileStream = File.OpenRead(txtFile.Text);
            var texts = parser.ParseStream(fileStream, Encoding.UTF8);

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
            if (!ThrowKeyValid())
            {
                return;
            }
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
                var errIndexs = new HashSet<string>(_trackError.Keys);
                _trackError.Clear();

                var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
                using var fileStream = File.OpenRead(txtFile.Text);
                var texts = parser.ParseStream(fileStream, Encoding.UTF8);
                texts = texts.Where(f => errIndexs.Contains(f.Index.ToString())).ToList();

                if (!texts.Any())
                {
                    MessageBox.Show("Không có bản ghi lỗi nào!!");
                    return;
                }

                _ = StartT2S(texts);
            }
        }
    }
}
