using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.AppWindow.Views;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SubtitlesParser.Classes;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace SRT2Speech.AppWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string fileInputContent;
        string nameFileInput;
        string location = "";
        bool isValidKey = true;
        FptConfig _fptConfig;
        ConcurrentDictionary<string, SubtitleItem> _trackError;
        public MainWindow()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }
        private void InitDefaultValue()
        {

            //ReadKey();
            if (!ThrowKeyValid())
            {
                return;
            }
            location = Directory.GetCurrentDirectory();
            CreateFolders();
            btnDowloadError.IsEnabled = false;
            _trackError = new ConcurrentDictionary<string, SubtitleItem>();
            _fptConfig = YamlUtility.Deserialize<FptConfig>(File.ReadAllText(System.IO.Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "ConfigFpt.yaml")));
            WriteLog($"Thông tin cấu hình {JsonConvert.SerializeObject(_fptConfig)}");
            fileInputContent = string.Empty;
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
                WriteLog(ex.Message);
            }

            return true;
        }

        private bool ThrowKeyValid()
        {
            if (!isValidKey)
            {
                WriteLog($"Key app không hợp lệ!!!");
            }

            return isValidKey;
        }

        private void ReadKey()
        {
            try
            {
                string key = File.ReadAllText(System.IO.Path.Combine($@"{Directory.GetCurrentDirectory()}\Configs", "key.txt"));
                string decrypt = AESEncryption.DecryptAES(key);
                string date = decrypt.Split("__")[1];
                string mac = decrypt.Split("__")[0];
                if (string.IsNullOrEmpty(key))
                {
                    isValidKey = false;
                }
                isValidKey = EncodeUtility.IsValidKey(key);
                WriteLog($"Thông tin key: {key}, Valid key = {isValidKey}, Date = {date}, mac = {mac}");
            }
            catch (Exception ex)
            {
                isValidKey = false;
                WriteLog(ex.Message);
            }
        }

        private void InitContent()
        {
            FullWidthLog();
            CenterForm();
            InitWindow();
        }

        private void InitWindow()
        {
            //VbeeUserControl vbeeControl = new VbeeUserControl();
            //TabItem newTab = new TabItem();
            //newTab.Header = "Vbee";
            //newTab.Content = vbeeControl;
            //tabControl.Items.Add(newTab);

            //EnglishVoiceControl enControl = new EnglishVoiceControl();
            //TabItem newTab1 = new TabItem();
            //newTab1.Header = "EnglishVoice";
            //newTab1.Content = enControl;
            //tabControl.Items.Add(newTab1);

            //TranslateControl tranControl = new TranslateControl();
            //TabItem newTab2 = new TabItem();
            //newTab2.Header = "Translate SRT";
            //newTab2.Content = tranControl;
            //tabControl.Items.Add(newTab2);

            AIStudioControl aiStudioControl = new AIStudioControl();
            TabItem newTab3 = new TabItem();
            newTab3.Header = "AiStudio";
            newTab3.Content = aiStudioControl;
            tabControl.Items.Add(newTab3);

            //ElevenlabVoiceControl elevenLabControl = new ElevenlabVoiceControl();
            //TabItem newTab4 = new TabItem();
            //newTab4.Header = "Elevenlab";
            //newTab4.Content = elevenLabControl;
            //tabControl.Items.Add(newTab4);
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
            if (!ThrowKeyValid())
            {
                return;
            }
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.srt)|*.srt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                txtFile.Text = openFileDialog.FileName;


                if (string.IsNullOrEmpty(txtFile.Text))
                {
                    MessageBox.Show("File name empty.");
                }
                nameFileInput = Path.GetFileName(openFileDialog.FileName);
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
            if (!ThrowKeyValid())
            {
                return;
            }
            if (string.IsNullOrEmpty(fileInputContent))
            {
                MessageBox.Show("Please choose file.");
                return;
            }
            WriteLog("Bắt đầu đọc file SRT.");
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using var fileStream = File.OpenRead(txtFile.Text);
            var texts = parser.ParseStream(fileStream, Encoding.UTF8);
            WriteLog($"Đọc xong file SRT. Tổng {texts.Count} file mp3 cần dowload.");
            WriteLog("Begin dowload...");
            Task.Run(async () => await StartT2S(texts));
        }

        private void Button_DowloadError(object sender, RoutedEventArgs e)
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            if (!_trackError.Any())
            {
                MessageBox.Show("Không có bản ghi lỗi nào!!");
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"Còn {_trackError.Count} bản ghi chưa được tải về. Dowload tiếp?",
                "Tải bản ghi lỗi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var errIndexs = new HashSet<string>(_trackError.Keys);
                _trackError.Clear();

                var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
                using var fileStream = File.OpenRead(txtFile.Text);
                var texts = parser.ParseStream(fileStream, Encoding.UTF8);
                texts = texts.Where(f => errIndexs.Contains(f.Index.ToString())).ToList();

                if (!texts.Any())
                {
                    MessageBox.Show("Không có bản ghi lỗi nào!!");
                    return;
                }

                _ = StartT2S(texts);
            }
        }

        public HttpRequestMessage GetRqMessage(string url)
        {
            var rqGet = new HttpRequestMessage(HttpMethod.Get, $"{url}");
            rqGet.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return rqGet;
        }

        private async Task StartT2S(List<SubtitleItem> texts)
        {
            string directoryPath = "Files/FPT";
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            try
            {

                if (texts.Any(f => f.Line == "##"))
                {
                    WriteLog($"Tồn tại các dòng trống ở vị trí {string.Join(", ", texts.Where(f => f.Line == "##").Select(f => f.Index))}");
                    return;
                }
                if (texts.Any(f => f.Line.Length < 3))
                {
                    WriteLog($"Tồn tại các dòng nhỏ hơn 3 kí tự ở vị trí {string.Join(", ", texts.Where(f => f.Line.Length < 3).Select(f => f.Index))}");
                    return;
                }
                this.Dispatcher.Invoke(() =>
                {
                    btnDowloadError.IsEnabled = false;
                    btnDowload.IsEnabled = false;
                });
                using (var httpClient = new HttpClient())
                {
                    var uri = new Uri(_fptConfig.Url);
                    httpClient.DefaultRequestHeaders.Add("api-key", _fptConfig.ApiKey);
                    httpClient.DefaultRequestHeaders.Add("speed", string.IsNullOrEmpty(_fptConfig.Speed) ? "" : _fptConfig.Speed);
                    httpClient.DefaultRequestHeaders.Add("voice", _fptConfig.Voice);
                    httpClient.DefaultRequestHeaders.Add("callback_url", _fptConfig.CallbackUrl);
                    httpClient.DefaultRequestHeaders
                      .Accept
                      .Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header

                    var chunks = texts.ChunkBy(_fptConfig.MaxThreads);
                    foreach (var item in chunks)
                    {
                        var tasks = item.Select(async (f, index) =>
                        {
                            var content = new StringContent(f.Line, Encoding.UTF8, "application/json");
                            var response = await httpClient.PostAsync(uri, content);
                            _trackError.AddOrUpdate(f.Index.ToString(), f, (_, _) => f);
                            if (response.IsSuccessStatusCode)
                            {
                                var responseContent = await response.Content.ReadAsStringAsync();
                                var responseObject = JObject.Parse(responseContent);
                                string requestId = responseObject.GetSafeValue<string>("request_id");
                                string link = responseObject.GetSafeValue<string>("async");
                                WriteLog($"Gọi sang FPT thành công: {responseContent}");

                                _ = Task.Run(async () =>
                                {
                                    await Task.Delay(3000);
                                    var dowload = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(async () => await httpClient.SendAsync(GetRqMessage(link)), (res) =>
                                    {
                                        return res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound;
                                    });

                                    string filePath = Path.Combine(location, $"Files/FPT/{f.Index}.mp3");
                                    var stream = await dowload.Content.ReadAsStreamAsync();
                                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                                    {
                                        await stream.CopyToAsync(fileStream);
                                    }
                                    _trackError.Remove(f.Index.ToString(), out SubtitleItem? _);
                                    WriteLog($"[DOWLOADED] Dowload thành công {f.Index}.mp3, link = {link}");
                                });
                            }
                            else
                            {
                                string c = await response.Content.ReadAsStringAsync();
                                WriteLog($"Lỗi gọi sang Fpt: {response.StatusCode} - {response.ReasonPhrase} - {c}");
                            }
                        });
                        await Task.WhenAll(tasks);
                        WriteLog($"Bắt đầu đợi {_fptConfig.SleepTime} giây đến lần tiếp theo.");
                        await Task.Delay(TimeSpan.FromSeconds(_fptConfig.SleepTime));
                    }
                }
                if (_trackError.Any()) WriteLog($"Đã dowload xong còn {_trackError.Count} bản ghi chưa được dowload");
                else WriteLog($"Đã dowload xong {texts.Count} bản ghi");
                this.Dispatcher.Invoke(() =>
                {
                    btnDowloadError.IsEnabled = true;
                    btnDowload.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(() =>
                {
                    btnDowloadError.IsEnabled = true;
                    btnDowload.IsEnabled = true;
                });
                // Hiển thị MessageBox với thông tin lỗi
                MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                WriteLog($"Xuất hiện lỗi gọi sang FPT, có thể do tài khoản của bạn đã hết dung lượng, nếu chưa hết hãy thử giảm số luồng đồng thời xuống");
            }
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