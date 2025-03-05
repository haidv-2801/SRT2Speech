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
                CreateFolders();
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private bool CreateFolders()
        {
            try
            {
                var curDirect = Directory.GetCurrentDirectory();
                var fpt = Path.Combine(curDirect, "Files/FPT");
                var vbee = Path.Combine(curDirect, "Files/Vbee");
                var english = Path.Combine(curDirect, "Files/English");
                var eleven = Path.Combine(curDirect, "Files/Eleven");
                var aiStudio = Path.Combine(curDirect, "Files/AiStudio");
                if (!Directory.Exists(fpt))
                {
                    Directory.CreateDirectory(fpt);
                }
                if (!Directory.Exists(vbee))
                {
                    Directory.CreateDirectory(vbee);
                }
                if (!Directory.Exists(english))
                {
                    Directory.CreateDirectory(english);
                }
                if (!Directory.Exists(eleven))
                {
                    Directory.CreateDirectory(eleven);
                }
                if (!Directory.Exists(aiStudio))
                {
                    Directory.CreateDirectory(aiStudio);
                }
            }
            catch (Exception ex)
            {
                
            }

            return true;
        }
    }
}
