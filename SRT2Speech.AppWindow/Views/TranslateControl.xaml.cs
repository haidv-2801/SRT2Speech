using Microsoft.Win32;
using Newtonsoft.Json;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.AppWindow.Services;
using SRT2Speech.AppWindow.ViewModels;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SubtitlesParser.Classes;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Interaction logic for TranslateControl.xaml
    /// </summary>
    public partial class TranslateControl : UserControl
    {
        string fileInputContent;
        string outFolder;
        string prompt = "You are a translator, let translate text bellow to Vietnamese";
        bool isValidKey = false;
        TranslateConfig _tranConfig;
        ConcurrentDictionary<string, SubtitleItem> _trackError;

        public TranslateControl()
        {
            DataContext = new TranslateControlViewModel();
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }
        private void InitDefaultValue()
        {
            ReadKey();
            if (!ThrowKeyValid())
            {
                return;
            }
            _trackError = new ConcurrentDictionary<string, SubtitleItem>();
            _tranConfig = YamlUtility.Deserialize<TranslateConfig>(File.ReadAllText(System.IO.Path.Combine($"{Directory.GetCurrentDirectory()}/Configs", "TranslateConfig.yaml")));
            WriteLog($"Thông tin cấu hình Translate {JsonConvert.SerializeObject(_tranConfig)}");
            fileInputContent = string.Empty;
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
        }

        private void FullWidthLog()
        {
            txtLog.Height = SystemParameters.PrimaryScreenHeight - 480;
        }

        private void ButtonSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!ThrowKeyValid())
            {
                return;
            }
            var folderDialog = new OpenFolderDialog
            {
                Title = "Mở thư mục"
            };

            if (folderDialog.ShowDialog() == true)
            {
                var folderName = folderDialog.FolderName;
                outFolder = folderName;
                WriteLog($"Thư mục xuất file dịch: {outFolder}");
            }
        }

        private StringContent GetContent(string text, string prompt)
        {
            var requestBody = new
            {
                contents = new { role = "user", parts = new object[] { new { text = $"{prompt}. {_tranConfig.Prompt}\n@@@{text}@@@" } } },
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            return content;
        }

        private void Button_Translate(object sender, RoutedEventArgs e)
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
            WriteLog("Begin translate text from file.");
            var parser = new SubtitlesParser.Classes.Parsers.SrtParser();
            using var fileStream = File.OpenRead(txtFile.Text);
            var texts = parser.ParseStream(fileStream, Encoding.UTF8);
            WriteLog($"Tổng số bản ghi dịch là {texts.Count}");
            WriteLog("Begin translate...");
            _trackError.Clear();

            if (string.IsNullOrEmpty(outFolder))
            {
                WriteLog($"Lỗi chưa chọn thư mục kết quả");
                return;
            }


            //MessageBoxResult result = MessageBox.Show(
            //    $"Bạn có muốn xóa các file dịch trong thư mục đích?",
            //    "Info",
            //    MessageBoxButton.YesNo,
            //    MessageBoxImage.Question);

            //if (result == MessageBoxResult.Yes)
            //{ 

            //}

            _ = StartTranslate(texts);
        }

        private bool WriteLog(string message)
        {
            if (txtLog == null)
            {
                return false;
            }
            this.Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"{DateTime.Now} - {message}\n");
                txtLog.ScrollToEnd();
            });

            return true;
        }

        private async Task StartTranslate(List<SubtitleItem> texts)
        {
            if (texts.Any(f => f.Line == "##"))
            {
                WriteLog($"Tồn tại các dòng trống ở vị trí {string.Join(", ", texts.Where(f => f.Line == "##").Select(f => f.Index))}");
                return;
            }

            if (string.IsNullOrEmpty(prompt))
            {
                WriteLog($"Lỗi prompt trống");
                return;
            }

            await Task.Run(async () =>
            {
                try
                {
                    string apiKey = _tranConfig.ApiKey;
                    string url = _tranConfig.Url.Replace("#key#", apiKey); ;
                    using (var client = new HttpClient())
                    {
                        var chunks = texts.ChunkBy(_tranConfig.MaxItems);
                        var text = chunks.Select(f => string.Join("\n", f.Select(p => $"{p.Index}. {p.Line.Trim()}")));

                        foreach (var item in text)
                        {
                            //_trackError.AddOrUpdate(f.Index.ToString(), f, (_, _) => f);
                            var response = await RetryWithJitterAndPolly.ExecuteWithRetryAndJitterAsync(async () =>
                            await client.PostAsync(url, GetContent(item, prompt)), (res) =>
                            {
                                bool success = res.IsSuccessStatusCode;
                                if (!success)
                                {
                                    string content = res.Content.ReadAsStringAsync().Result;
                                    if (content.Contains("API_KEY_INVALID"))
                                    {
                                        WriteLog($"[ERROR] Đang dịch lại - Message: API key không hợp lệ");
                                    }
                                    else
                                    {
                                        WriteLog($"[ERROR] {content} Đang dịch lại lại text thứ ");
                                    }
                                }
                                return success;
                            });
                            WriteLog($"[SUCCESS] Gửi request dịch.");
                            var responseContent = await response.Content.ReadAsStringAsync();
                            if (response.IsSuccessStatusCode)
                            {
                                try
                                {
                                    var jsonDoc = JsonDocument.Parse(responseContent);
                                    var res = jsonDoc.RootElement
                                        .GetProperty("candidates")[0]
                                        .GetProperty("content")
                                        .GetProperty("parts")[0]
                                        .GetProperty("text")
                                        .GetString();

                                    res = res.Replace("@@@", "");
                                    string filePath = "translated.srt";
                                    var resSplit = res.Split("\n").Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f));
                                    using (StreamWriter sw = new StreamWriter(Path.Combine(outFolder, filePath), true))
                                    {
                                        foreach (var line in resSplit)
                                        {
                                            int index = int.Parse(line.Substring(0, line.IndexOf(".")).Replace(".", ""));
                                            string t = line.Substring(line.IndexOf(".") + 1);
                                            SubtitleItem fi = texts.Find(a => a.Index == index);

                                            if (fi != null)
                                            {
                                                fi.Lines = new List<string>() { t.Trim() };

                                                sw.WriteLine($"{fi.ToSRTString()}\n");

                                            }

                                            _trackError.Remove(index.ToString(), out SubtitleItem? _);
                                            WriteLog($"[SUCCESS] Dịch thành công, đã lưu file {index}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WriteLog($"[ERROR] {ex.Message}");
                                }
                            }
                            else
                            {
                                WriteLog("[ERROR]: " + responseContent);
                            }
                            WriteLog($"Bắt đầu nghỉ {_tranConfig.SleepTime} giây");
                            await Task.Delay(TimeSpan.FromSeconds(_tranConfig.SleepTime));
                        }

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    WriteLog($"Xuất hiện lỗi {ex.Message}");
                }

            });
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

            }
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
                fileInputContent = File.ReadAllText(openFileDialog.FileName);
                if (string.IsNullOrEmpty(fileInputContent))
                {
                    MessageBox.Show("File no content.");
                }
                WriteLog("Read file done.");
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (promptCombobox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (selectedItem.Content != null)
                {
                    prompt = GetPromptCombobox(selectedItem.Content.ToString()!);
                    WriteLog($"Chọn prompt: {selectedItem.Content}");
                }
            }
        }

        //private string GetPromptCombobox(string content)
        //{
        //    return content switch
        //    {
        //        "To English" => "You are a translator, let translate text bellow to English and don't give any redudant text",
        //        "To Vietnamese" => "You are a translator, let translate text bellow to Vietnamese and don't give any redudant text",
        //        _ => "You are a translator, let translate text bellow to Vietnamese and don't give any redudant text"
        //    };
        //}

        private string GetPromptCombobox(string content)
        {
            return content switch
            {
                "To English" => "Hãy dịch giúp tôi ngôn ngữ sau sang tiếng anh, tôi mong muốn chỉ nhận được kết quả dịch và không muốn thêm bất cứ text nào vào kết quả của tôi và hãy giữ giúp tôi 3 kí tự @@@ trong kết quả và không đưa ra bất cứ giải thích nào",
                "To Vietnamese" => "Hãy dịch giúp tôi ngôn ngữ sau sang tiếng việt, tôi mong muốn chỉ nhận được kết quả dịch và không muốn thêm bất cứ text nào vào kết quả của tôi và hãy giữ giúp tôi 3 kí tự @@@ trong kết quả và không đưa ra bất cứ giải thích nào",
                _ => "Hãy dịch giúp tôi ngôn ngữ sau sang tiếng việt, tôi mong muốn chỉ nhận được kết quả dịch và không muốn thêm bất cứ text nào vào kết quả của tôi và hãy giữ giúp tôi 3 kí tự @@@ trong kết quả và không đưa ra bất cứ giải thích nào"
            };
        }
    }
}
