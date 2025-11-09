using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.ProxyService.Interfaces;
using SubtitlesParser.Classes;

namespace SRT2Speech.AppWindow.ViewModels
{
    public class ElevenlabVoiceControlViewModel : INotifyPropertyChanged, IDisposable
    {
        private string _filePath;
        private string _fileInputContent;
        private bool _isValidKey = true;
        private readonly IProxyManager _proxyManager;
        private ElevenlabConfig _elevenLabConfig;
        private ElevenlabKeyState _elevenLabKeyState;
        private ApiKeyManager _apiKeyManager;
        private ConcurrentDictionary<string, SubtitleItem> _trackError;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<string>? LogRequested;

        public ICommand OpenFileCommand { get; }
        public ICommand DownloadMp3Command { get; }
        public ICommand DownloadErrorCommand { get; }
        public ICommand LoadApiKeysCommand { get; }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        public ElevenlabVoiceControlViewModel(IProxyManager proxyManager)
        {
            _proxyManager = proxyManager;

            OpenFileCommand = new RelayCommand(OpenFile);
            DownloadMp3Command = new AsyncRelayCommand(DownloadMp3Async);
            DownloadErrorCommand = new AsyncRelayCommand(DownloadErrorAsync);
            LoadApiKeysCommand = new RelayCommand(LoadApiKeys);
        }

        public void Initialize()
        {
            CreateFolders();
            InitDefaultValue();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Log(string message)
        {
            LogRequested?.Invoke(this, message);
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
                    Log("/Files/Eleven created");
                }

            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
            return true;
        }

        private bool ThrowKeyValid()
        {
            if (!_isValidKey)
            {
                Log("Key app không hợp lệ!!!");
            }
            return _isValidKey;
        }

        private void InitDefaultValue()
        {
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

            if (_elevenLabConfig.EnableProxyRotation)
            {
                Log("[PROXY] Proxy rotation được kích hoạt");
                Log($"[PROXY] Strategy: {_elevenLabConfig.ProxyRotationStrategy}");
                Log($"[PROXY] Max retries: {_elevenLabConfig.ProxyMaxRetries}");
                Log($"[PROXY] Health check: {_elevenLabConfig.EnableProxyHealthCheck}");
            }

            Log("[CONFIG] ===== CẤU HÌNH ELEVENLAB =====");
            Log($"  Voice ID: {_elevenLabConfig.VoiceId}");
            Log($"  Model: {_elevenLabConfig.ModelId}");
            Log($"  Output: {_elevenLabConfig.OutputFormat}");
            Log($"  Language: {_elevenLabConfig.LanguageCode}");
            Log($"  Thuật toán chọn key: {_elevenLabConfig.KeySelectionAlgorithm}");
            Log($"  Số lượng API keys: {_elevenLabKeyState.ApiKeys.Count}");
            Log($"  Thời gian reset quota: {_elevenLabConfig.QuotaResetTimeMinutes} phút");
            Log($"  Max threads: {_elevenLabConfig.MaxThreads}, Sleep time: {_elevenLabConfig.SleepTime}s");
            Log($"  Voice settings - Stability: {_elevenLabConfig.VoiceSettings.Stability}, Similarity: {_elevenLabConfig.VoiceSettings.SimilarityBoost}");
            Log($"[KEY_STATE] {_apiKeyManager.GetKeyStatusSummary()}");
            _fileInputContent = string.Empty;
        }

