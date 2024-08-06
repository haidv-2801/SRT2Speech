namespace SRT2Speech.AppAPI.Services.DowloadService
{
    public interface IDowloadService
    {
        Task<bool> DownloadMp3Async(string mp3Url, string filePath);
    }
}
