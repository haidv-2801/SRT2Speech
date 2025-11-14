using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
            // Log section now automatically fills available space via DockPanel
            // No need to manually calculate height
            
            // Optional: Set minimum height if needed (already set in XAML)
            var rtb = this.FindName("txtLog") as RichTextBox;
            if (rtb != null)
            {
                // Ensure minimum height is respected
                rtb.MinHeight = 200;
            }
        }

        private bool WriteLog(string message)
        {
            var rtb = this.FindName("txtLog") as RichTextBox;
            if (rtb == null) return false;
            
            this.Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logColor = GetLogColor(message);
                var logLevel = ExtractLogLevel(message);
                
                // Create paragraph for this log entry
                var paragraph = new Paragraph();
                
                // Add timestamp in gray
                var timestampRun = new Run($"[{timestamp}] ")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                };
                paragraph.Inlines.Add(timestampRun);
                
                // Add log level with color if exists
                if (!string.IsNullOrEmpty(logLevel))
                {
                    var levelRun = new Run($"{logLevel} ")
                    {
                        Foreground = new SolidColorBrush(logColor),
                        FontWeight = FontWeights.Bold
                    };
                    paragraph.Inlines.Add(levelRun);
                    
                    // Add message content (remove log level prefix)
                    var messageContent = message.Replace(logLevel, "").TrimStart();
                    var messageRun = new Run(messageContent)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212))
                    };
                    paragraph.Inlines.Add(messageRun);
                }
                else
                {
                    // No log level, use default color
                    var messageRun = new Run(message)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212))
                    };
                    paragraph.Inlines.Add(messageRun);
                }
                
                rtb.Document.Blocks.Add(paragraph);
                rtb.ScrollToEnd();
            });
            
            return true;
        }
        
        private string ExtractLogLevel(string message)
        {
            // Extract log level prefix like [INFO], [ERROR], [WARNING], [DEBUG], [SUCCESS]
            var match = Regex.Match(message, @"^\[(INFO|ERROR|WARNING|WARN|DEBUG|SUCCESS|TRACE)\]", RegexOptions.IgnoreCase);
            return match.Success ? match.Value : string.Empty;
        }
        
        private Color GetLogColor(string message)
        {
            var upperMessage = message.ToUpper();
            
            // Error/Exception - Red
            if (upperMessage.Contains("[ERROR]") || upperMessage.Contains("[EXCEPTION]"))
                return Color.FromRgb(220, 53, 69); // #DC3545
            
            // Warning - Yellow/Orange
            if (upperMessage.Contains("[WARNING]") || upperMessage.Contains("[WARN]"))
                return Color.FromRgb(255, 193, 7); // #FFC107
            
            // Success - Green
            if (upperMessage.Contains("[SUCCESS]"))
                return Color.FromRgb(40, 167, 69); // #28A745
            
            // Info - Blue
            if (upperMessage.Contains("[INFO]"))
                return Color.FromRgb(0, 122, 204); // #007ACC
            
            // Debug - Purple
            if (upperMessage.Contains("[DEBUG]"))
                return Color.FromRgb(108, 117, 125); // #6C757D
            
            // Trace - Gray
            if (upperMessage.Contains("[TRACE]"))
                return Color.FromRgb(150, 150, 150); // #969696
            
            // Default - Light gray
            return Color.FromRgb(212, 212, 212); // #D4D4D4
        }
    }
}
