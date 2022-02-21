using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HomeSync.Classes.Network {
    class NetworkDiscover {

        // Port to Discover
        private readonly int port;
        private List<string> activeHosts;

        public NetworkDiscover(string port) {
            // Set the Port
            this.port = int.Parse(port);
            // Set the ActiveHosts List
            this.activeHosts = new List<string>();
        }

        public List<string> GetActiveHosts() {
            // Return new List of Active Hosts
            return new List<string>(activeHosts);
        }

        public void ScanLocalNetwork() {
            // Check if connected to a network
            if (NetworkInterface.GetIsNetworkAvailable()) {
                // Get local IP Address
                IPAddress ipAddress = GetLocalIPAddress();
                // Get local IP Subnet Mask
                IPAddress ipMask = GetLocalIPSubnetMask(ipAddress);
                // Get all local Subnet IP Addresses
                List<string> localSubnetIpAddresses = GetLocalSubnetIPAddresses(ipAddress, ipMask);

                TimeSpan timeSpan = new TimeSpan(0, 0, 0, 0, 100);

                // Iterate through all local Subnet IP Addresses
                foreach (string ipString in localSubnetIpAddresses) {
                    // Try and Catch any Problems
                    try {
                        // !! NEED TO ADD PASSKEY VERIFICATION HERE - CONFIRM THAT CONNECTION IS SUCCESSFUL !!
                        using (var client = new TcpClient()) {
                            var result = client.ConnectAsync(ipString, port);
                            var success = result.Wait(timeSpan);
                            client.EndConnect(result);
                            // Add the Host
                            activeHosts.Add(ipString);
                        }
                    } catch { }
                }
            }
        }

        private static IPAddress GetLocalIPAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public static IPAddress GetLocalIPSubnetMask(IPAddress address) {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses) {
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork) {
                        if (address.Equals(unicastIPAddressInformation.Address)) {
                            return unicastIPAddressInformation.IPv4Mask;
                        }
                    }
                }
            }
            throw new ArgumentException(string.Format("Can't find subnetmask for IP address '{0}'", address));
        }

        public static List<string> GetLocalSubnetIPAddresses(IPAddress ip, IPAddress mask) {
            // Create List to hold Local Addresses
            List<string> addresses = new List<string>();
            //// Split the Netmask
            //List<string> maskSplit = mask.ToString().Split('.').ToList();
            //// Iterate through each Netmask Portion
            //foreach (string maskPortion in maskSplit) {
            //    // Convert the Netmask Portion to Integer
            //    int maskPortionInt = int.Parse(maskPortion);
            //    // If the Netmask Portion is less than 255
            //    if (maskPortionInt < 255) {
            //        System.Diagnostics.Debug.WriteLine("here");
            //    }
            //}

            // Get Network Address
            IPAddress networkAddress = ip.GetNetworkAddress(mask);
            // Get Broadcast Address
            IPAddress broadcastAddress = ip.GetBroadcastAddress(mask);

            string[] networkAddressSplit = networkAddress.ToString().Split('.').Reverse().ToArray();
            string[] broadcastAddressSplit = broadcastAddress.ToString().Split('.').Reverse().ToArray();

            // FINISH THIS LATER !!

            string[] ipAddressSplit = ip.ToString().Split('.');
            for (int i = 1; i < 255; i++) {
                string current = $"{ipAddressSplit[0]}.{ipAddressSplit[1]}.{ipAddressSplit[2]}.{i}";
                if (current != ip.ToString()) {
                    addresses.Add(current);
                }
            }

            // Return the List of Local Addresses
            return addresses;
        }

        private bool IsPortOpen(string host, int port, TimeSpan timeout) {
            try {
                using (var client = new TcpClient()) {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(timeout);
                    client.EndConnect(result);
                    return success;
                }
            }
            catch {
                return false;
            }
        }

    }



    #region IP Address Extensions #############################################

    public static class IPAddressExtensions {
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask) {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++) {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        public static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask) {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++) {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
            }
            return new IPAddress(broadcastAddress);
        }

        public static bool IsInSameSubnet(this IPAddress address2, IPAddress address, IPAddress subnetMask) {
            IPAddress network1 = address.GetNetworkAddress(subnetMask);
            IPAddress network2 = address2.GetNetworkAddress(subnetMask);

            return network1.Equals(network2);
        }
    }

    #endregion ################################################################

    public static class SubnetMask {
        public static readonly IPAddress ClassA = IPAddress.Parse("255.0.0.0");
        public static readonly IPAddress ClassB = IPAddress.Parse("255.255.0.0");
        public static readonly IPAddress ClassC = IPAddress.Parse("255.255.255.0");

        public static IPAddress CreateByHostBitLength(int hostpartLength) {
            int hostPartLength = hostpartLength;
            int netPartLength = 32 - hostPartLength;

            if (netPartLength < 2)
                throw new ArgumentException("Number of hosts is to large for IPv4");

            Byte[] binaryMask = new byte[4];

            for (int i = 0; i < 4; i++) {
                if (i * 8 + 8 <= netPartLength)
                    binaryMask[i] = (byte)255;
                else if (i * 8 > netPartLength)
                    binaryMask[i] = (byte)0;
                else {
                    int oneLength = netPartLength - i * 8;
                    string binaryDigit =
                        String.Empty.PadLeft(oneLength, '1').PadRight(8, '0');
                    binaryMask[i] = Convert.ToByte(binaryDigit, 2);
                }
            }
            return new IPAddress(binaryMask);
        }

        public static IPAddress CreateByNetBitLength(int netpartLength) {
            int hostPartLength = 32 - netpartLength;
            return CreateByHostBitLength(hostPartLength);
        }

        public static IPAddress CreateByHostNumber(int numberOfHosts) {
            int maxNumber = numberOfHosts + 1;

            string b = Convert.ToString(maxNumber, 2);

            return CreateByHostBitLength(b.Length);
        }
    }
}
