using AutoApp.Utility;
using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;
using System.Net.Http.Headers;

namespace SRT2Speech.AppWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string fileInputContent;

        public MainWindow()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }

        private void InitDefaultValue()
        {
            fileInputContent = string.Empty;
            txtLog.AppendText("Logging...");
        }

        private void InitContent()
        {
            FullWidthLog();
            CenterForm();
        }

        private void FullWidthLog()
        {
            //txtLog.Width = SystemParameters.PrimaryScreenWidth - 24;
            txtLog.Height = SystemParameters.PrimaryScreenHeight - 480;
        }

        private void CenterForm()
        {
            // Get the screen dimensions
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            // Get the window dimensions
            double windowWidth = this.Width;
            double windowHeight = this.Height;

            // Calculate the left and top positions to center the window
            double left = (screenWidth - windowWidth) / 2;
            double top = (screenHeight - windowHeight) / 2;

            // Set the window's position
            this.Left = left;
            this.Top = top;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.srt)|*.srt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                txtFile.Text = openFileDialog.FileName;
                if (string.IsNullOrEmpty(txtFile.Text))
                {
                    MessageBox.Show("File name empty.");
                }
                fileInputContent = File.ReadAllText(openFileDialog.FileName);
                if (string.IsNullOrEmpty(fileInputContent))
                {
                    MessageBox.Show("File no content.");
                }
                WriteLog("Read file done.");
            }
        }

        private void Button_Dowload(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(fileInputContent))
            {
                MessageBox.Show("Please choose file.");
                return;
            }
            WriteLog("Begin extract text from file.");
            var texts = SRTUtility.ExtractSrt(fileInputContent);
            WriteLog("Extract text from file done.");
            WriteLog("Begin dowload...");
            WriteLog(string.Join("\n", texts));

            Task.Run(async () =>
            {
                using (var httpClient = new HttpClient())
                {

                    var uri = new Uri("https://api.fpt.ai/hmi/tts/v5");
                    httpClient.DefaultRequestHeaders.Add("api-key", "hccDEEYRsrddTX9C1TE7sMj0EJOEzbn1");
                    httpClient.DefaultRequestHeaders.Add("speed", "");
                    httpClient.DefaultRequestHeaders.Add("voice", "banmai");
                    httpClient.DefaultRequestHeaders.Add("callback_url", "https://4723-14-191-165-222.ngrok-free.app/api/fpt/listen?index=1");
                    httpClient.DefaultRequestHeaders
                      .Accept
                      .Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    foreach (var item in texts)
                    {
                        var content = new FormUrlEncodedContent(new[]
                        {
                                new KeyValuePair<string, string>(item, "")
                        });

                        var response = await httpClient.PostAsync(uri, content);
                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine(responseContent);
                        }
                        else
                        {
                            Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                        }
                    }
                }
            });
        }

        private async Task DoLongRunningTask()
        {
            await Task.Run(() =>
            {

            });
        }

        private bool WriteLog(string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"{DateTime.Now} - {message}\n");
                txtLog.ScrollToEnd();
            });

            return true;
        }
    }
}