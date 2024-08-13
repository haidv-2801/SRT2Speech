using AutoApp.Utility;
using Microsoft.Win32;
using Newtonsoft.Json;
using SRT2Speech.AppWindow.Models;
using SRT2Speech.Core.Audio;
using SRT2Speech.Core.Extensions;
using SRT2Speech.Core.Utilitys;
using SRT2Speech.Socket.Client;
using SRT2Speech.Socket.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

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
            _signalR = YamlUtility.Deserialize<SignalRConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "SignalRConfig.yaml")));
            _vbeeConfig = YamlUtility.Deserialize<VbeeConfig>(File.ReadAllText(System.IO.Path.Combine(Directory.GetCurrentDirectory(), "ConfigVbee.yaml")));
            _messageClient = new MessageClient(_signalR.HubUrl, SignalMethods.SIGNAL_LOG_EN_VOICE);
            _ = _messageClient.CreateConncetion(async (object message) =>
            {
                string msg = $"{message}";
                WriteLog(msg);
            });

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
            using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderDialog.Description = "Chọn thư mục";
                folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    //System.Diagnostics.Process.Start("explorer.exe", folderDialog.SelectedPath);
                    txtFile.Text = folderDialog.SelectedPath;
                    WriteLog($"Selected: {txtFile.Text}");
                }
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
            try
            {
                var textss = AudioProcessor.GetFileNames(txtFile.Text);
                RefreshProggressBar();
                Task.Run(() => {
                    AudioProcessor.MergeMP3Files(textss, Path.Combine(outFolder, "out.mp3"), (double per) =>
                    {
                        StartProgressBar(per);
                    });
                    this.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Merge file thành công!!");
                        WriteLog("Merge file thành công!!");
                    });
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
            if (AudioProcessor.ExistFolder(folder))
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
            using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderDialog.Description = "Chọn thư mục";
                folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    outFolder = folderDialog.SelectedPath;
                    WriteLog($"Thư mục xuất: {outFolder}");
                }
            }
        }
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
