﻿using Microsoft.Win32;
using Newtonsoft.Json;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SubtitlesParser.Classes;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Interaction logic for ElevenlabVoiceControl.xaml
    /// </summary>
    public partial class ElevenlabVoiceControl : UserControl
    {
        string fileInputContent;
        bool isValidKey = true;
        ElevenlabConfig _elevenLabConfig;
        ConcurrentDictionary<string, SubtitleItem> _trackError;

        public ElevenlabVoiceControl()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }
        private void InitDefaultValue()
        {
            //ReadKey();
            if (!ThrowKeyValid())
            {
                return;
            }
            _trackError = new ConcurrentDictionary<string, SubtitleItem>();
            _elevenLabConfig = YamlUtility.Deserialize<ElevenlabConfig>(File.ReadAllText(Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ElevenlabConfig.yaml")));
            WriteLog($"Thông tin cấu hình ElevenlabConfig {JsonConvert.SerializeObject(_elevenLabConfig)}");
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

        private StringContent GetContent(string text)
        {
            var requestBody = new
            {
                voice_id = _elevenLabConfig.VoiceId,  // Adam pre-made voice
                optimize_streaming_latency = _elevenLabConfig.OptimizeStreamingLatency,
                output_format = _elevenLabConfig.OutputFormat,
                text,
                model_id = _elevenLabConfig.ModelId,
                voice_settings = new
                {
                    stability = _elevenLabConfig.VoiceSettings.Stability,
                    similarity_boost = _elevenLabConfig.VoiceSettings.SimilarityBoost,
                    style = _elevenLabConfig.VoiceSettings.Style,
                    use_speaker_boost = _elevenLabConfig.VoiceSettings.UseSpeakerBoost
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            return content;
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
            WriteLog($"Tổng số bản ghi mp3 cần được dowload là {texts.Count}");
            WriteLog("Extract text from file done.");
            WriteLog("Begin dowload...");
            _trackError.Clear();

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

        private async Task StartT2S(List<SubtitleItem> texts)
        {
            if (texts.Any(f => f.Line == "##"))
            {
                WriteLog($"Tồn tại các dòng trống ở vị trí {string.Join(", ", texts.Where(f => f.Line == "##").Select(f => f.Index))}");
                return;
            }

            await Task.Run(async () =>
            {
                try
                {
                    string apiKey = _elevenLabConfig.ApiKey;
                    string url = _elevenLabConfig.Url.Replace("#key#", _elevenLabConfig.VoiceId);
                    using (var client = new HttpClient())
                    {
                        // Add the xi-api-key header
                        client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

                        var chunks = texts.ChunkBy(_elevenLabConfig.MaxThreads);
                        foreach (var item in chunks)
                        {
                            var tasks = item.Select(async f =>
                            {
                                _trackError.AddOrUpdate(f.Index.ToString(), f, (_, _) => f);
                                var response = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(async () => await client.PostAsync(url, GetContent(f.Line)), (res) =>
                                {
                                    bool success = res.IsSuccessStatusCode;
                                    return success;
                                });
                                WriteLog($"[SUCCESS] Gửi request thành công cho file {f.Index}.mp3");
                                if (response.IsSuccessStatusCode)
                                {
                                    using (var fileStream = new FileStream($"Files/Eleven/{f.Index}.mp3", FileMode.Create, FileAccess.Write, FileShare.None))
                                    {
                                        await response.Content.CopyToAsync(fileStream);
                                    }
                                    _trackError.Remove(f.Index.ToString(), out SubtitleItem? _);
                                    WriteLog($"[DOWLOADED] Dowload thành công Files/Eleven/{f.Index}.mp3");
                                }
                                else
                                {
                                    string contentErr = await response.Content.ReadAsStringAsync();
                                    WriteLog("[ERROR]: " + contentErr);
                                }
                            });
                            await Task.WhenAll(tasks);
                            WriteLog($"Bắt đầu nghỉ {_elevenLabConfig.SleepTime} giây");
                            await Task.Delay(TimeSpan.FromSeconds(_elevenLabConfig.SleepTime));
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