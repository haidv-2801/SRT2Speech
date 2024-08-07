
namespace SRT2Speech.AppAPI.Services.DowloadService
{
    public class DowloadService : IDowloadService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DowloadService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> DownloadMp3Async(string mp3Url, string filePath)
        {
            string directoryPath = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using (var httpClient = _httpClientFactory.CreateClient())
            {
                try
                {
                    var response = await httpClient.GetAsync(mp3Url);
                    response.EnsureSuccessStatusCode();
                    var stream = await response.Content.ReadAsStreamAsync();
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                    
                    Console.WriteLine($"MP3 file downloaded successfully: {filePath}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading MP3 file: {ex.Message}");
                    return false;
                }
            }
        }
    }
}
