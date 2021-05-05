using System;
using System.Management;
using System.Windows.Forms;

namespace HomeSync.Server.Classes {
    class Utilities {
        public static string TryGetLocalFromUncDirectory(string local) {
            string unc = null;
            if ((local == null) || (local == "")) {
                throw new ArgumentNullException("local");
            }

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_share WHERE path ='" + local.Replace("\\", "\\\\") + "'");
            ManagementObjectCollection coll = searcher.Get();
            if (coll.Count == 1) {
                foreach (ManagementObject share in searcher.Get()) {
                    unc = share["Name"] as String;
                    unc = "\\\\" + SystemInformation.ComputerName + "\\" + unc;
                }
            }
            return unc;
        }
    }
}
