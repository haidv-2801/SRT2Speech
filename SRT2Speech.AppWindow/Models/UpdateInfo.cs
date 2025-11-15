using System;
using System.Collections.Generic;

namespace SRT2Speech.AppWindow.Models
{
    /// <summary>
    /// Thông tin về phiên bản cập nhật
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// Phiên bản mới nhất (format: Major.Minor.Patch)
        /// </summary>
        public string LatestVersion { get; set; } = string.Empty;

        /// <summary>
        /// Ngày phát hành
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// URL để tải về bản cập nhật
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Danh sách thay đổi (changelog)
        /// </summary>
        public List<string> Changelog { get; set; } = new();

        /// <summary>
        /// Phiên bản tối thiểu yêu cầu
        /// </summary>
        public string MinimumVersion { get; set; } = string.Empty;

        /// <summary>
        /// Có bắt buộc cập nhật không
        /// </summary>
        public bool ForceUpdate { get; set; }

        /// <summary>
        /// Có phiên bản mới không (được tính toán)
        /// </summary>
        public bool HasUpdate { get; set; }

        /// <summary>
        /// Kích thước file (bytes)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Ghi chú bổ sung
        /// </summary>
        public string ReleaseNotes { get; set; } = string.Empty;
    }
}