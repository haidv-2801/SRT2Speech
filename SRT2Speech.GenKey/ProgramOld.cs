using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace SRT2Speech.GenKey
{
    public static class AESEncryption
    {
        private static readonly byte[] _key = Encoding.UTF8.GetBytes("XSVmBQXytXdtYpQ7ObPAkCxIULQbdxLN");
        private static readonly byte[] _iv = Encoding.UTF8.GetBytes("2VxcOyKWh2KGVnPt");

        public static string EncryptAES(string plaintext)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            using (var sw = new StreamWriter(cs))
                            {
                                sw.Write(plaintext);
                            }
                        }

                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        public static string DecryptAES(string ciphertext)
        {
            byte[] bytes = Convert.FromBase64String(ciphertext);

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    using (var ms = new MemoryStream(bytes))
                    {
                        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (var sr = new StreamReader(cs))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
        }
    }


    internal class Program1
    {
        static void Main1(string[] args)
        {
            try
            {
                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;

                Console.WriteLine("Nhập MAC:");
                string mac = Console.ReadLine()!;
                mac = mac.ToLowerInvariant().Replace(":", "").Replace("-", "").Replace("_", "");

                string endTimeInput;

                while (true)
                {
                    Console.Write("Nhập ngày tháng năm (dd/mm/yyyy): ");
                    endTimeInput = Console.ReadLine()!;

                    if (DateTime.TryParseExact(endTimeInput, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime endTime))
                    {
                        Console.WriteLine("Ngày tháng năm hợp lệ: " + endTime.ToString("dd/MM/yyyy"));


                        string plainText = $"{mac}__{endTime.ToString("dd/MM/yyyy")}";
                        string encrypt = AESEncryption.EncryptAES(plainText);
                        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "key.txt");

                        // Write text to the file
                        using (StreamWriter writer = new StreamWriter(filePath))
                        {
                            writer.WriteLine(encrypt);
                        }
                        Console.WriteLine($"Key {encrypt} đã được tạo vào file {filePath}");

                        break;
                    }
                    else
                    {
                        Console.WriteLine("Ngày tháng năm không hợp lệ. Vui lòng nhập lại.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi {ex.Message}");
            }
            Console.ReadLine();
        }

        public static string GetMacAddress()
        {
            var macAddress = string.Empty;

            // Get all network interfaces
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Find the first non-virtual network interface
            var physicalNetworkInterface = networkInterfaces.FirstOrDefault(ni =>
                ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            if (physicalNetworkInterface != null)
            {
                // Get the MAC address
                macAddress = string.Concat(physicalNetworkInterface.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
            }

            return macAddress.ToLowerInvariant();
        }

        public static bool IsValidKey(string key)
        {
            try
            {
                string decrypt = AESEncryption.DecryptAES(key);
                string date = decrypt.Split("__")[1];
                string mac = decrypt.Split("__")[0];
                bool isParsed = DateTime.TryParse(date, out DateTime licenseTime);
                if (!isParsed)
                    return false;
                if (licenseTime <= DateTime.Now)
                    return false;

                string m = GetMacAddress();
                
                if(m.Equals(mac, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