        private void OpenFile()
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.srt)|*.srt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                FilePath = openFileDialog.FileName;
                if (string.IsNullOrEmpty(FilePath))
                {
                    MessageBox.Show("File name empty.");
                }
                _fileInputContent = File.ReadAllText(openFileDialog.FileName);
                if (string.IsNullOrEmpty(_fileInputContent))
                {
                    MessageBox.Show("File no content.");
                }
                Log($"[FILE] Đã đọc file SRT thành công: {Path.GetFileName(openFileDialog.FileName)} ({_fileInputContent.Length} ký tự)");
            }
        }

        private StringContent GetContent(string text)
        {
            var requestBody = new
            {
                voice_id = _elevenLabConfig.VoiceId,
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
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            return content;
        }

        private async Task DownloadMp3Async()
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            if (string.IsNullOrEmpty(_fileInputContent))
            {
                MessageBox.Show("Please choose file.");
                return;
            }
            Log($"[PROCESSING] Bắt đầu xử lý file: {Path.GetFileName(FilePath)}");
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using var fileStream = File.OpenRead(FilePath);
            var texts = parser.ParseStream(fileStream, Encoding.UTF8);
            Log($"[SUBTITLES] Đã parse {texts.Count} subtitle items từ file SRT");
            Log($"[DOWNLOAD] Bắt đầu tải {texts.Count} file MP3 với {_elevenLabConfig.MaxThreads} threads đồng thời");
            _trackError.Clear();
            await StartT2S(texts);
        }

        private async Task StartT2S(List<SubtitleItem> texts)
        {
            if (texts.Any(f => f.Line == "##"))
            {
                Log($"Tồn tại các dòng trống ở vị trí {string.Join(", ", texts.Where(f => f.Line == "##").Select(f => f.Index))}");
                return;
            }

            try
            {
                string url = _elevenLabConfig.Url.Replace("#key#", _elevenLabConfig.VoiceId);
                using (var client = await CreateHttpClientWithProxyAsync())
                {
                    var chunks = texts.ChunkBy(_elevenLabConfig.MaxThreads);
                    foreach (var item in chunks)
                    {
                        var tasks = item.Select(async f =>
                        {
                            _trackError.AddOrUpdate(f.Index.ToString(), f, (_, _) => f);

                            var apiKeyInfo = _apiKeyManager.GetAvailableKey();
                            if (apiKeyInfo == null)
                            {
                                Log($"[ERROR] Không có API key khả dụng cho file {f.Index}.mp3");
                                return;
                            }

                            Log($"[KEY_SELECTED] Thuật toán {_elevenLabConfig.KeySelectionAlgorithm} - Chọn key {apiKeyInfo.Key} (Used: {apiKeyInfo.UsedCount}, Priority: {apiKeyInfo.Priority}) cho file {f.Index}.mp3");

                            client.DefaultRequestHeaders.Remove("xi-api-key");
                            client.DefaultRequestHeaders.Add("xi-api-key", apiKeyInfo.Key);

                            var response = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(async () => await client.PostAsync(url, GetContent(f.Line)), (res) =>
                            {
                                bool success = res.IsSuccessStatusCode;
                                return success;
                            });
                            Log($"[SUCCESS] Gửi request thành công cho file {f.Index}.mp3");
                            if (response.IsSuccessStatusCode)
                            {
                                using (var fileStream = new FileStream($"Files/Eleven/{f.Index}.mp3", FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await response.Content.CopyToAsync(fileStream);
                                }
                                _trackError.Remove(f.Index.ToString(), out SubtitleItem? _);
                                Log($"[DOWLOADED] Dowload thành công Files/Eleven/{f.Index}.mp3");
                            }
                            else
                            {
                                string contentErr = await response.Content.ReadAsStringAsync();
                                Log("[ERROR]: " + contentErr);

                                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                    contentErr.Contains("quota exceeded") ||
                                    contentErr.Contains("rate limit") ||
                                    contentErr.Contains("invalid_api_key") ||
                                    contentErr.Contains("detected_unusual_activity"))
                                {
                                    _apiKeyManager.MarkKeyExhausted(apiKeyInfo.Key, response);
                                    Log($"[KEY_EXHAUSTED] Key {apiKeyInfo.Key} đã bị khóa do vượt quota hoặc lỗi API key (Status: {response.StatusCode})");
                                }
                            }
                        });
                        await Task.WhenAll(tasks);
                        Log($"[BATCH] Hoàn thành batch {chunks.ToList().IndexOf(item) + 1}/{chunks.Count()}, nghỉ {_elevenLabConfig.SleepTime}s trước batch tiếp theo");
                        await Task.Delay(TimeSpan.FromSeconds(_elevenLabConfig.SleepTime));
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Lỗi xử lý: {ex.Message}");
                MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<HttpClient> CreateHttpClientWithProxyAsync()
        {
            if (_elevenLabConfig.EnableProxyRotation && _proxyManager != null)
            {
                try
                {
                    var client = await _proxyManager.GetHttpClientWithProxyAsync();
                    Log("[PROXY] Đã tạo HttpClient với proxy rotation");
                    return client;
                }
                catch (Exception ex)
                {
                    Log($"[PROXY_ERROR] Lỗi khi tạo HttpClient với proxy: {ex.Message}");
                }
            }
            Log("[PROXY] Sử dụng HttpClient (fallback), không sử dụng proxy");
            return new HttpClient();
        }

        private async Task DownloadErrorAsync()
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
            var result = MessageBox.Show(
                $"Còn {_trackError.Count} bản ghi chưa được tải về. Dowload tiếp?",
                "Tải bản ghi lỗi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            var errIndexs = new HashSet<string>(_trackError.Keys);
            _trackError.Clear();
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using var fileStream = File.OpenRead(FilePath);
            var texts = parser.ParseStream(fileStream, Encoding.UTF8);
            texts = texts.Where(f => errIndexs.Contains(f.Index.ToString())).ToList();
            if (!texts.Any())
            {
                MessageBox.Show("Không có bản ghi lỗi nào!!");
                return;
            }
            await StartT2S(texts);
        }

        private void LoadApiKeys()
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            try
            {
                Log("[KEY_LOAD] Bắt đầu tải API keys từ file...");
                var openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.Title = "Select API Keys File";
                if (openFileDialog.ShowDialog() == true)
                {
                    var apiKeys = LoadApiKeysFromFile(openFileDialog.FileName);
                    if (apiKeys != null && apiKeys.Count > 0)
                    {
                        UpdateKeyStateFile(apiKeys);
                        RefreshApiKeyManager(apiKeys);
                        Log($"[KEY_LOAD] Đã tải thành công {apiKeys.Count} API keys từ file: {Path.GetFileName(openFileDialog.FileName)}");
                        MessageBox.Show($"Đã tải thành công {apiKeys.Count} API keys!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        Log("[KEY_LOAD] Không tìm thấy API key hợp lệ trong file");
                        MessageBox.Show("Không tìm thấy API key hợp lệ trong file!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Log("[KEY_LOAD] Người dùng đã hủy việc chọn file");
                }
            }
            catch (Exception ex)
            {
                Log($"[KEY_LOAD_ERROR] Lỗi khi tải API keys: {ex.Message}");
                MessageBox.Show($"Lỗi khi tải API keys: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> LoadApiKeysFromFile(string filePath)
        {
            try
            {
                Log($"[KEY_LOAD] Đọc file: {Path.GetFileName(filePath)}");
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
                Log($"[KEY_LOAD] Đã đọc {lines.Length} dòng, tìm thấy {apiKeys.Count} API keys hợp lệ");
                return apiKeys;
            }
            catch (Exception ex)
            {
                Log($"[KEY_LOAD_ERROR] Lỗi khi đọc file: {ex.Message}");
                throw new Exception($"Không thể đọc file: {ex.Message}");
            }
        }

        private void UpdateKeyStateFile(List<string> apiKeys)
        {
            try
            {
                Log("[KEY_LOAD] Cập nhật file ElevenlabKeyState.yaml...");
                var keyStatePath = Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ElevenlabKeyState.yaml");
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
                var yamlContent = YamlUtility.Serialize(newKeyState);
                File.WriteAllText(keyStatePath, yamlContent);
                _elevenLabKeyState = newKeyState;
                Log($"[KEY_LOAD] Đã cập nhật thành công file key state với {apiKeys.Count} keys");
            }
            catch (Exception ex)
            {
                Log($"[KEY_LOAD_ERROR] Lỗi khi cập nhật file key state: {ex.Message}");
                throw new Exception($"Không thể cập nhật file key state: {ex.Message}");
            }
        }

        private void RefreshApiKeyManager(List<string> apiKeys)
        {
            try
            {
                Log("[KEY_LOAD] Làm mới ApiKeyManager...");
                (_apiKeyManager as IDisposable)?.Dispose();
                var apiKeyInfos = apiKeys.Select(key => new ApiKeyInfo
                {
                    Key = key,
                    Available = true,
                    UsedCount = 0,
                    Priority = 0,
                    CooldownUntil = null
                }).ToList();
                _apiKeyManager = new ApiKeyManager(
                    apiKeyInfos,
                    _elevenLabConfig.KeySelectionAlgorithm,
                    TimeSpan.FromMinutes(_elevenLabConfig.QuotaResetTimeMinutes),
                    Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ElevenlabKeyState.yaml")
                );
                Log($"[KEY_LOAD] Đã làm mới thành công ApiKeyManager với {apiKeys.Count} keys");
                Log($"[KEY_STATE] {_apiKeyManager.GetKeyStatusSummary()}");
            }
            catch (Exception ex)
            {
                Log($"[KEY_LOAD_ERROR] Lỗi khi làm mới ApiKeyManager: {ex.Message}");
                throw new Exception($"Không thể làm mới ApiKeyManager: {ex.Message}");
            }
        }

        public void Dispose()
        {
            (_apiKeyManager as IDisposable)?.Dispose();
        }

        ~ElevenlabVoiceControlViewModel()
        {
            try
            {
                Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool>? _canExecute;
        private bool _isRunning;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

       public event EventHandler? CanExecuteChanged
       {
           add { CommandManager.RequerySuggested += value; }
           remove { CommandManager.RequerySuggested -= value; }
       }

        public bool CanExecute(object? parameter)
        {
            return !_isRunning && (_canExecute?.Invoke() ?? true);
        }

        public async void Execute(object? parameter)
        {
            if (_isRunning) return;
            _isRunning = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _executeAsync();
            }
            finally
            {
                _isRunning = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}