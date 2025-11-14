using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.ProxyService.Interfaces;
using SRT2Speech.ProxyService.Models;
using System.IO;
using System.Threading;
using System.Windows;

namespace SRT2Speech.AppWindow
{
    public partial class App : Application
    {
        // DI root cho toàn app
        public static System.IServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                // Build configuration với appsettings.json
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                // Khởi tạo ServiceLocator với configuration
                ServiceLocator.Initialize(configuration);

                // Load proxy configuration sau khi ServiceProvider đã được build
                LoadProxyConfiguration();

                // Khởi động các HostedService (ProxyHealthChecker)
                var hostedServices = ServiceLocator.ServiceProvider.GetServices<IHostedService>();
                foreach (var hosted in hostedServices)
                {
                    hosted.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("StartupError: " + ex.Message);
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Load proxy configuration từ file
        /// </summary>
        private static void LoadProxyConfiguration()
        {
            // Ưu tiên đọc tệp cấu hình tại SRT2Speech.AppWindow/Configs/proxies.yaml (tệp nguồn),
            // nếu không tồn tại thì fallback sang tệp runtime tại BaseDirectory/Configs/proxies.yaml
            var runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "proxies.yaml");
            var workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "SRT2Speech.AppWindow", "Configs", "proxies.yaml");
            var configPath = File.Exists(workspacePath) ? workspacePath : runtimePath;

            if (File.Exists(configPath))
            {
                try
                {
                    var yaml = File.ReadAllText(configPath);
                    var config = YamlUtility.DeserializeAuto<ProxyConfiguration>(yaml);

                    // Lấy ProxyPool từ ServiceProvider đã được build
                    var proxyPool = ServiceLocator.GetService<IProxyPool>();

                    if (proxyPool != null)
                    {
                        foreach (var proxy in config.Proxies)
                        {
                            proxyPool.AddProxy(proxy);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: IProxyPool service not registered");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - allow app to start without proxies
                    Console.WriteLine($"Warning: Could not load proxy configuration from {configPath}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Warning: proxies.yaml not found at {configPath}");
            }
        }
    }
}
