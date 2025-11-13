using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

                // Build configuration (tối thiểu) để đăng ký ProxyService
                // Có thể mở rộng đọc appsettings.json khi bổ sung gói Microsoft.Extensions.Configuration.Json/FileExtensions
                var configuration = new ConfigurationBuilder()
                    .Build();

                // Đăng ký Logging và ProxyService vào DI
                var services = new ServiceCollection();
                services.AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                });
                SRT2Speech.ProxyService.Extensions.ServiceCollectionExtensions.AddProxyService(services, configuration);

                ServiceProvider = services.BuildServiceProvider();

                // Khởi động các HostedService (ProxyHealthChecker)
                var hostedServices = ServiceProvider.GetServices<IHostedService>();
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
    }
}
