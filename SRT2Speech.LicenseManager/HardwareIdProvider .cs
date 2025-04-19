using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.LicenseManager
{
    /// <summary>
    /// Provides functionality to retrieve hardware identifiers (e.g., MAC address).
    /// Internal class, as the main service will expose the necessary method.
    /// </summary>
    public static class HardwareIdProvider // Hoặc public nếu muốn ứng dụng gọi trực tiếp
    {
        /// <summary>
        /// Attempts to get the primary MAC address of the machine.
        /// </summary>
        /// <returns>The MAC address in AA-BB-CC-DD-EE-FF format, or null if an error occurs or no suitable interface is found.</returns>
        public static string? GetPrimaryMacAddress()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) && // Ưu tiên Ethernet/Wifi
                                 ni.GetPhysicalAddress()?.ToString() != "" && // Phải có địa chỉ vật lý
                                 !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) && // Loại bỏ các card ảo cơ bản
                                 !ni.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase));

                // Sắp xếp: Ưu tiên Ethernet, sau đó là Wifi, sau đó sắp xếp theo tốc độ (cao -> thấp) hoặc mô tả
                var bestInterface = networkInterfaces
                                    .OrderBy(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet) // Ethernet first
                                    .ThenByDescending(ni => ni.Speed) // Then by speed
                                    .FirstOrDefault();


                if (bestInterface != null)
                {
                    PhysicalAddress address = bestInterface.GetPhysicalAddress();
                    byte[] bytes = address.GetAddressBytes();
                    // Trả về định dạng chuẩn AA-BB-CC-DD-EE-FF
                    return string.Join("-", bytes.Select(b => b.ToString("X2")));
                }
                else
                {
                    // Log: "No suitable active network interface found."
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Log exception: $"Error getting MAC address: {ex.Message}"
                Console.WriteLine($"Error getting MAC address: {ex.Message}"); // Log tạm thời
                return null;
            }
        }
    }
}
