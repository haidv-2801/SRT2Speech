using Microsoft.Win32;
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
        ElevenlabKeyState _elevenLabKeyState;
        ApiKeyManager _apiKeyManager;
        ConcurrentDictionary<string, SubtitleItem> _trackError;

        public ElevenlabVoiceControl()
        {
            InitializeComponent();
            CreateFolders();
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
            _elevenLabKeyState = YamlUtility.Deserialize<ElevenlabKeyState>(File.ReadAllText(Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ElevenlabKeyState.yaml")));
            _apiKeyManager = new ApiKeyManager(
                _elevenLabKeyState.ApiKeys,
                _elevenLabConfig.KeySelectionAlgorithm,
                TimeSpan.FromMinutes(_elevenLabConfig.QuotaResetTimeMinutes),
                Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ElevenlabKeyState.yaml")
            );
            WriteLog($"[CONFIG] ===== CẤU HÌNH ELEVENLAB =====");
            WriteLog($"  Voice ID: {_elevenLabConfig.VoiceId}");
            WriteLog($"  Model: {_elevenLabConfig.ModelId}");
            WriteLog($"  Output: {_elevenLabConfig.OutputFormat}");
            WriteLog($"  Language: {_elevenLabConfig.LanguageCode}");
            WriteLog($"  Thuật toán chọn key: {_elevenLabConfig.KeySelectionAlgorithm}");
            WriteLog($"  Số lượng API keys: {_elevenLabKeyState.ApiKeys.Count}");
            WriteLog($"  Thời gian reset quota: {_elevenLabConfig.QuotaResetTimeMinutes} phút");
            WriteLog($"  Max threads: {_elevenLabConfig.MaxThreads}, Sleep time: {_elevenLabConfig.SleepTime}s");
            WriteLog($"  Voice settings - Stability: {_elevenLabConfig.VoiceSettings.Stability}, Similarity: {_elevenLabConfig.VoiceSettings.SimilarityBoost}");
            WriteLog($"[KEY_STATE] {_apiKeyManager.GetKeyStatusSummary()}");
            fileInputContent = string.Empty;
        }

        private bool CreateFolders()
        {
            try
            {
                var curDirect = Directory.GetCurrentDirectory();
                var eleven = Path.Combine(curDirect, "Files/Eleven");
                if (!Directory.Exists(eleven))
                {
                    Directory.CreateDirectory(eleven);
                }
             
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
            }

            return true;
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
                WriteLog($"[FILE] Đã đọc file SRT thành công: {Path.GetFileName(openFileDialog.FileName)} ({fileInputContent.Length} ký tự)");
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
                language_code = _elevenLabConfig.LanguageCode,
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
            WriteLog($"[PROCESSING] Bắt đầu xử lý file: {Path.GetFileName(txtFile.Text)}");
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using var fileStream = File.OpenRead(txtFile.Text);
            var texts = parser.ParseStream(fileStream, Encoding.UTF8);
            WriteLog($"[SUBTITLES] Đã parse {texts.Count} subtitle items từ file SRT");
            WriteLog($"[DOWNLOAD] Bắt đầu tải {texts.Count} file MP3 với {_elevenLabConfig.MaxThreads} threads đồng thời");
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
                    string url = _elevenLabConfig.Url.Replace("#key#", _elevenLabConfig.VoiceId);
                    using (var client = new HttpClient())
                    {

                        var chunks = texts.ChunkBy(_elevenLabConfig.MaxThreads);
                        foreach (var item in chunks)
                        {
                            var tasks = item.Select(async f =>
                            {
                                _trackError.AddOrUpdate(f.Index.ToString(), f, (_, _) => f);

                                // Get available API key
                                var apiKeyInfo = _apiKeyManager.GetAvailableKey();
                                if (apiKeyInfo == null)
                                {
                                    WriteLog($"[ERROR] Không có API key khả dụng cho file {f.Index}.mp3");
                                    return;
                                }

                                WriteLog($"[KEY_SELECTED] Thuật toán {_elevenLabConfig.KeySelectionAlgorithm} - Chọn key {apiKeyInfo.Key} (Used: {apiKeyInfo.UsedCount}, Priority: {apiKeyInfo.Priority}) cho file {f.Index}.mp3");

                                // Add the xi-api-key header
                                client.DefaultRequestHeaders.Remove("xi-api-key");
                                client.DefaultRequestHeaders.Add("xi-api-key", apiKeyInfo.Key);

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

                                    // Check for quota exceeded or invalid API key
                                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                        contentErr.Contains("quota exceeded") ||
                                        contentErr.Contains("rate limit") ||
                                        contentErr.Contains("invalid_api_key") ||
                                        contentErr.Contains("detected_unusual_activity"))
                                    {
                                        _apiKeyManager.MarkKeyExhausted(apiKeyInfo.Key, response);
                                        WriteLog($"[KEY_EXHAUSTED] Key {apiKeyInfo.Key} đã bị khóa do vượt quota hoặc lỗi API key (Status: {response.StatusCode})");
                                    }
                                }
                            });
                            await Task.WhenAll(tasks);
                            WriteLog($"[BATCH] Hoàn thành batch {chunks.ToList().IndexOf(item) + 1}/{chunks.Count()}, nghỉ {_elevenLabConfig.SleepTime}s trước batch tiếp theo");
                            await Task.Delay(TimeSpan.FromSeconds(_elevenLabConfig.SleepTime));
                        }

                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"[ERROR] Lỗi xử lý: {ex.Message}");
                    MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void Button_LoadApiKeys(object sender, RoutedEventArgs e)
        {
            if (!ThrowKeyValid())
            {
                return;
            }

            try
            {
                WriteLog("[KEY_LOAD] Bắt đầu tải API keys từ file...");
                
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.Title = "Select API Keys File";
                
                if (openFileDialog.ShowDialog() == true)
                {
                    var apiKeys = LoadApiKeysFromFile(openFileDialog.FileName);
                    
                    if (apiKeys != null && apiKeys.Count > 0)
                    {
                        UpdateKeyStateFile(apiKeys);
                        RefreshApiKeyManager(apiKeys);
                        WriteLog($"[KEY_LOAD] Đã tải thành công {apiKeys.Count} API keys từ file: {Path.GetFileName(openFileDialog.FileName)}");
                        MessageBox.Show($"Đã tải thành công {apiKeys.Count} API keys!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        WriteLog("[KEY_LOAD] Không tìm thấy API key hợp lệ trong file");
                        MessageBox.Show("Không tìm thấy API key hợp lệ trong file!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    WriteLog("[KEY_LOAD] Người dùng đã hủy việc chọn file");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[KEY_LOAD_ERROR] Lỗi khi tải API keys: {ex.Message}");
                MessageBox.Show($"Lỗi khi tải API keys: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> LoadApiKeysFromFile(string filePath)
        {
            try
            {
                WriteLog($"[KEY_LOAD] Đọc file: {Path.GetFileName(filePath)}");
                
                var lines = File.ReadAllLines(filePath);
                var apiKeys = new List<string>();
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("#"))
                    {
                        apiKeys.Add(trimmedLine);
                    }
                }
                
                WriteLog($"[KEY_LOAD] Đã đọc {lines.Length} dòng, tìm thấy {apiKeys.Count} API keys hợp lệ");
                return apiKeys;
            }
            catch (Exception ex)
            {
                WriteLog($"[KEY_LOAD_ERROR] Lỗi khi đọc file: {ex.Message}");
                throw new Exception($"Không thể đọc file: {ex.Message}");
            }
        }

        private void UpdateKeyStateFile(List<string> apiKeys)
        {
            try
            {
                WriteLog("[KEY_LOAD] Cập nhật file ElevenlabKeyState.yaml...");

                var keyStatePath = Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ElevenlabKeyState.yaml");

                // Create new key state with loaded keys
                var newKeyState = new ElevenlabKeyState
                {
                    ApiKeys = apiKeys.Select(key => new ApiKeyInfo
                    {
                        Key = key,
                        Available = true,
                        UsedCount = 0,
                        Priority = 0,
                        CooldownUntil = null
                    }).ToList(),
                    LastSaved = DateTime.UtcNow,
                    KeySelectionAlgorithm = _elevenLabConfig.KeySelectionAlgorithm
                };

                // Serialize and save to file
                var yamlContent = YamlUtility.Serialize(newKeyState);
                File.WriteAllText(keyStatePath, yamlContent);

                // Update the in-memory key state
                _elevenLabKeyState = newKeyState;

                WriteLog($"[KEY_LOAD] Đã cập nhật thành công file key state với {apiKeys.Count} keys");
            }
            catch (Exception ex)
            {
                WriteLog($"[KEY_LOAD_ERROR] Lỗi khi cập nhật file key state: {ex.Message}");
                throw new Exception($"Không thể cập nhật file key state: {ex.Message}");
            }
        }

        private void RefreshApiKeyManager(List<string> apiKeys)
        {
            try
            {
                WriteLog("[KEY_LOAD] Làm mới ApiKeyManager...");

                // Dispose old manager
                (_apiKeyManager as IDisposable)?.Dispose();

                // Create new ApiKeyInfo objects
                var apiKeyInfos = apiKeys.Select(key => new ApiKeyInfo
                {
                    Key = key,
                    Available = true,
                    UsedCount = 0,
                    Priority = 0,
                    CooldownUntil = null
                }).ToList();

                // Create new ApiKeyManager
                _apiKeyManager = new ApiKeyManager(
                    apiKeyInfos,
                    _elevenLabConfig.KeySelectionAlgorithm,
                    TimeSpan.FromMinutes(_elevenLabConfig.QuotaResetTimeMinutes),
                    Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ElevenlabKeyState.yaml")
                );

                WriteLog($"[KEY_LOAD] Đã làm mới thành công ApiKeyManager với {apiKeys.Count} keys");
                WriteLog($"[KEY_STATE] {_apiKeyManager.GetKeyStatusSummary()}");
            }
            catch (Exception ex)
            {
                WriteLog($"[KEY_LOAD_ERROR] Lỗi khi làm mới ApiKeyManager: {ex.Message}");
                throw new Exception($"Không thể làm mới ApiKeyManager: {ex.Message}");
            }
        }

        ~ElevenlabVoiceControl()
        {
            // Dispose ApiKeyManager to save final state
            (_apiKeyManager as IDisposable)?.Dispose();
        }
    }
}
