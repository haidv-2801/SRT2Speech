using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace SRT2Speech.AppWindow
{
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                var license = new LicenseManager.LicensingService();
                var result = license.ValidateLicense();
                if(result.IsValid)
                {
                    CreateFolders();
                    base.OnStartup(e);
                }
                else
                {
                    MessageBox.Show("LicenseError: " + result.Message);
                    Application.Current.Shutdown(); // Thêm lệnh tắt ứng dụng
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("LicenseError: " + ex.Message);
                Application.Current.Shutdown(); // Thêm lệnh tắt ứng dụng
            }
        }
    }
}
