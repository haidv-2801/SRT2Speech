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
using Microsoft.Extensions.DependencyInjection;
using SRT2Speech.ProxyService.Interfaces;

namespace SRT2Speech.AppWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitContent();
        }
      
       

        private void InitContent()
        {
            CenterForm();
            InitWindow();
        }

        private void InitWindow()
        {
            var proxyManager = ServiceLocator.GetService<IProxyManager>();
            if (proxyManager == null)
            {
                MessageBox.Show("Error: IProxyManager service not available", "Service Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ElevenlabVoiceControl elevenLabControl = new ElevenlabVoiceControl(proxyManager);
            TabItem newTab4 = new TabItem();
            newTab4.Header = "Elevenlab";
            newTab4.Content = elevenLabControl;
            newTab4.IsEnabled = true;
            tabControl.Items.Add(newTab4);

            // Set focus to Elevenlab tab
            tabControl.SelectedItem = newTab4;
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
    }
}