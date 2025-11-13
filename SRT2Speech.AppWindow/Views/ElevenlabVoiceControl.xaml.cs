using System;
using System.Windows;
using System.Windows.Controls;
using SRT2Speech.AppWindow.ViewModels;
using SRT2Speech.ProxyService.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace SRT2Speech.AppWindow.Views
{
    public partial class ElevenlabVoiceControl : UserControl
    {
        public ElevenlabVoiceControl()
        {
        }

        public ElevenlabVoiceControl(IProxyManager proxyManager)
        {
            System.Windows.Application.LoadComponent(this, new Uri("/SRT2Speech.AppWindow;component/Views/ElevenlabVoiceControl.xaml", UriKind.Relative));

            var vm = new ElevenlabVoiceControlViewModel(proxyManager);
            vm.LogRequested += (_, msg) => WriteLog(msg);
            this.DataContext = vm;
            vm.Initialize();

            this.Loaded += (_, __) => InitContent();
            this.Unloaded += (_, __) =>
            {
                try
                {
                    (this.DataContext as IDisposable)?.Dispose();
                }
                catch
                {
                    // ignore
                }
            };
        }

        private void InitContent()
        {
            FullWidthLog();
        }

        private void FullWidthLog()
        {
            var rtb = this.FindName("txtLog") as RichTextBox;
            if (rtb != null)
            {
                rtb.Height = SystemParameters.PrimaryScreenHeight - 480;
            }
        }

        private bool WriteLog(string message)
        {
            var rtb = this.FindName("txtLog") as RichTextBox;
            if (rtb == null) return false;
            this.Dispatcher.Invoke(() =>
            {
                rtb.AppendText($"{DateTime.Now} - {message}\n");
                rtb.ScrollToEnd();
            });
            return true;
        }
    }
}
