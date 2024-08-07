using SRT2Speech.Socket.Hubs;

namespace SRT2Speech.AppAPI.Extensions
{
    public static class SignalRExtensions
    {
        public static void ConfigureSignalR(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<MessageHub>("/messageHub");
            });
        }

        public static IServiceCollection AddSignalRService(this IServiceCollection services)
        {
            services.AddSignalR();
            return services;
        }
    }
}
