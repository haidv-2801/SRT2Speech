using System;
using System.Collections.Generic;

namespace SRT2Speech.AppWindow.Models
{
    /// <summary>
    /// Model lưu trạng thái checkpoint để phục hồi xử lý sau khi bị gián đoạn
    /// </summary>
    public class ProcessingCheckpoint
    {
        public string CheckpointId { get; set; } = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string SourceFolder { get; set; } = string.Empty;
        public List<string> ProcessedFiles { get; set; } = new List<string>();
        public List<ProcessedItem> ProcessedItems { get; set; } = new List<ProcessedItem>();
        public List<ErrorItem> ErrorItems { get; set; } = new List<ErrorItem>();
        public int TotalCount { get; set; }
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
        public string LastBatchProcessed { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
    }

    public class ProcessedItem
    {
        public string SourcePath { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public int ItemIndex { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public string ApiKeyUsed { get; set; } = string.Empty;
        public string ProxyUsed { get; set; } = string.Empty;
    }

    public class ErrorItem
    {
        public string SourcePath { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public int ItemIndex { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime ErrorAt { get; set; } = DateTime.UtcNow;
        public string ApiKeyUsed { get; set; } = string.Empty;
        public string ProxyUsed { get; set; } = string.Empty;
        public int RetryCount { get; set; } = 0;
    }
}