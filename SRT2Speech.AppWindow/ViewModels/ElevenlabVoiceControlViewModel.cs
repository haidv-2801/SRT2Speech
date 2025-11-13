using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using YamlDotNet.Serialization.NamingConventions;
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
        private ConcurrentDictionary<string, SubtitleTaskItem> _trackError;
        private CancellationTokenSource? _cts;
        private bool _isProcessing;
        private int _totalCount;
        private int _processedCount;
        private int _errorCount;
        private string _statusText;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<string>? LogRequested;

        public ICommand OpenFileCommand { get; }
        public ICommand DownloadMp3Command { get; }
        public ICommand DownloadErrorCommand { get; }
        public ICommand LoadApiKeysCommand { get; }
        public ICommand ImportProxiesCommand { get; }
        public ICommand StopCommand { get; }

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

        public bool IsProcessing
        {
            get => _isProcessing;
            private set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged(nameof(IsProcessing));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount != value)
                {
                    _totalCount = value;
                    OnPropertyChanged(nameof(TotalCount));
                    OnPropertyChanged(nameof(ProgressPercent));
                }
            }
        }

        public int ProcessedCount
        {
            get => _processedCount;
            private set
            {
                if (_processedCount != value)
                {
                    _processedCount = value;
                    OnPropertyChanged(nameof(ProcessedCount));
                    OnPropertyChanged(nameof(ProgressPercent));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public int ErrorCount
        {
            get => _errorCount;
            private set
            {
                if (_errorCount != value)
                {
                    _errorCount = value;
                    OnPropertyChanged(nameof(ErrorCount));
                }
            }
        }

        public double ProgressPercent => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100.0 : 0;

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private class SubtitleTaskItem
        {
            public string SourcePath { get; init; } = string.Empty;
            public string SourceName { get; init; } = string.Empty;
            public SubtitleItem Item { get; init; } = default!;
        }

        public ElevenlabVoiceControlViewModel(IProxyManager proxyManager)
        {
            _proxyManager = proxyManager;

            OpenFileCommand = new RelayCommand(OpenFile, () => !IsProcessing);
            DownloadMp3Command = new AsyncRelayCommand(DownloadMp3Async, () => !IsProcessing);
            DownloadErrorCommand = new AsyncRelayCommand(DownloadErrorAsync, () => !IsProcessing);
            LoadApiKeysCommand = new RelayCommand(LoadApiKeys, () => !IsProcessing);
            ImportProxiesCommand = new AsyncRelayCommand(ImportProxiesAsync, () => !IsProcessing);
            StopCommand = new RelayCommand(Stop, () => IsProcessing);
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
            _trackError = new ConcurrentDictionary<string, SubtitleTaskItem>();
            
            // Helper method to get config path
            string GetConfigPath(string fileName)
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", fileName);
            }
            
            _elevenLabConfig = YamlUtility.DeserializeAuto<ElevenlabConfig>(File.ReadAllText(GetConfigPath("ElevenlabConfig.yaml")));
            _elevenLabKeyState = YamlUtility.DeserializeAuto<ElevenlabKeyState>(File.ReadAllText(GetConfigPath("ElevenlabKeyState.yaml")));
            _apiKeyManager = new ApiKeyManager(
                _elevenLabKeyState.ApiKeys,
                _elevenLabConfig.KeySelectionAlgorithm,
                TimeSpan.FromMinutes(_elevenLabConfig.QuotaResetTimeMinutes),
                GetConfigPath("ElevenlabKeyState.yaml")
            );

            if (_elevenLabConfig.EnableProxyRotation)
            {
                Log("[PROXY] Proxy rotation được kích hoạt");
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

            // Kiểm tra binding BoundProxyEndpoint cho từng key, đối chiếu với proxy pool (nếu có)
            try
            {
                if (_proxyManager != null)
                {
                    var allProxies = _proxyManager.GetAllProxiesAsync().GetAwaiter().GetResult();
                    var poolMap = allProxies.ToDictionary(p => $"{p.Host}:{p.Port}", p => p, StringComparer.OrdinalIgnoreCase);

                    int totalKeys = _elevenLabKeyState.ApiKeys.Count;
                    int missing = 0, invalid = 0, notInPool = 0, bound = 0;

                    foreach (var k in _elevenLabKeyState.ApiKeys)
                    {
                        if (string.IsNullOrWhiteSpace(k.BoundProxyEndpoint))
                        {
                            missing++;
                            Log($"[BINDING_MISSING] Key {k.Key} chưa cấu hình BoundProxyEndpoint - sẽ bị bỏ qua khi chạy (không fallback)");
                            continue;
                        }

                        if (!BoundProxyParser.TryParseBoundEndpoint(k.BoundProxyEndpoint, out var parsed, out var err))
                        {
                            invalid++;
                            Log($"[BINDING_INVALID] Key {k.Key} endpoint '{k.BoundProxyEndpoint}': {err}");
                            continue;
                        }

                        var hostPortKey = BoundProxyParser.NormalizeToHostPortKey(k.BoundProxyEndpoint);
                        if (hostPortKey == null || !poolMap.ContainsKey(hostPortKey))
                        {
                            notInPool++;
                            Log($"[BINDING_NOT_IN_POOL] Key {k.Key} endpoint {parsed!.Host}:{parsed.Port} không có trong proxy pool. Vẫn sử dụng được nhưng không có metrics từ pool.");
                        }
                        else
                        {
                            bound++;
                        }
                    }

                    Log($"[BINDING_SUMMARY] Keys: {totalKeys}, Bound: {bound}, Missing: {missing}, Invalid: {invalid}, NotInPool: {notInPool}");
                }
                else
                {
                    // Không có proxy manager: vẫn cho phép chạy theo binding tự cung cấp (không rotation, không metrics)
                    int missing = _elevenLabKeyState.ApiKeys.Count(k => string.IsNullOrWhiteSpace(k.BoundProxyEndpoint));
                    if (missing > 0)
                    {
                        Log($"[BINDING_NOTICE] ProxyManager=null. Có {missing} key chưa có BoundProxyEndpoint và sẽ bị bỏ qua khi chạy.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[BINDING_CHECK_ERROR] {ex.Message}");
            }

            _fileInputContent = string.Empty;
        }

        private void OpenFile()
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.srt)|*.srt|All files (*.*)|*.*",
                Title = "Select any SRT file (thư mục sẽ là nơi chứa file này)"
            };
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
                Log($"[FILE] Đã đọc file SRT: {Path.GetFileName(openFileDialog.FileName)} ({_fileInputContent.Length} ký tự)");
                var folderPath = Path.GetDirectoryName(FilePath) ?? "";
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var srtCount = Directory.EnumerateFiles(folderPath, "*.srt", SearchOption.TopDirectoryOnly).Count();
                    Log($"[FOLDER] Thư mục chứa file có {srtCount} file .srt sẽ được xử lý");
                }
            }
        }

        private void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Log("[CANCEL] Đã yêu cầu dừng tiến trình hiện tại");
                StatusText = "Đang dừng...";
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
            if (string.IsNullOrEmpty(FilePath))
            {
                MessageBox.Show("Vui lòng chọn file .srt để xác định thư mục.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var folderPath = Directory.Exists(FilePath) ? FilePath : Path.GetDirectoryName(FilePath);
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                MessageBox.Show("Không xác định được thư mục chứa các file .srt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            IsProcessing = true;
            ProcessedCount = 0;
            ErrorCount = 0;
            StatusText = "Bắt đầu xử lý...";

            try
            {
                var srtFiles = Directory.EnumerateFiles(folderPath, "*.srt", SearchOption.TopDirectoryOnly).ToList();
                if (!srtFiles.Any())
                {
                    MessageBox.Show("Thư mục không có file .srt nào.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Log($"[PROCESSING] Bắt đầu xử lý thư mục: {folderPath} ({srtFiles.Count} file)");
                var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
                var allItems = new List<SubtitleTaskItem>();
                foreach (var srt in srtFiles)
                {
                    using var fileStream = File.OpenRead(srt);
                    var items = parser.ParseStream(fileStream, Encoding.UTF8);
                    var name = Path.GetFileNameWithoutExtension(srt);
                    allItems.AddRange(items.Select(it => new SubtitleTaskItem
                    {
                        SourcePath = srt,
                        SourceName = name,
                        Item = it
                    }));
                }
                TotalCount = allItems.Count;
                Log($"[SUBTITLES] Đã parse tổng cộng {allItems.Count} subtitle items từ {srtFiles.Count} file SRT");
                Log($"[DOWNLOAD] Chuẩn bị tải: {allItems.Count} file MP3 với {_elevenLabConfig.MaxThreads} threads đồng thời");
                _trackError.Clear();
                await StartT2S(allItems, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("[CANCELLED] Người dùng đã dừng tiến trình");
            }
            finally
            {
                IsProcessing = false;
                StatusText = $"Kết thúc: {ProcessedCount}/{TotalCount} - Lỗi còn lại: {ErrorCount}";
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task StartT2S(List<SubtitleTaskItem> texts, CancellationToken ct)
        {
            if (texts.Any(f => f.Item.Line == "##"))
            {
                Log($"Tồn tại các dòng trống ở vị trí {string.Join(", ", texts.Where(f => f.Item.Line == "##").Select(f => $"{f.SourceName}#{f.Item.Index}"))}");
                return;
            }

            try
            {
                string url = _elevenLabConfig.Url.Replace("#key#", _elevenLabConfig.VoiceId);
                var chunks = texts.ChunkBy(_elevenLabConfig.MaxThreads);
                int batchIndex = 0;
                foreach (var item in chunks)
                {
                    ct.ThrowIfCancellationRequested();
                    batchIndex++;
    
                    var tasks = item.Select(async f =>
                    {
                        ct.ThrowIfCancellationRequested();
    
                        var errKey = $"{f.SourceName}:{f.Item.Index}";
                        _trackError.AddOrUpdate(errKey, f, (_, _) => f);
    
                        var apiKeyInfo = _apiKeyManager.GetAvailableKey();
                        if (apiKeyInfo == null)
                        {
                            Log($"[ERROR] Không có API key khả dụng cho file {f.SourceName}/{f.Item.Index}.mp3");
                            var newProcessed = Interlocked.Increment(ref _processedCount);
                            ProcessedCount = newProcessed;
                            ErrorCount = _trackError.Count;
                            StatusText = $"Đã xử lý {ProcessedCount}/{TotalCount}";
                            return;
                        }
    
                        Log($"[KEY_SELECTED] Thuật toán {_elevenLabConfig.KeySelectionAlgorithm} - Chọn key {apiKeyInfo.Key} (Used: {apiKeyInfo.UsedCount}, Priority: {apiKeyInfo.Priority}) cho file {f.SourceName}/{f.Item.Index}.mp3");
    
                        // Bắt buộc có BoundProxyEndpoint theo yêu cầu business (không fallback)
                        if (string.IsNullOrWhiteSpace(apiKeyInfo.BoundProxyEndpoint))
                        {
                            Log($"[BINDING_MISSING] Key {apiKeyInfo.Key} không có BoundProxyEndpoint - bỏ qua {f.SourceName}/{f.Item.Index}.mp3");
                            var newProcessed = Interlocked.Increment(ref _processedCount);
                            ProcessedCount = newProcessed;
                            ErrorCount = _trackError.Count;
                            StatusText = $"Đã xử lý {ProcessedCount}/{TotalCount}";
                            return;
                        }
    
                        HttpClient? clientLocal = null;
                        SRT2Speech.ProxyService.Models.ProxyInfo? poolProxy = null;
    
                        try
                        {
                            // Parse endpoint "host:port" hoặc "host:port:username:password"
                            if (!SRT2Speech.AppWindow.Services.BoundProxyParser.TryParseBoundEndpoint(apiKeyInfo.BoundProxyEndpoint, out var parsedProxy, out var parseError))
                            {
                                Log($"[BINDING_INVALID] Key {apiKeyInfo.Key} endpoint không hợp lệ: {parseError}");
                                return;
                            }
    
                            // Thử đối chiếu với pool để có ProxyId cho metrics
                            if (_proxyManager != null)
                            {
                                try
                                {
                                    var allProxies = await _proxyManager.GetAllProxiesAsync();
                                    poolProxy = allProxies.FirstOrDefault(p =>
                                        string.Equals(p.Host, parsedProxy!.Host, StringComparison.OrdinalIgnoreCase)
                                        && p.Port == parsedProxy.Port);
                                }
                                catch { /* ignore */ }
                            }
    
                            var chosen = poolProxy ?? parsedProxy!;
    
                            // Tạo HttpClient với proxy cố định
                            var handler = new SRT2Speech.ProxyService.HttpHandlers.ProxyHttpClientHandler(chosen);
                            clientLocal = new HttpClient(handler)
                            {
                                Timeout = TimeSpan.FromSeconds(30)
                            };
    
                            Log($"[KEY->PROXY] {apiKeyInfo.Key} -> {chosen.Host}:{chosen.Port}{(string.IsNullOrEmpty(chosen.Username) ? "" : " (auth)")}");
    
                            clientLocal.DefaultRequestHeaders.Remove("xi-api-key");
                            clientLocal.DefaultRequestHeaders.Add("xi-api-key", apiKeyInfo.Key);
    
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            var response = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(
                                async () => await clientLocal.PostAsync(url, GetContent(f.Item.Line), ct),
                                (res) => res.IsSuccessStatusCode
                            );
                            sw.Stop();
    
                            if (response.IsSuccessStatusCode)
                            {
                                var outDir = Path.Combine("Files/Eleven", f.SourceName);
                                if (!Directory.Exists(outDir))
                                {
                                    Directory.CreateDirectory(outDir);
                                }
                                var outPath = Path.Combine(outDir, $"{f.Item.Index}.mp3");
                                using (var fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await response.Content.CopyToAsync(fileStream, ct);
                                }
                                _trackError.Remove(errKey, out SubtitleTaskItem? _);
                                Log($"[DOWLOADED] Dowload thành công {outPath}");
    
                                if (poolProxy != null)
                                {
                                    try { await _proxyManager!.MarkProxySuccessAsync(poolProxy.Id, sw.Elapsed); } catch { /* ignore */ }
                                }
                            }
                            else
                            {
                                string contentErr = await response.Content.ReadAsStringAsync(ct);
                                Log("[ERROR]: " + contentErr);
    
                                if (poolProxy != null)
                                {
                                    try { await _proxyManager!.MarkProxyFailureAsync(poolProxy.Id, $"{response.StatusCode}"); } catch { /* ignore */ }
                                }
    
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
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        finally
                        {
                            try { clientLocal?.Dispose(); } catch { /* ignore */ }
                            var newProcessed = Interlocked.Increment(ref _processedCount);
                            ProcessedCount = newProcessed;
                            ErrorCount = _trackError.Count;
                            StatusText = $"Đã xử lý {ProcessedCount}/{TotalCount}";
                        }
                    });
    
                    await Task.WhenAll(tasks);
    
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
    
                    Log($"[BATCH] Hoàn thành batch {batchIndex}/{chunks.Count()}, nghỉ {_elevenLabConfig.SleepTime}s trước batch tiếp theo");
                    await Task.Delay(TimeSpan.FromSeconds(_elevenLabConfig.SleepTime), ct);
                }
            }
            catch (OperationCanceledException)
            {
                Log("[CANCELLED] Tiến trình đã bị dừng theo yêu cầu");
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
            else
            {
                Log("[PROXY] Sử dụng HttpClient (fallback), không sử dụng proxy");
                return new HttpClient();
            }

            throw new ArgumentNullException("Không có proxy nào hợp lệ");
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

            var texts = _trackError.Values.ToList();
            _trackError.Clear();

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            IsProcessing = true;
            ProcessedCount = 0;
            ErrorCount = 0;
            StatusText = "Bắt đầu xử lý lỗi...";

            try
            {
                TotalCount = texts.Count;
                Log($"[DOWNLOAD-ERROR] Tải lại {texts.Count} bản ghi lỗi");
                await StartT2S(texts, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("[CANCELLED] Người dùng đã dừng tiến trình (DownloadError)");
            }
            finally
            {
                IsProcessing = false;
                StatusText = $"Kết thúc: {ProcessedCount}/{TotalCount} - Lỗi còn lại: {ErrorCount}";
                _cts?.Dispose();
                _cts = null;
            }
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
                var yamlContent = YamlUtility.SerializeToHyphenated(newKeyState);
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

        private async Task ImportProxiesAsync()
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            if (_proxyManager == null)
            {
                Log("[PROXY_IMPORT] Proxy manager chưa sẵn sàng");
                MessageBox.Show("Proxy manager chưa được khởi tạo.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Log("[PROXY_IMPORT] Bắt đầu import danh sách proxy (binding ip:port:username:password)...");
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Proxy list (*.txt;*.list)|*.txt;*.list|All files (*.*)|*.*",
                    Title = "Select Proxy Binding List (host:port:username:password)"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    Log($"[PROXY_IMPORT] Đọc file: {Path.GetFileName(openFileDialog.FileName)}");
                    var lines = File.ReadAllLines(openFileDialog.FileName);
                    var imported = await _proxyManager.ImportBindingsAsync(lines, CancellationToken.None);

                    if (imported > 0)
                    {
                        Log($"[PROXY_IMPORT] Đã import {imported} proxies (Replace) và reload cấu hình");
                        var summary = _proxyManager.GetStatusSummary();
                        Log($"[PROXY] {summary}");
                        MessageBox.Show($"Đã import {imported} proxies và reload cấu hình.\n{summary}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        Log("[PROXY_IMPORT] Không có dòng hợp lệ để import");
                        MessageBox.Show("Không có dòng hợp lệ để import.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Log("[PROXY_IMPORT] Người dùng đã hủy việc chọn file");
                }
            }
            catch (Exception ex)
            {
                Log($"[PROXY_IMPORT_ERROR] Lỗi khi import proxy: {ex.Message}");
                MessageBox.Show($"Lỗi khi import proxy: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch
            {
                // ignore
            }
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