using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net.NetworkInformation;
using System.Threading.Tasks.Dataflow;

namespace SRT2Speech.Core.Utilitys
{
    public static class EncodeUtility
    {
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
                string[] formats = { "dd/MM/yyyy hh:mm:ss tt", "MM/dd/yyyy HH:mm:ss tt" };
                string decrypt = AESEncryption.DecryptAES(key);
                string date = decrypt.Split("__")[1];
                string mac = decrypt.Split("__")[0];

                DateTime licenseTime = DateTime.Now.AddMicroseconds(-10);
                bool isParsed = false;
                foreach (string format in formats)
                {
                    if (DateTime.TryParseExact(date, format, null, System.Globalization.DateTimeStyles.None, out licenseTime))
                    {
                        isParsed = true;
                        break;
                    }
                }

                if (!isParsed)
                    return false;
                if (licenseTime <= DateTime.Now)
                    return false;

                //string m = GetMacAddress().Replace("_", "").Replace("-", "");

                //if (!m.Equals(mac, StringComparison.OrdinalIgnoreCase))
                //{
                //    return false;
                //}

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
