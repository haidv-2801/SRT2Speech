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


    internal class Program
    {
        private static readonly byte[] _key = Encoding.UTF8.GetBytes("XSVmBQXytXdtYpQ7ObPAkCxIULQbdxLN");
        private static readonly byte[] _iv = Encoding.UTF8.GetBytes("2VxcOyKWh2KGVnPt");

        static void Main(string[] args)
        {
            try
            {
                //string m = "0a0027000006";
                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;
                Console.WriteLine("Nhập MAC:");
                string mac = Console.ReadLine()!;
                mac = mac.ToLowerInvariant().Replace(":", "").Replace("-", "").Replace("_", "");

                Console.WriteLine("Nhập số ngày:");
                string day = Console.ReadLine()!;

                string plainText = $"{mac}__{DateTime.Now.AddDays(int.Parse(day)).ToString("dd/MM/yyyy")}";
                string encrypt = AESEncryption.EncryptAES(plainText);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "key.txt");

                // Write text to the file
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine(encrypt);
                }
                Console.WriteLine($"Key {encrypt} đã được tạo vào file {filePath}");
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"[ERROR] Số ngày phải nhập số");
                Console.WriteLine("[ERROR]" + ex.Message);
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
