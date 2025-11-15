using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.Core.Utilitys;

namespace SRT2Speech.AppWindow.Services
{
    /// <summary>
    /// Service quản lý checkpoint để lưu và khôi phục trạng thái xử lý
    /// </summary>
    public class CheckpointManager : IDisposable
    {
        private readonly string _checkpointDirectory;
        private readonly object _lock = new object();
        private ProcessingCheckpoint? _currentCheckpoint;

        public CheckpointManager(string? checkpointDirectory = null)
        {
            _checkpointDirectory = checkpointDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "Files", "Checkpoints");
            EnsureDirectoryExists();
        }

        /// <summary>
        /// Tạo checkpoint mới cho phiên xử lý
        /// </summary>
        public ProcessingCheckpoint CreateCheckpoint(string sourceFolder, int totalCount)
        {
            lock (_lock)
            {
                _currentCheckpoint = new ProcessingCheckpoint
                {
                    SourceFolder = sourceFolder,
                    TotalCount = totalCount,
                    ProcessedCount = 0,
                    ErrorCount = 0
                };

                SaveCheckpoint(_currentCheckpoint);
                return _currentCheckpoint;
            }
        }

        /// <summary>
        /// Lưu trạng thái hiện tại vào checkpoint
        /// </summary>
        public void UpdateCheckpoint(ProcessingCheckpoint checkpoint)
        {
            lock (_lock)
            {
                checkpoint.ProcessedCount = checkpoint.ProcessedItems.Count;
                checkpoint.ErrorCount = checkpoint.ErrorItems.Count;
                _currentCheckpoint = checkpoint;
                SaveCheckpoint(checkpoint);
            }
        }

        /// <summary>
        /// Thêm item đã xử lý thành công vào checkpoint
        /// </summary>
        public void AddProcessedItem(ProcessingCheckpoint checkpoint, ProcessedItem item)
        {
            lock (_lock)
            {
                checkpoint.ProcessedItems.Add(item);
                UpdateCheckpoint(checkpoint);
            }
        }

        /// <summary>
        /// Thêm item bị lỗi vào checkpoint
        /// </summary>
        public void AddErrorItem(ProcessingCheckpoint checkpoint, ErrorItem item)
        {
            lock (_lock)
            {
                checkpoint.ErrorItems.Add(item);
                UpdateCheckpoint(checkpoint);
            }
        }

        /// <summary>
        /// Đánh dấu checkpoint đã hoàn thành
        /// </summary>
        public void CompleteCheckpoint(ProcessingCheckpoint checkpoint)
        {
            lock (_lock)
            {
                checkpoint.IsCompleted = true;
                checkpoint.CompletedAt = DateTime.UtcNow;
                UpdateCheckpoint(checkpoint);
            }
        }

        /// <summary>
        /// Lấy checkpoint gần nhất cho thư mục nguồn
        /// </summary>
        public ProcessingCheckpoint? GetLatestCheckpoint(string sourceFolder)
        {
            lock (_lock)
            {
                var checkpointFiles = Directory.GetFiles(_checkpointDirectory, "*.yaml")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                foreach (var file in checkpointFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file.FullName);
                        var checkpoint = YamlUtility.DeserializeAuto<ProcessingCheckpoint>(content);
                        
                        if (checkpoint.SourceFolder.Equals(sourceFolder, StringComparison.OrdinalIgnoreCase) && 
                            !checkpoint.IsCompleted)
                        {
                            _currentCheckpoint = checkpoint;
                            return checkpoint;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CHECKPOINT_ERROR] Error loading checkpoint {file.Name}: {ex.Message}");
                        // Xóa file checkpoint bị lỗi
                        try
                        {
                            File.Delete(file.FullName);
                        }
                        catch { /* ignore */ }
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Lấy danh sách các checkpoint chưa hoàn thành
        /// </summary>
        public List<ProcessingCheckpoint> GetIncompleteCheckpoints()
        {
            lock (_lock)
            {
                var checkpoints = new List<ProcessingCheckpoint>();
                var checkpointFiles = Directory.GetFiles(_checkpointDirectory, "*.yaml");

                foreach (var file in checkpointFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        var checkpoint = YamlUtility.DeserializeAuto<ProcessingCheckpoint>(content);
                        
                        if (!checkpoint.IsCompleted)
                        {
                            checkpoints.Add(checkpoint);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CHECKPOINT_ERROR] Error loading checkpoint {file}: {ex.Message}");
                        // Xóa file checkpoint bị lỗi
                        try
                        {
                            File.Delete(file);
                        }
                        catch { /* ignore */ }
                    }
                }

                return checkpoints.OrderByDescending(c => c.CreatedAt).ToList();
            }
        }

        /// <summary>
        /// Xóa checkpoint
        /// </summary>
        public void DeleteCheckpoint(string checkpointId)
        {
            lock (_lock)
            {
                var filePath = Path.Combine(_checkpointDirectory, $"{checkpointId}.yaml");
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CHECKPOINT_ERROR] Error deleting checkpoint {checkpointId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Dọn dẹp các checkpoint cũ (quá 7 ngày)
        /// </summary>
        public void CleanupOldCheckpoints()
        {
            lock (_lock)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-7);
                var checkpointFiles = Directory.GetFiles(_checkpointDirectory, "*.yaml");

                foreach (var file in checkpointFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CHECKPOINT_ERROR] Error cleaning up old checkpoint {file}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Lấy checkpoint hiện tại
        /// </summary>
        public ProcessingCheckpoint? GetCurrentCheckpoint()
        {
            lock (_lock)
            {
                return _currentCheckpoint;
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_checkpointDirectory))
            {
                Directory.CreateDirectory(_checkpointDirectory);
            }
        }

        private void SaveCheckpoint(ProcessingCheckpoint checkpoint)
        {
            try
            {
                var filePath = Path.Combine(_checkpointDirectory, $"{checkpoint.CheckpointId}.yaml");
                var yamlContent = YamlUtility.SerializeToHyphenated(checkpoint);
                File.WriteAllText(filePath, yamlContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHECKPOINT_ERROR] Error saving checkpoint: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Cleanup old checkpoints on dispose
            try
            {
                CleanupOldCheckpoints();
            }
            catch { /* ignore */ }
        }
    }
}