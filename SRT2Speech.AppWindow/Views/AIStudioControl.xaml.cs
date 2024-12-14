using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using SRT2Speech.Core.Constants;
using SRT2Speech.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
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
using System.Windows.Threading;

namespace SRT2Speech.AppWindow.Views
{
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    /// <summary>
    /// Interaction logic for AIStudioControl.xaml
    /// </summary>
    public partial class AIStudioControl : UserControl
    {
        string currentPos = "";
        private bool isGettingCoordinates = false;
        DispatcherTimer dt = new System.Windows.Threading.DispatcherTimer();

        private DispatcherTimer timer;
        private int clickInterval = 2000; // Thời gian giữa các lần nhấp (ms)
        private int clickCount = 0;
        private bool isRunning = false;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT pPoint);

        // Định nghĩa hàm SetCursorPos
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetCursorPos(int X, int Y);


        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;


        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const byte VK_SHIFT = 0x10; // Mã phím Shift
        private const byte VK_RETURN = 0x0D; // Mã phím Enter
        private const uint KEYEVENTF_KEYUP = 0x0002; // Thả phím
        private const byte VK_NEXT = 0x22; // Page Down
        private const byte VK_END = 0x23; // End key

        //Khóa chuột
        [DllImport("MouseLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void LockMouse();

        [DllImport("MouseLock.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void UnlockMouse();

        // Các mã phím
        private const byte VK_CONTROL = 0x11; // Mã phím Ctrl

        //Khai báo các biến

        Dictionary<string, bool> ProcessedID = new Dictionary<string, bool>();

        public AIStudioControl()
        {
            InitializeComponent();

            _ = ReceivedEventKeys();
        }

        private async Task ReceivedEventKeys()
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/message")
                .Build();

            connection.On("ReceiveKeyEvents", (List<string> eventKeys) =>
            {
                var (valid, message) = ValidateCoordinates(currentPos);
                if (!valid)
                {
                    WriteLog($"Không sử lý được event {message}");
                    MessageBox.Show($"Error: {message}");
                }
                else
                {
                    ProcessEventKeys(eventKeys);
                }
                WriteLog($"Event nhận từ chrome: {JsonConvert.SerializeObject(eventKeys)}");
            });

            connection.On("ReceiveAIResult", (List<MessageModel> models) =>
            {
                ProcessAIResult(models);
            });

            connection.On("ReceiveMessage", (string msg) =>
            {
                WriteLog(msg);
            });

            connection.On("ResetIndex", (object par) =>
            {
                ProcessedID.Clear();
                WriteLog("Đã reset index");
            });

            try
            {
                await connection.StartAsync();
                WriteLog("Connection started. Listening for messages...");
            }
            catch (Exception ex)
            {
                WriteLog($"Error connecting to the hub: {ex.Message}");
            }
        }

        private void ProcessAIResult(List<MessageModel> models)
        {
            foreach (var model in models)
            {
                string key = $"{model.File}__{model.ID}";
                if (!ProcessedID.ContainsKey(key))
                {
                    string path = System.IO.Path.Combine($"{Directory.GetCurrentDirectory()}/Files/AiStudio", model.File);

                    try
                    {
                        using (StreamWriter writer = new StreamWriter(path, true))
                        {
                            writer.WriteLine(model.Message);
                            writer.WriteLine();
                        }

                        ProcessedID[key] = true;
                        WriteLog("Text appended successfully.");
                    }
                    catch (Exception ex)
                    {
                        WriteLog("An error occurred: " + ex.Message);
                        WriteLog($"Error file: {model.File}, item: " + model.Message);
                    }
                }
            }
            WriteLog($"Event nhận từ chrome: {JsonConvert.SerializeObject(models)}");
        }

        /// <summary>
        /// Sử lý event key
        /// </summary>
        /// <param name="eventKeys"></param>
        private void ProcessEventKeys(List<string> eventKeys)
        {
            foreach (string key in eventKeys)
            {
                if (key == EventKeys.Enter)
                {
                    SimulateEnter();
                    WriteLog($"Enter...");
                }

                if (key == EventKeys.MouseLeft)
                {
                    var (x, y) = GetCoordinate(currentPos);
                    if (x != 0 || y != 0)
                    {
                        ClickAtPosition(x, y);
                        WriteLog($"Clicking X: {x}, Y: {y}");
                    }
                }

                if (key == EventKeys.CtrlPgdn)
                {
                    SimulateCtrlPgDn();
                    WriteLog($"SimulateCtrlPgDn");
                }
            }
        }

        private void SimulateCtrlEnter()
        {
            // Nhấn phím Ctrl
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            // Nhấn phím Enter
            keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
            // Thả phím Enter
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            // Thả phím Ctrl
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void SimulateCtrlPgDn()
        {
            var (x, y) = GetCoordinate(currentPos);
            GetCursorPos(out POINT _currentPos);
            SetCursorPos((int)x, (int)y);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, UIntPtr.Zero);
            Thread.Sleep(300);
            // Press Ctrl
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            // Press Page Down
            keybd_event(VK_END, 0, 0, UIntPtr.Zero);
            // Release Page Down
            keybd_event(VK_END, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            // Release Ctrl
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            Thread.Sleep(100);
            SetCursorPos(_currentPos.X, _currentPos.Y);
        }


        private void SimulateEnter()
        {
            // Nhấn phím Enter
            keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
            // Thả phím Enter
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void SimulateShiftEnter()
        {
            // Nhấn phím Shift
            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
            // Nhấn phím Enter
            keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
            // Thả phím Enter
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            // Thả phím Shift
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }


        private void ClickAtPosition(double x, double y)
        {
            GetCursorPos(out POINT currentPos);
            SetCursorPos((int)x, (int)y);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, UIntPtr.Zero);
            SetCursorPos(currentPos.X, currentPos.Y);
        }

        private void timer_tick(object sender, EventArgs e)
        {
            POINT pnt;
            GetCursorPos(out pnt);
            txtCoordination.Text = $"X: {pnt.X}, Y: {pnt.Y}";
        }

        private void ButtonGetCoordination_Click(object sender, RoutedEventArgs e)
        {
            if (!isGettingCoordinates)
            {
                // Bật chế độ lấy tọa độ
                isGettingCoordinates = true;
                txtCoordination.Text = "Bấm vào một điểm trên màn hình để lấy tọa độ";
                btnMove.Content = "Dừng lấy tọa độ...";

                dt.Stop();
                dt.Tick += new EventHandler(timer_tick);
                dt.Interval = new TimeSpan(0, 0, 0, 0, 300);
                dt.Start();
            }
            else
            {
                dt.Stop();
                btnMove.Content = "Lấy tọa độ";
                isGettingCoordinates = false;
            }
        }

        private (double, double) GetCoordinate(string label)
        {
            var (valid, msg) = ValidateCoordinates(label);
            if (!valid)
            {
                MessageBox.Show($"Error {label}, {msg}");
                return (0, 0);
            }

            var sp = label.Split(',');
            return (double.Parse(sp[0]), double.Parse(sp[1]));
        }

        private (bool, string) ValidateCoordinates(string input)
        {

            // Kiểm tra xem chuỗi có chứa dấu phẩy không
            if (!input.Contains(","))
            {
                return (false, "Định dạng không hợp lệ. Vui lòng nhập dưới dạng x,y.");
            }

            // Tách chuỗi thành hai phần
            string[] parts = input.Split(',');
            if (parts.Length != 2)
            {
                return (false, "Định dạng không hợp lệ. Vui lòng nhập dưới dạng x,y.");
            }

            string mgs = "";

            // Kiểm tra và chuyển đổi từng phần thành số
            if (double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                // Nếu hợp lệ, bạn có thể xử lý tọa độ
                mgs = $"Tọa độ hợp lệ: X = {x}, Y = {y}";
            }
            else
            {
                mgs = "Giá trị không hợp lệ. Vui lòng nhập số.";

                return (false, mgs);
            }

            return (true, mgs);
        }

        private void ButtonRun_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                //timer.Stop();
                isRunning = false;
                WriteLog($"Dừng chạy");
                return;
            }

            if (isGettingCoordinates)
            {
                MessageBox.Show("Cần dùng lấy tọa độ trước");
                return;
            }
            isRunning = true;
            WriteLog($"Bắt đầu chạy... với tọa độ set là {txtCoordination1.Text}");
            currentPos = txtCoordination1.Text;

            //timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromMilliseconds(clickInterval);
            //timer.Tick += Timer_Tick;
            //timer.Start();
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

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                return;
            }

            var (x, y) = GetCoordinate(txtCoordination1.Text);
            if (x != 0 || y != 0)
            {
                ClickAtPosition(x, y);
                SimulateEnter();
                WriteLog($"Clicking X: {x}, Y: {y}");
            }
            else
            {
                MessageBox.Show("Không chạy được timer click...");
                timer.Stop();
            }
        }

        // Hàm để lấy đối tượng con theo loại
        private static DependencyObject GetDescendantByType(DependencyObject parent, System.Type type)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && type.IsInstanceOfType(child))
                {
                    return child;
                }
                var childOfChild = GetDescendantByType(child, type);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private void btnScroll_Click(object sender, RoutedEventArgs e)
        {
            // Lấy ScrollViewer của RichTextBox
            var scrollViewer = GetDescendantByType(txtLog, typeof(ScrollViewer)) as ScrollViewer;

            if (scrollViewer != null)
            {
                // Cuộn xuống cuối RichTextBox
                scrollViewer.ScrollToEnd();
            }
        }
    }
}
