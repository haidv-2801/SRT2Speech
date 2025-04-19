using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SRT2Speech.LicenseManager
{
    /// <summary>
    /// Provides services for validating application licenses.
    /// </summary>
    public class LicensingService
    {
        private string _licenseFilePath;
        private string _publicKeyFilePath;

        // *** THAY THẾ BẰNG PUBLIC KEY THỰC TẾ ***
        // Ví dụ: public key dạng XML cho RSA
        private string? _privateXml = "<RSAKeyValue><Modulus>0/RHinLuNIF3ueSdbFwpjARUFpjHhFEq9ZsLL/fvumsZAxcvDDOu2WQFL2469Pq93SAfTu1JsL/2Dee6AKoLBEeeBcXHcdXWH+8g+u/xhfnSOtgU6BkyTWO5cRq3k6WcHH+24MUzcphzYfaw9O7VQ3mamJYqM2H7m9gO8EVZa8WKF01ptwSYjST68A2JZYq7Y5wCmYtW+TrgUQv+ydOtN5V6mlX0IVNzvSBp5zH3M8jDJzdH6Hypiq/tmN1BNHI5DVlGyI71pYy96UGMZsadke+HjB/m9YFdvLmVk+QAM6RtAbGpSe7Bct1ev0DamHdoOAhFDDvOOllh/omk7Sg2oQ==</Modulus><Exponent>AQAB</Exponent><P>/gdgx2ZgGha0e1wo6hSK7gaoRuchCNFezM7+kIBbJzXibWrz9F7V8jptmvVolhmHPc7RFldx21tQL53J+Fls4CC89m32M1c0F7Cd9TjRj9lqrELDTJiw7kSA5gFbwYTQoDiYfpWXu+O8CS99Ioo6zzMG9CKgco3Zwu3dbaUWo28=</P><Q>1ZlSPdBs+Ts/Fbm5WFizcJ2di0OWKMAzaJ2rVi4HsX+2oOW/PMPXK7ozenu++QSdkkGb/DLRqoGg3PKLmwxC3rB+VIlZxmXcSmYvs6K5+pAT/Gj/y8YnBifujR8l7EiGcQjgqEjFrPHDeWOJA9gMqpuGCM/7luxcBSO4aeI7fu8=</Q><DP>Whirzo0U0Kv03zvUlxEqJTnfPSaEpOf9Q4624vFjYwjNghlI5QctwnfOoAMiUPpE6TsNFWt2SmKckgbYt2igIO37lztw8syfATf3Dh5U1Tw7hVU4h1YwipFpViptLLm6dNkd3OeZpV4kNu3M6A82xH5InRYa6gY9kdFjk5vV56k=</DP><DQ>vJJJOu1A559YiFOYv9g/QpLDvWkDriJQwHFCDClC/X8kulNaS8kRszcFr4KEDM/4VGC71yD+XStn2uf+O/nNJ1BllzhTG4ZdvwkL3+kvi3ebTWFdQodDeHnUbp4rGrjEiBIwVCE68v3Vdtta4RvhwtTetfT3xjwQ2TC55DCoGd0=</DQ><InverseQ>5GqMQV+nybxdUhdKNDau2UhrxkAuK/nlxZx+ei9Qi4p/F/iiV9DZmZBuAmC6+8L22rC8hINX62RQzN37PGqZ7ZxTOJaljFqXSfyPgI1LC16l4VtR/uKkOUkJOIE9/IE4oPYrrIIgDi/p7DwXihkUvZhkQxio2PDZFWeMd/2s2KE=</InverseQ><D>bBmtlyYw6WSSvKOlyotiwT+/51p6f7iqEWPHG+r3QHu44QOlAjMl3UgPAPbWaPWteicU8LtDFzuAce0ax5XyUnd4V/dcVmm6cMUUbAeOhkc4+rwURZ/8MM5I2S/IKyyO51YX3qGVMMAiMPeRnwpkiKpvt5z23wf9PKnATLkB9111JR5vXZlzgRIdq6Dg7Gh98TQzNYNLpIe4yQxiwTQVnkowrImvrBMHs1+Fo0KBfXAF3jy5ZVn9IXM18JTKh6ZWk2yzbzARaKxsZHSou+0gQ0L12TZcQykaRlHZKkIeRoDz56Cqm9QgfvpM/d3e5bgif3fZ+P+saFZ//gMGYgcChQ==</D></RSAKeyValue>";

        /// <summary>
        /// Initializes a new instance of the <see cref="LicensingService"/> class
        /// using the default license file name ("license.lic") located in the application's base directory.
        /// </summary>
        public LicensingService()
        {
            _licenseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.lic");
            _publicKeyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "key.txt");
        }

        public string LicenseFilePath
        {
            get
            {
                return _licenseFilePath;
            }
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="LicensingService"/> class
        /// with a specific license file path.
        /// </summary>
        /// <param name="licenseFilePath">The full path to the license file.</param>
        public LicensingService(string licenseFilePath)
        {
            _licenseFilePath = licenseFilePath;
        }

        /// <summary>
        /// Gets the primary hardware identifier (MAC Address) of the current machine.
        /// This ID should be provided by the user to the license issuer.
        /// </summary>
        /// <returns>The MAC address string or null if unable to retrieve.</returns>
        public string? GetHardwareId()
        {
            return HardwareIdProvider.GetPrimaryMacAddress();
        }

        /// <summary>
        /// Validates the license file specified during initialization or configuration.
        /// </summary>
        /// <returns>A <see cref="LicenseResult"/> object containing the validation status and details.</returns>
        public LicenseResult ValidateLicense()
        {
            // --- 1. Lấy Hardware ID ---
            string? currentMac = GetHardwareId();
            if (string.IsNullOrEmpty(currentMac))
            {
                return new LicenseResult(LicenseStatus.HardwareIdError, "Could not retrieve the MAC address of this machine.");
            }
            currentMac = currentMac.ToUpperInvariant(); // Chuẩn hóa

            // --- 2. Đọc File License ---
            string? rawLicenseData;
            try
            {
                if (!File.Exists(_licenseFilePath))
                {
                    return new LicenseResult(LicenseStatus.FileNotFound, $"License file not found at: '{_licenseFilePath}'");
                }
                // Đọc dữ liệu thô (có thể là base64 hoặc text đã mã hóa/ký số)
                rawLicenseData = File.ReadAllText(_licenseFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(rawLicenseData))
                {
                    return new LicenseResult(LicenseStatus.InvalidOrTampered, "License file is empty.");
                }
            }
            catch (IOException ioEx)
            {
                return new LicenseResult(LicenseStatus.Error, $"Error reading license file: {ioEx.Message}", exception: ioEx);
            }
            catch (Exception ex)
            {
                return new LicenseResult(LicenseStatus.Error, $"Unexpected error reading license file: {ex.Message}", exception: ex);
            }


            // --- 3. Xác thực và Giải mã (PHẦN MÔ PHỎNG) ---
            // !!! THAY THẾ PHẦN NÀY BẰNG LOGIC XÁC THỰC CHỮ KÝ (RSA) VÀ/HOẶC GIẢI MÃ (AES) THỰC TẾ !!!
            // Giả sử license là JSON được encode Base64 (Mô phỏng đơn giản)
            string? verifiedJsonData;
            try
            {
                // Mô phỏng: Nếu có Public Key thì giả bộ xác thực (luôn thành công nếu decode được)
                // Nếu không có Public Key thì không an toàn -> Báo lỗi hoặc cảnh báo? (Tùy thiết kế)
                if (string.IsNullOrEmpty(_privateXml))
                {
                    // Hoặc là không cho chạy, hoặc là chạy nhưng log warning
                    return new LicenseResult(LicenseStatus.Error, "Public key for verification is not configured.");
                }

                // Thử decode Base64
                //byte[] decodedBytes = Convert.FromBase64String(rawLicenseData);
                //verifiedJsonData = Encoding.UTF8.GetString(decodedBytes);

                // *** NƠI GỌI HÀM XÁC THỰC THỰC TẾ DÙNG _publicKeyXml ***
                verifiedJsonData = Decrypt(rawLicenseData);
            }
            catch (FormatException) // Lỗi nếu không phải Base64 hợp lệ
            {
                // Có thể đây là plain text JSON? Hoặc định dạng khác?
                // Trong ví dụ này, nếu không phải Base64, ta thử coi nó là JSON thường
                // CẢNH BÁO: Không an toàn nếu không có ký số!
                Console.WriteLine("Warning: License data does not appear to be Base64 encoded. Attempting to parse as plain JSON. THIS IS INSECURE without digital signatures.");
                verifiedJsonData = rawLicenseData;
                // Nếu bắt buộc phải mã hóa/ký số, thì ở đây nên trả về InvalidOrTampered
                return new LicenseResult(LicenseStatus.InvalidOrTampered, "License data is not in the expected format (Base64) or is corrupted.");
            }
            catch (Exception ex) // Các lỗi khác
            {
                return new LicenseResult(LicenseStatus.InvalidOrTampered, $"Error decoding/verifying license data: {ex.Message}", exception: ex);
            }

            if (string.IsNullOrWhiteSpace(verifiedJsonData))
            {
                return new LicenseResult(LicenseStatus.InvalidOrTampered, "Failed to retrieve valid data after decoding/verification.");
            }


            // --- 4. Parse Thông tin License ---
            LicenseInfo? licenseInfo;
            try
            {
                licenseInfo = JsonSerializer.Deserialize<LicenseInfo>(verifiedJsonData);
                if (licenseInfo == null || string.IsNullOrEmpty(licenseInfo.LicensedMacAddress))
                {
                    return new LicenseResult(LicenseStatus.InvalidOrTampered, "Invalid license structure after parsing.");
                }
            }
            catch (JsonException jsonEx)
            {
                return new LicenseResult(LicenseStatus.InvalidOrTampered, $"Error parsing license data: {jsonEx.Message}", exception: jsonEx);
            }
            catch (Exception ex)
            {
                return new LicenseResult(LicenseStatus.Error, $"Unexpected error parsing license data: {ex.Message}", exception: ex);
            }


            // --- 5. Kiểm tra MAC ---
            if (!currentMac.Equals(licenseInfo.LicensedMacAddress?.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                return new LicenseResult(
                    LicenseStatus.WrongMachine,
                    $"License is for machine [{licenseInfo.LicensedMacAddress}], but current machine is [{currentMac}].",
                    licenseInfo // Vẫn trả về thông tin license để có thể hiển thị
                );
            }

            // --- 6. Kiểm tra Thời gian ---
            if (DateTime.UtcNow > licenseInfo.ExpirationDateUtc)
            {
                return new LicenseResult(
                    LicenseStatus.Expired,
                    $"License expired on {licenseInfo.ExpirationDateUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}.",
                    licenseInfo
                );
            }

            // --- 7. Thành công ---
            return new LicenseResult(
                LicenseStatus.Valid,
                $"License is valid until {licenseInfo.ExpirationDateUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}.",
                licenseInfo
            );
        }

        public string Encrypt(string plainText)
        {
            try
            {
                if (!File.Exists(_publicKeyFilePath))
                {
                    throw new Exception($"Không tồn tại file key tại: {_publicKeyFilePath}");
                }

                string publicKeyXml = File.ReadAllText(_publicKeyFilePath, Encoding.UTF8);
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(publicKeyXml);
                    byte[] dataToEncrypt = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedData = rsa.Encrypt(dataToEncrypt, RSAEncryptionPadding.Pkcs1);
                    return Convert.ToBase64String(encryptedData);
                }
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public string Decrypt(string base64CipherText)
        {
            try
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(_privateXml);
                    byte[] encryptedData = Convert.FromBase64String(base64CipherText);
                    byte[] decryptedData = rsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);
                    return Encoding.UTF8.GetString(decryptedData);
                }
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

    }
}
