using SRT2Speech.LicenseManager;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SRT2Speech.GenKey
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Tùy chọn: Cấu hình đường dẫn file license khác
            // string customPath = @"C:\MyAppData\license.key";
            // var licenseService = new LicensingService(customPath);

            // Tùy chọn (QUAN TRỌNG TRONG THỰC TẾ): Cấu hình Public Key
            // string publicKey = "<RSAKeyValue>...</RSAKeyValue>"; // Lấy từ cấu hình an toàn
            // licenseService.SetPublicKey(publicKey);

            //Console.WriteLine("Initializing Licensing Service...");
            //var licenseService = new LicensingService(); // Dùng đường dẫn mặc định

            //Console.WriteLine($"Attempting to validate license file at: {licenseService.LicenseFilePath}"); // Truy cập private field không được, cần public property nếu muốn xem

            //LicenseResult result = licenseService.ValidateLicense();

            //Console.WriteLine($"Validation Status: {result.Status}");
            //Console.WriteLine($"Message: {result.Message}");

            //if (result.IsValid)
            //{
            //    Console.WriteLine($"Customer: {result.LicenseDetails?.CustomerName ?? "N/A"}");
            //    Console.WriteLine($"Expires (UTC): {result.LicenseDetails?.ExpirationDateUtc}");
            //    // ---> Khởi chạy logic chính của ứng dụng ở đây <---
            //}
            //else
            //{
            //    Console.WriteLine("\nLicense is INVALID. Application cannot run in licensed mode.");
            //    if (result.ErrorException != null)
            //    {
            //        Console.WriteLine($"Error Details: {result.ErrorException.Message}");
            //    }
            //    // ---> Hiển thị thông báo lỗi, yêu cầu kích hoạt, hoặc thoát <---
            //}

            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Nhấn phím ESC để thoát chương trình.");
            while (true)
            {
                if (Console.KeyAvailable) //Kiểm tra xem phím có được nhấn không
                {
                    ConsoleKeyInfo key = Console.ReadKey(true); //Lấy phím đã nhấn và không hiển thị nó trên console
                    if (key.Key == ConsoleKey.Escape) //Kiểm tra xem phím đó có phải là ESC
                    {
                        Console.WriteLine("\nThoát chương trình.");
                        break;
                    }
                }

                string macAddress = GetValidMacAddress();
                int days = GetValidDays();
                string username = "dvhai";//GetValidUsername();

                Console.WriteLine("\nThông tin người dùng:");
                Console.WriteLine($"Địa chỉ MAC: {macAddress}");
                Console.WriteLine($"Số ngày: {days}");
                Console.WriteLine($"Tên người dùng: {username}");

                CreateDemoLicenseFile(macAddress, username, DateTime.UtcNow.AddDays(days));
                var licenseService = new LicensingService();
                var validateResult = licenseService.ValidateLicense();

                Console.WriteLine("\nNhấn ESC để thoát hoặc nhấn Enter để tiếp tục");
                Console.ReadLine();
            }
        }


        // --- Phần tạo license demo (Để chạy thử) ---
        // Bạn cần tạo file license trước khi chạy Validate
        // Hàm này nên nằm trong Tool riêng, không nên ở đây
        static void CreateDemoLicenseFile(string mac, string userName, DateTime expiry)
        {
            var licenseService = new LicensingService();
            var licenseInfo = new LicenseInfo
            {
                LicensedMacAddress = mac.ToUpper(),
                ExpirationDateUtc = expiry.ToUniversalTime(),
                CustomerName = userName
            };
            string jsonData = JsonSerializer.Serialize(licenseInfo, new JsonSerializerOptions { WriteIndented = true });

            // !!! MÔ PHỎNG MÃ HÓA/KÝ SỐ: DÙNG BASE64 !!!
            // THAY THẾ BẰNG HÀM KÝ SỐ/MÃ HÓA THỰC TẾ BẰNG PRIVATE KEY
            string encodedData = licenseService.Encrypt(jsonData);

            try
            {
                File.WriteAllText("license.lic", encodedData, Encoding.UTF8);
                Console.WriteLine($"Created demo license file 'license.lic' for MAC {mac}, expires {expiry.ToString("dd/MM/yyyy")}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create demo license file: {ex.Message}");
            }

        }

        // Ví dụ gọi tạo license trước khi validate (chỉ để test)
        //static void Main(string[] args)
        //{
        //    string? myMac = new LicensingService().GetHardwareId();
        //    if (myMac != null) CreateDemoLicenseFile(myMac, DateTime.Now.AddDays(30));
        //    // ... phần validate như trên ...
        //}

        public static string GetValidMacAddress()
        {
            string macAddress;
            Regex macRegex = new Regex("^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$");

            while (true)
            {
                Console.Write("Nhập địa chỉ MAC: ");
                macAddress = Console.ReadLine();

                if (macRegex.IsMatch(macAddress))
                {
                    return macAddress;
                }
                else
                {
                    Console.WriteLine("Địa chỉ MAC không hợp lệ. Vui lòng nhập lại.");
                }
            }
        }

        public static int GetValidDays()
        {
            int days;
            while (true)
            {
                Console.Write("Nhập số ngày: ");
                if (int.TryParse(Console.ReadLine(), out days) && days > 0)
                {
                    return days;
                }
                else
                {
                    Console.WriteLine("Số ngày không hợp lệ. Vui lòng nhập lại.");
                }
            }
        }
    }
}


