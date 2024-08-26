using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace SRT2Speech.AppWindow
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }
       

        protected override void OnStartup(StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            base.OnStartup(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleException((Exception)e.ExceptionObject);
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception);
            e.Handled = true;
        }

        private void HandleException(Exception ex)
        {
            // Log the exception
            LogException(ex);

            // Display an error message to the user
            MessageBox.Show(ex.InnerException.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void LogException(Exception ex)
        {
            // Implement your own logging mechanism here
            Console.WriteLine($"An exception occurred: {ex.Message}\n{ex.StackTrace}");
        }

        private void ConfigureServices(IServiceCollection services)
        {
        }
    }
}
