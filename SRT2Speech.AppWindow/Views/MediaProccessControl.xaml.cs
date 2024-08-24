using Microsoft.Win32;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.Core.Audio;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.Socket.Client;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SRT2Speech.AppWindow.Views
{
    /// <summary>
    /// Interaction logic for MediaProccessControl.xaml
    /// </summary>
    public partial class MediaProccessControl : UserControl
    {
        string fileInputContent;
        VbeeConfig _vbeeConfig;
        SignalRConfig _signalR;
        MessageClient _messageClient;
        string outFolder;

        public MediaProccessControl()
        {
            InitializeComponent();
            InitDefaultValue();
            InitContent();
        }

        private void InitDefaultValue()
        {
            //_signalR = YamlUtility.Deserialize<SignalRConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SignalRConfig.yaml")));
            _vbeeConfig = YamlUtility.Deserialize<VbeeConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "ConfigVbee.yaml")));
            //_messageClient = new MessageClient(_signalR.HubUrl, SignalMethods.SIGNAL_LOG_EN_VOICE);
            //_ = _messageClient.CreateConncetion(async (object message) =>
            //{
            //    string msg = $"{message}";
            //    WriteLog(msg);
            //});

            fileInputContent = string.Empty;
            txtLog.AppendText("Logging...");
        }

        private void InitContent()
        {
            FullWidthLog();
        }

        private void FullWidthLog()
        {
            txtLog.Height = SystemParameters.PrimaryScreenHeight - 480;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Mở thư mục"
            };

            if (folderDialog.ShowDialog() == true)
            {
                var folderName = folderDialog.FolderName;
                txtFile.Text = folderName;
                WriteLog($"Thư mục mp3: {txtFile.Text}");
            }
        }

        private void ButtonSrt_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.srt)|*.srt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                txtFileSrt.Text = openFileDialog.FileName;
                if (string.IsNullOrEmpty(txtFile.Text))
                {
                    MessageBox.Show("File name empty.");
                }
                fileInputContent = File.ReadAllText(openFileDialog.FileName);
                if (string.IsNullOrEmpty(fileInputContent))
                {
                    MessageBox.Show($"File {openFileDialog.FileName} không chứa content");
                    WriteLog($"File {openFileDialog.FileName} không chứa content");
                }
                WriteLog($"Read file {openFileDialog.FileName} done.");
            }
        }

        private void StartProgressBar(double percent)
        {
            progressBar.Dispatcher.Invoke(() =>
            {
                progressBar.Value = percent;
                percentText.Text = $"{percent}%";
            });
        }

        private void RefreshProggressBar()
        {
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
        }

        private void Button_Merge(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(outFolder))
            {
                MessageBox.Show("Chọn thư mục lưu");
                return;
            }
            if (string.IsNullOrEmpty(txtFileSrt.Text) || string.IsNullOrEmpty(fileInputContent))
            {
                MessageBox.Show("Chọn file SRT");
                return;
            }
            try
            {
                string mp3Folder = txtFile.Text;
                string srtPath = txtFileSrt.Text;
                RefreshProggressBar();
                btnMerge.IsEnabled = false;
                Task.Run(async () =>
                {
                    try
                    {
                        var mProcessor = new MediaProcessor(mp3Folder, @"D:\tool\sample.mp4", srtPath);
                        StringBuilder logs = new StringBuilder();
                        await mProcessor.CompressAudioBySubtitles(msg =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                //logs.AppendLine(msg);
                                //if(logs.Length > 100)
                                //{
                                //    WriteLog(logs.ToString());
                                //    logs.Clear();
                                //}
                            });
                        });
                        this.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Merge file thành công {outFolder}!!");
                            WriteLog($"Merge file thành công {outFolder}!!");
                            btnMerge.IsEnabled = true;
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Lỗi merge file {outFolder} {ex.Message}");
                            WriteLog($"Lỗi merge file {outFolder} {ex.Message}");
                        });
                    }

                });
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private bool ValidateFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                MessageBox.Show("OK");
                return true;
            }

            MessageBox.Show($"Thư mục {folder} không tồn tại");
            return false;
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

        private void ButtonOutput_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Mở thư mục"
            };

            if (folderDialog.ShowDialog() == true)
            {
                var folderName = folderDialog.FolderName;
                outFolder = folderName;
                WriteLog($"Thư mục xuất file mp4: {outFolder}");
            }
        }
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
