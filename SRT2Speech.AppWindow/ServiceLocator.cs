using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SRT2Speech.ProxyService.Extensions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.AppWindow
{
    public static class ServiceLocator
    {
        // Thuộc tính tĩnh để giữ ServiceProvider
        public static IServiceProvider ServiceProvider { get; private set; }

        public static void Initialize(IConfiguration configuration)
        {
            var services = new ServiceCollection();

            // Đăng ký các dịch vụ của bạn ở đây
            ConfigureServices(services, configuration);

            // Xây dựng ServiceProvider
            ServiceProvider = services.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Đăng ký logging services
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Đăng ký ProxyService với tất cả dependencies
            services.AddProxyService(configuration);
        }

        // Phương thức helper để lấy dịch vụ một cách dễ dàng
        public static T GetService<T>() where T : class
        {
            return ServiceProvider.GetService<T>();
        }
    }
}


