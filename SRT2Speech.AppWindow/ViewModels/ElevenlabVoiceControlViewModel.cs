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
        private bool _useProxy = false;
        
        // Error Recovery và Performance improvements
        private CheckpointManager _checkpointManager;
        private ElevenLabsHttpClientFactory _httpClientFactory;
        private ProcessingCheckpoint? _currentCheckpoint;
        private bool _enableCheckpointing = true;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<string>? LogRequested;

        public ICommand OpenFileCommand { get; }
        public ICommand DownloadMp3Command { get; }
        public ICommand DownloadErrorCommand { get; }
        public ICommand LoadApiKeysCommand { get; }
        public ICommand CopyUnavailableKeysCommand { get; }
        public ICommand ImportProxiesCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand OpenConfigFolderCommand { get; }
        public ICommand ReloadConfigCommand { get; }
        public ICommand RestartAppCommand { get; }
        public ICommand ViewDeadKeysCommand { get; }

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

        public bool UseProxy
        {
            get => _useProxy;
            set
            {
                if (_useProxy != value)
                {
                    _useProxy = value;
                    OnPropertyChanged(nameof(UseProxy));
                    Log($"[PROXY] Use Proxy: {(_useProxy ? "Enabled" : "Disabled")}");
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
            
            // Initialize error recovery và performance improvements
            _checkpointManager = new CheckpointManager();
            _httpClientFactory = new ElevenLabsHttpClientFactory();

            OpenFileCommand = new RelayCommand(OpenFile, () => !IsProcessing);
            DownloadMp3Command = new AsyncRelayCommand(DownloadMp3Async, () => !IsProcessing);
            DownloadErrorCommand = new AsyncRelayCommand(DownloadErrorAsync, () => !IsProcessing);
            LoadApiKeysCommand = new RelayCommand(LoadApiKeys, () => !IsProcessing);
            CopyUnavailableKeysCommand = new RelayCommand(CopyUnavailableKeys, () => !IsProcessing);
            ImportProxiesCommand = new AsyncRelayCommand(ImportProxiesAsync, () => !IsProcessing);
            StopCommand = new RelayCommand(Stop, () => IsProcessing);
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
            OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder);
            ReloadConfigCommand = new RelayCommand(ReloadConfig, () => !IsProcessing);
            RestartAppCommand = new RelayCommand(RestartApp, () => !IsProcessing);
            ViewDeadKeysCommand = new RelayCommand(ViewDeadKeys);
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

        // Helper method để đảm bảo UI updates chạy trên UI thread
        private void UpdateUIProperty(Action action)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                action();
            }
            else
            {
                Application.Current?.Dispatcher.InvokeAsync(action);
            }
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
            
            try
            {
                // Đọc file config với xử lý lỗi
                var configPath = GetConfigPath("ElevenlabConfig.yaml");
                if (!File.Exists(configPath))
                {
                    Log($"[ERROR] File config không tồn tại: {configPath}");
                    MessageBox.Show($"File config không tồn tại: {configPath}\nVui lòng kiểm tra thư mục Configs.", "Lỗi cấu hình", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _elevenLabConfig = YamlUtility.DeserializeAuto<ElevenlabConfig>(File.ReadAllText(configPath));
                
                // Đọc file key state với xử lý lỗi
                var keyStatePath = GetConfigPath("ElevenlabKeyState.yaml");
                if (!File.Exists(keyStatePath))
                {
                    Log($"[ERROR] File key state không tồn tại: {keyStatePath}");
                    MessageBox.Show($"File key state không tồn tại: {keyStatePath}\nVui lòng load API keys từ file.", "Lỗi cấu hình", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _elevenLabKeyState = YamlUtility.DeserializeAuto<ElevenlabKeyState>(File.ReadAllText(keyStatePath));
                
                // Kiểm tra có API key nào không
                if (_elevenLabKeyState.ApiKeys == null || _elevenLabKeyState.ApiKeys.Count == 0)
                {
                    Log("[WARNING] Không có API key nào trong file cấu hình");
                    MessageBox.Show("Không có API key nào trong file cấu hình.\nVui lòng sử dụng nút 'Load API Keys' để tải API keys.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _apiKeyManager = new ApiKeyManager(
                    _elevenLabKeyState.ApiKeys,
                    _elevenLabConfig.KeySelectionAlgorithm,
                    TimeSpan.FromMinutes(_elevenLabConfig.QuotaResetTimeMinutes),
                    GetConfigPath("ElevenlabKeyState.yaml")
                );
                
                // Log thông tin thành công
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
                Log("[SUCCESS] Đã load thành công cấu hình và API keys");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Lỗi khi load cấu hình: {ex.Message}");
                MessageBox.Show($"Lỗi khi load cấu hình: {ex.Message}\nVui lòng kiểm tra lại file cấu hình trong thư mục Configs.", "Lỗi cấu hình", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_elevenLabConfig != null && _elevenLabConfig.EnableProxyRotation)
            {
                Log("[PROXY] Proxy rotation được kích hoạt");
            }

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
        private void OpenOutputFolder()
        {
            try
            {
                var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "Files", "Eleven");
                
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                    Log($"[FOLDER] Đã tạo thư mục output: {outputFolder}");
                }
                
                // Open folder in Windows Explorer
                System.Diagnostics.Process.Start("explorer.exe", outputFolder);
                Log($"[INFO] Đã mở thư mục output: {outputFolder}");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Không thể mở thư mục output: {ex.Message}");
                MessageBox.Show($"Không thể mở thư mục output:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenConfigFolder()
        {
            try
            {
                var configFolder = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
                
                if (!Directory.Exists(configFolder))
                {
                    Directory.CreateDirectory(configFolder);
                    Log($"[FOLDER] Đã tạo thư mục config: {configFolder}");
                }
                
                // Open folder in Windows Explorer
                System.Diagnostics.Process.Start("explorer.exe", configFolder);
                Log($"[INFO] Đã mở thư mục config: {configFolder}");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Không thể mở thư mục config: {ex.Message}");
                MessageBox.Show($"Không thể mở thư mục config:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadConfig()
        {
            try
            {
                Log("[RELOAD] ===== BẮT ĐẦU RELOAD CẤU HÌNH =====");
                
                // Dispose ApiKeyManager cũ nếu có
                if (_apiKeyManager != null)
                {
                    try
                    {
                        (_apiKeyManager as IDisposable)?.Dispose();
                        Log("[RELOAD] Đã dispose ApiKeyManager cũ");
                    }
                    catch (Exception ex)
                    {
                        Log($"[RELOAD_WARNING] Lỗi khi dispose ApiKeyManager: {ex.Message}");
                    }
                }
                
                // Reload configuration
                InitDefaultValue();
                
                Log("[RELOAD] ===== HOÀN TẤT RELOAD CẤU HÌNH =====");
                MessageBox.Show("Đã reload cấu hình thành công!\nVui lòng xem log để kiểm tra chi tiết.",
                    "Reload Config", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[RELOAD_ERROR] Lỗi khi reload cấu hình: {ex.Message}");
                MessageBox.Show($"Lỗi khi reload cấu hình:\n{ex.Message}",
                    "Lỗi Reload", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewDeadKeys()
        {
            try
            {
                var configFolder = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
                var deadKeysFile = Path.Combine(configFolder, "DeadKeys.txt");
                
                if (!File.Exists(deadKeysFile))
                {
                    MessageBox.Show(
                        "Chưa có file DeadKeys.txt.\nFile này sẽ được tạo tự động khi có API key bị exhausted.",
                        "Thông tin",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Log("[DEAD_KEYS] File DeadKeys.txt chưa tồn tại");
                    return;
                }
                
                // Đọc nội dung file
                var content = File.ReadAllText(deadKeysFile);
                var lines = File.ReadAllLines(deadKeysFile);
                
                Log($"[DEAD_KEYS] Đang mở file DeadKeys.txt ({lines.Length} dòng)");
                
                // Hiển thị thông tin tổng quan
                var summary = $"File DeadKeys.txt có {lines.Length} mục\n" +
                             $"Đường dẫn: {deadKeysFile}\n\n" +
                             $"Bạn muốn xem nội dung file không?";
                
                var result = MessageBox.Show(
                    summary,
                    "Dead Keys Information",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Mở file bằng Notepad
                    System.Diagnostics.Process.Start("notepad.exe", deadKeysFile);
                    Log($"[DEAD_KEYS] Đã mở file DeadKeys.txt bằng Notepad");
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Không thể xem file DeadKeys.txt: {ex.Message}");
                MessageBox.Show(
                    $"Không thể xem file DeadKeys.txt:\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RestartApp()
        {
            try
            {
                var result = MessageBox.Show(
                    "Bạn có chắc muốn khởi động lại ứng dụng?\nTất cả tiến trình đang chạy sẽ bị dừng.",
                    "Xác nhận Restart",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Log("[RESTART] Đang khởi động lại ứng dụng...");
                    
                    // Lấy đường dẫn executable hiện tại
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        // Start new instance
                        System.Diagnostics.Process.Start(exePath);
                        
                        // Close current instance
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        Log("[RESTART_ERROR] Không thể lấy đường dẫn ứng dụng");
                        MessageBox.Show("Không thể khởi động lại ứng dụng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[RESTART_ERROR] Lỗi khi khởi động lại: {ex.Message}");
                MessageBox.Show($"Lỗi khi khởi động lại ứng dụng:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Log("[CANCEL] Đã yêu cầu dừng tiến trình hiện tại");
                UpdateUIProperty(() => StatusText = "Đang dừng...");
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
            
            // Kiểm tra API Key Manager đã được khởi tạo chưa
            if (_apiKeyManager == null)
            {
                Log("[ERROR] API Key Manager chưa được khởi tạo. Vui lòng load API keys trước.");
                MessageBox.Show(
                    "❌ Chưa load API keys!\n\n" +
                    "Vui lòng thực hiện một trong các bước sau:\n" +
                    "1. Sử dụng menu 'Tools' → 'Load API Keys' để load từ file text\n" +
                    "2. Hoặc sử dụng 'Tools' → 'Reload Config' để load từ file cấu hình\n\n" +
                    "Lưu ý: File cấu hình phải có ít nhất 1 API key trong ElevenlabKeyState.yaml",
                    "⚠️ Thiếu API Keys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            // Kiểm tra có API key khả dụng không
            var availableKeysCount = _elevenLabKeyState?.ApiKeys?.Count ?? 0;
            if (availableKeysCount == 0)
            {
                Log("[ERROR] Không có API key nào trong hệ thống.");
                MessageBox.Show(
                    "❌ Không có API key nào!\n\n" +
                    "Vui lòng load API keys bằng menu 'Tools' → 'Load API Keys'",
                    "⚠️ Thiếu API Keys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                
                // Check for existing checkpoint if checkpointing is enabled
                List<SubtitleTaskItem> allItems;
                if (_enableCheckpointing)
                {
                    _currentCheckpoint = _checkpointManager.GetLatestCheckpoint(folderPath);
                    
                    if (_currentCheckpoint != null)
                    {
                        var result = MessageBox.Show(
                            $"Phát hiện checkpoint chưa hoàn thành từ {_currentCheckpoint.CreatedAt:yyyy-MM-dd HH:mm:ss}.\n" +
                            $"Đã xử lý: {_currentCheckpoint.ProcessedCount}/{_currentCheckpoint.TotalCount}\n" +
                            $"Lỗi: {_currentCheckpoint.ErrorCount}\n\n" +
                            "Bạn có muốn tiếp tục từ checkpoint không?",
                            "Checkpoint Found",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Cancel)
                        {
                            return;
                        }
                        else if (result == MessageBoxResult.Yes)
                        {
                            // Resume from checkpoint
                            allItems = await ResumeFromCheckpoint(folderPath, srtFiles);
                            Log($"[CHECKPOINT] Resuming from checkpoint: {allItems.Count} items remaining to process");
                        }
                        else
                        {
                            // Start fresh, delete existing checkpoint
                            _checkpointManager.DeleteCheckpoint(_currentCheckpoint.CheckpointId);
                            _currentCheckpoint = null;
                            allItems = await ParseAllSrtFiles(srtFiles);
                        }
                    }
                    else
                    {
                        allItems = await ParseAllSrtFiles(srtFiles);
                    }
                }
                else
                {
                    allItems = await ParseAllSrtFiles(srtFiles);
                }
                
                if (!allItems.Any())
                {
                    MessageBox.Show("Không có item nào để xử lý.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create new checkpoint if needed
                if (_enableCheckpointing && _currentCheckpoint == null)
                {
                    _currentCheckpoint = _checkpointManager.CreateCheckpoint(folderPath, allItems.Count);
                    Log($"[CHECKPOINT] Created new checkpoint: {_currentCheckpoint.CheckpointId}");
                }

                TotalCount = allItems.Count;
                Log($"[SUBTITLES] Tổng cộng {allItems.Count} subtitle items cần xử lý");
                Log($"[DOWNLOAD] Chuẩn bị tải: {allItems.Count} file MP3 với {_elevenLabConfig.MaxThreads} threads đồng thời");
                Log($"[PERFORMANCE] HttpClient Factory stats: {_httpClientFactory.GetPoolStatistics()}");
                
                _trackError.Clear();
                await StartT2S(allItems, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("[CANCELLED] Người dùng đã dừng tiến trình");
                if (_enableCheckpointing && _currentCheckpoint != null)
                {
                    Log($"[CHECKPOINT] Progress saved. Resume later with checkpoint ID: {_currentCheckpoint.CheckpointId}");
                }
            }
            finally
            {
                IsProcessing = false;
                StatusText = $"Kết thúc: {ProcessedCount}/{TotalCount} - Lỗi còn lại: {ErrorCount}";
                
                // Complete checkpoint if all items processed successfully
                if (_enableCheckpointing && _currentCheckpoint != null && ProcessedCount == TotalCount)
                {
                    _checkpointManager.CompleteCheckpoint(_currentCheckpoint);
                    Log($"[CHECKPOINT] Checkpoint completed: {_currentCheckpoint.CheckpointId}");
                }
                
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// Parse tất cả SRT files thành danh sách SubtitleTaskItem với memory optimization
        /// </summary>
        private async Task<List<SubtitleTaskItem>> ParseAllSrtFiles(List<string> srtFiles)
        {
            return await Task.Run(() =>
            {
                var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
                var allItems = new List<SubtitleTaskItem>();
                var totalFiles = srtFiles.Count;
                var processedFiles = 0;
                
                foreach (var srt in srtFiles)
                {
                    try
                    {
                        using var fileStream = File.OpenRead(srt);
                        var items = parser.ParseStream(fileStream, Encoding.UTF8);
                        var name = Path.GetFileNameWithoutExtension(srt);
                        
                        // Add items with memory-efficient approach
                        foreach (var item in items)
                        {
                            allItems.Add(new SubtitleTaskItem
                            {
                                SourcePath = srt,
                                SourceName = name,
                                Item = item
                            });
                        }
                        
                        processedFiles++;
                        Log($"[PARSING] Đã parse {processedFiles}/{totalFiles} files: {name} ({items.Count} items)");
                        
                        // Force garbage collection periodically for large file sets
                        if (processedFiles % 10 == 0)
                        {
                            GC.Collect();
                            Log($"[MEMORY] Forced GC after processing {processedFiles} files");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Lỗi khi parse file {srt}: {ex.Message}");
                    }
                }
                
                Log($"[PARSING] Hoàn thành parse: {allItems.Count} items từ {totalFiles} files");
                return allItems;
            });
        }

        /// <summary>
        /// Khôi phục danh sách items cần xử lý từ checkpoint
        /// </summary>
        private async Task<List<SubtitleTaskItem>> ResumeFromCheckpoint(string folderPath, List<string> srtFiles)
        {
            if (_currentCheckpoint == null)
                return new List<SubtitleTaskItem>();

            return await Task.Run(() =>
            {
                var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
                var allItems = new List<SubtitleTaskItem>();
                
                // Parse tất cả files
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

                // Lọc ra các items chưa được xử lý thành công
                var processedItemKeys = _currentCheckpoint.ProcessedItems
                    .Select(p => $"{p.SourceName}:{p.ItemIndex}")
                    .ToHashSet();

                var remainingItems = allItems
                    .Where(item => !processedItemKeys.Contains($"{item.SourceName}:{item.Item.Index}"))
                    .ToList();

                // Thêm lại các items bị lỗi để retry
                var errorItemKeys = _currentCheckpoint.ErrorItems
                    .Select(e => $"{e.SourceName}:{e.ItemIndex}")
                    .ToHashSet();

                var errorItems = allItems
                    .Where(item => errorItemKeys.Contains($"{item.SourceName}:{item.Item.Index}"))
                    .ToList();

                remainingItems.AddRange(errorItems);

                Log($"[CHECKPOINT] Resuming with {remainingItems.Count} items (success: {_currentCheckpoint.ProcessedItems.Count}, errors: {_currentCheckpoint.ErrorItems.Count})");
                
                return remainingItems;
            });
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
                            UpdateUIProperty(() =>
                            {
                                ProcessedCount = newProcessed;
                                ErrorCount = _trackError.Count;
                                StatusText = $"Đã xử lý {ProcessedCount}/{TotalCount}";
                            });
                            return;
                        }
    
                        Log($"[KEY_SELECTED] Key {apiKeyInfo.Key} (Used: {apiKeyInfo.UsedCount}, Priority: {apiKeyInfo.Priority}) -> {f.SourceName}/{f.Item.Index}.mp3");

                        HttpClient? clientLocal = null;
                        SRT2Speech.ProxyService.Models.ProxyInfo? poolProxy = null;
                        SRT2Speech.ProxyService.Models.ProxyInfo? chosen = null;
                        bool useHttpClientFactory = true; // Luôn dùng HttpClientFactory cho tối ưu

                        try
                        {
                            if (!UseProxy)
                            {
                                // Checkbox "Không dùng proxy" được chọn - dùng HttpClientFactory không proxy
                                clientLocal = _httpClientFactory.GetClientWithoutProxy(apiKeyInfo.Key);
                                Log($"[NO_PROXY] {f.SourceName}/{f.Item.Index}.mp3 - Sử dụng direct connection");
                            }
                            else if (string.IsNullOrWhiteSpace(apiKeyInfo.BoundProxyEndpoint))
                            {
                                // Use proxy được chọn nhưng không có BoundProxyEndpoint
                                Log($"[BINDING_MISSING] Key {apiKeyInfo.Key} không có BoundProxyEndpoint - bỏ qua {f.SourceName}/{f.Item.Index}.mp3");
                                var newProcessed = Interlocked.Increment(ref _processedCount);
                                UpdateUIProperty(() =>
                                {
                                    ProcessedCount = newProcessed;
                                    ErrorCount = _trackError.Count;
                                    StatusText = $"Đã xử lý {ProcessedCount}/{TotalCount}";
                                });
                                return;
                            }
                            else
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

                                chosen = poolProxy ?? parsedProxy!;

                                // Sử dụng HttpClientFactory để tối ưu performance
                                clientLocal = _httpClientFactory.GetClient(apiKeyInfo.Key, chosen);
                                var authInfo = string.IsNullOrEmpty(chosen.Username) ? "" : " (auth)";
                                Log($"[PROXY] {f.SourceName}/{f.Item.Index}.mp3 -> {chosen.Host}:{chosen.Port}{authInfo}");
                            }
    
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            var response = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(
                                async () => await clientLocal.PostAsync(url, GetContent(f.Item.Line), ct),
                                (res) =>
                                {

                                    return res.IsSuccessStatusCode;
                                },
                                maxRetries: 3,
                                baseDelayMs: 1000,
                                jitterFactor: 0.5,
                                logCallback: (msg) => Log(msg)
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
                                Log($"[SUCCESS] {f.SourceName}/{f.Item.Index}.mp3 ({sw.ElapsedMilliseconds}ms)");

                                // Add to checkpoint if enabled
                                if (_enableCheckpointing && _currentCheckpoint != null)
                                {
                                    var processedItem = new ProcessedItem
                                    {
                                        SourcePath = f.SourcePath,
                                        SourceName = f.SourceName,
                                        ItemIndex = f.Item.Index,
                                        OutputPath = outPath,
                                        ApiKeyUsed = apiKeyInfo.Key,
                                        ProxyUsed = chosen != null ? $"{chosen.Host}:{chosen.Port}" : "No Proxy"
                                    };
                                    _checkpointManager.AddProcessedItem(_currentCheckpoint, processedItem);
                                }

                                // Chỉ mark proxy success nếu thực sự sử dụng proxy
                                if (UseProxy && poolProxy != null)
                                {
                                    try { await _proxyManager!.MarkProxySuccessAsync(poolProxy.Id, sw.Elapsed); } catch { /* ignore */ }
                                }
                            }
                            else
                            {
                                string contentErr = await response.Content.ReadAsStringAsync(ct);
                                Log("[ERROR]: " + contentErr);

                                // Add to checkpoint if enabled
                                if (_enableCheckpointing && _currentCheckpoint != null)
                                {
                                    var errorItem = new ErrorItem
                                    {
                                        SourcePath = f.SourcePath,
                                        SourceName = f.SourceName,
                                        ItemIndex = f.Item.Index,
                                        ErrorMessage = $"{response.StatusCode}: {contentErr}",
                                        ApiKeyUsed = apiKeyInfo.Key,
                                        ProxyUsed = chosen != null ? $"{chosen.Host}:{chosen.Port}" : "No Proxy"
                                    };
                                    _checkpointManager.AddErrorItem(_currentCheckpoint, errorItem);
                                }

                                // Chỉ mark proxy failure nếu thực sự sử dụng proxy
                                if (UseProxy && poolProxy != null)
                                {
                                    try { await _proxyManager!.MarkProxyFailureAsync(poolProxy.Id, $"{response.StatusCode}"); } catch { /* ignore */ }
                                }

                                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                    contentErr.Contains("quota exceeded") ||
                                    contentErr.Contains("quota_exceeded") ||
                                    contentErr.Contains("exceeds") ||
                                    contentErr.Contains("rate limit") ||
                                    contentErr.Contains("invalid_api_key") ||
                                    contentErr.Contains("detected_unusual_activity"))
                                {
                                    _apiKeyManager.MarkKeyExhausted(apiKeyInfo.Key, response);
                                    Log($"[KEY_EXHAUSTED] Key {apiKeyInfo.Key} đã bị khóa do vượt quota hoặc lỗi API key (Status: {response.StatusCode})");
                                    Log($"[DEAD_KEY_SAVED] Key đã được lưu vào file DeadKeys.txt trong thư mục Configs");
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        finally
                        {
                            // Trả HttpClient về pool (luôn dùng HttpClientFactory)
                            if (useHttpClientFactory && clientLocal != null)
                            {
                                if (!UseProxy)
                                {
                                    _httpClientFactory.ReturnClientWithoutProxy(clientLocal, apiKeyInfo.Key);
                                }
                                else if (chosen != null)
                                {
                                    _httpClientFactory.ReturnClient(clientLocal, apiKeyInfo.Key, chosen);
                                }
                            }
                            else
                            {
                                // Fallback: dispose client
                                try { clientLocal?.Dispose(); } catch { /* ignore */ }
                            }
                            
                            var newProcessed = Interlocked.Increment(ref _processedCount);
                            UpdateUIProperty(() =>
                            {
                                ProcessedCount = newProcessed;
                                ErrorCount = _trackError.Count;
                                StatusText = $"Đã xử lý {ProcessedCount}/{TotalCount}";
                            });
                        }
                    });
    
                    await Task.WhenAll(tasks);

                    // Save checkpoint after each batch
                    if (_enableCheckpointing && _currentCheckpoint != null)
                    {
                        _currentCheckpoint.LastBatchProcessed = batchIndex.ToString();
                        _checkpointManager.UpdateCheckpoint(_currentCheckpoint);
                        Log($"[CHECKPOINT] Saved checkpoint after batch {batchIndex}/{chunks.Count()}");
                    }

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
            
            // Kiểm tra API Key Manager đã được khởi tạo chưa
            if (_apiKeyManager == null)
            {
                Log("[ERROR] API Key Manager chưa được khởi tạo. Vui lòng load API keys trước.");
                MessageBox.Show(
                    "❌ Chưa load API keys!\n\n" +
                    "Vui lòng thực hiện một trong các bước sau:\n" +
                    "1. Sử dụng menu 'Tools' → 'Load API Keys' để load từ file text\n" +
                    "2. Hoặc sử dụng 'Tools' → 'Reload Config' để load từ file cấu hình\n\n" +
                    "Lưu ý: File cấu hình phải có ít nhất 1 API key trong ElevenlabKeyState.yaml",
                    "⚠️ Thiếu API Keys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            // Kiểm tra có API key khả dụng không
            var availableKeysCount = _elevenLabKeyState?.ApiKeys?.Count ?? 0;
            if (availableKeysCount == 0)
            {
                Log("[ERROR] Không có API key nào trong hệ thống.");
                MessageBox.Show(
                    "❌ Không có API key nào!\n\n" +
                    "Vui lòng load API keys bằng menu 'Tools' → 'Load API Keys'",
                    "⚠️ Thiếu API Keys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
        private void CopyUnavailableKeys()
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            
            try
            {
                // Kiểm tra có API key state không
                if (_elevenLabKeyState == null || _elevenLabKeyState.ApiKeys == null || _elevenLabKeyState.ApiKeys.Count == 0)
                {
                    Log("[COPY_KEYS] Không có API key nào trong hệ thống");
                    MessageBox.Show(
                        "Không có API key nào trong hệ thống!\n\n" +
                        "Vui lòng load API keys trước khi sử dụng chức năng này.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // Lọc các key không còn available
                var unavailableKeys = _elevenLabKeyState.ApiKeys
                    .Where(k => !k.IsAvailable())
                    .ToList();
                
                if (unavailableKeys.Count == 0)
                {
                    Log("[COPY_KEYS] Tất cả API keys đều còn available");
                    MessageBox.Show(
                        "✅ Tuyệt vời!\n\n" +
                        "Tất cả API keys đều còn available.\n" +
                        "Không có key nào cần copy.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // Tạo danh sách key để copy (mỗi key một dòng)
                var keysList = string.Join(Environment.NewLine, unavailableKeys.Select(k => k.Key));
                
                // Copy vào clipboard
                Clipboard.SetText(keysList);
                
                // Tạo summary report
                var totalKeys = _elevenLabKeyState.ApiKeys.Count;
                var availableKeys = _elevenLabKeyState.ApiKeys.Count(k => k.IsAvailable());
                var unavailableCount = unavailableKeys.Count;
                
                var reportLines = new System.Text.StringBuilder();
                reportLines.AppendLine("=== UNAVAILABLE KEYS REPORT ===");
                reportLines.AppendLine($"Total keys: {totalKeys}");
                reportLines.AppendLine($"Available: {availableKeys}");
                reportLines.AppendLine($"Unavailable: {unavailableCount}");
                reportLines.AppendLine();
                reportLines.AppendLine("Unavailable keys details:");
                
                foreach (var key in unavailableKeys)
                {
                    var keyPrefix = key.Key.Length > 12 ? key.Key.Substring(0, 12) + "..." : key.Key;
                    var reason = key.Available ? "In cooldown" : "Exhausted";
                    var cooldownInfo = key.CooldownUntil.HasValue 
                        ? $"until {key.CooldownUntil.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                        : "N/A";
                    
                    reportLines.AppendLine($"  • {keyPrefix} - {reason} ({cooldownInfo}) - Used: {key.UsedCount}x");
                }
                
                Log($"[COPY_KEYS] Đã copy {unavailableCount} unavailable keys vào clipboard");
                Log(reportLines.ToString());
                
                MessageBox.Show(
                    $"✅ Đã copy {unavailableCount} unavailable API keys vào clipboard!\n\n" +
                    $"📊 Thống kê:\n" +
                    $"   • Tổng số keys: {totalKeys}\n" +
                    $"   • Available: {availableKeys}\n" +
                    $"   • Unavailable: {unavailableCount}\n\n" +
                    $"Các keys đã được copy theo định dạng:\n" +
                    $"- Mỗi key một dòng\n" +
                    $"- Có thể paste trực tiếp vào file text\n\n" +
                    $"Xem log để biết chi tiết về từng key.",
                    "Copy Thành Công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"[COPY_KEYS_ERROR] Lỗi khi copy unavailable keys: {ex.Message}");
                MessageBox.Show(
                    $"Lỗi khi copy unavailable keys:\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private List<string> LoadApiKeysFromFile(string filePath)
        {
            try
            {
                Log($"[KEY_LOAD] Đọc file: {Path.GetFileName(filePath)}");
                
                // Validate file exists
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File không tồn tại: {filePath}");
                }
                
                // Validate file is text format
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (extension != ".txt" && extension != ".text")
                {
                    Log($"[KEY_LOAD_WARNING] File có extension '{extension}' không phải .txt, nhưng vẫn tiếp tục đọc");
                }
                
                var lines = File.ReadAllLines(filePath);
                
                // Validate file is not empty
                if (lines.Length == 0)
                {
                    throw new Exception("File trống, không có dữ liệu!");
                }
                
                var apiKeys = new List<string>();
                var lineNumber = 0;
                var skippedLines = 0;
                
                foreach (var line in lines)
                {
                    lineNumber++;
                    var trimmedLine = line.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmedLine))
                    {
                        continue;
                    }
                    
                    if (trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//"))
                    {
                        Log($"[KEY_LOAD] Dòng {lineNumber}: Bỏ qua comment");
                        skippedLines++;
                        continue;
                    }
                    
                    // Validate API key format (basic validation)
                    // ElevenLabs API keys typically have a specific format (alphanumeric, specific length)
                    if (trimmedLine.Length < 20)
                    {
                        Log($"[KEY_LOAD_WARNING] Dòng {lineNumber}: API key quá ngắn (< 20 ký tự), có thể không hợp lệ: '{trimmedLine.Substring(0, Math.Min(10, trimmedLine.Length))}...'");
                        skippedLines++;
                        continue;
                    }
                    
                    // Check for invalid characters (spaces, special chars that shouldn't be in API keys)
                    if (trimmedLine.Contains(" ") || trimmedLine.Contains("\t"))
                    {
                        Log($"[KEY_LOAD_WARNING] Dòng {lineNumber}: API key chứa khoảng trắng, bỏ qua");
                        skippedLines++;
                        continue;
                    }
                    
                    // Check for duplicates
                    if (apiKeys.Contains(trimmedLine))
                    {
                        Log($"[KEY_LOAD_WARNING] Dòng {lineNumber}: API key trùng lặp, bỏ qua");
                        skippedLines++;
                        continue;
                    }
                    
                    apiKeys.Add(trimmedLine);
                    Log($"[KEY_LOAD] Dòng {lineNumber}: Đã thêm API key hợp lệ (prefix: {trimmedLine.Substring(0, Math.Min(8, trimmedLine.Length))}...)");
                }
                
                Log($"[KEY_LOAD] === KẾT QUẢ ===");
                Log($"  Tổng số dòng: {lines.Length}");
                Log($"  Dòng bỏ qua: {skippedLines}");
                Log($"  API keys hợp lệ: {apiKeys.Count}");
                
                // Final validation
                if (apiKeys.Count == 0)
                {
                    throw new Exception($"Không tìm thấy API key hợp lệ nào trong file!\n\n" +
                                      $"File phải có định dạng:\n" +
                                      $"- Mỗi API key trên một dòng\n" +
                                      $"- API key phải dài hơn 20 ký tự\n" +
                                      $"- Không chứa khoảng trắng\n" +
                                      $"- Có thể dùng # hoặc // để comment\n\n" +
                                      $"Ví dụ:\n" +
                                      $"sk_abc123def456...\n" +
                                      $"sk_xyz789uvw012...\n" +
                                      $"# This is a comment");
                }
                
                return apiKeys;
            }
            catch (Exception ex)
            {
                Log($"[KEY_LOAD_ERROR] Lỗi khi đọc file: {ex.Message}");
                throw;
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
            _checkpointManager?.Dispose();
            _httpClientFactory?.Dispose();
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