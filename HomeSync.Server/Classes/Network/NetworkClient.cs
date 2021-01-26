using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HomeSync.Classes.Network {
    class NetworkClient {

        private Log log;
        private string hostname;
        private Socket socket;
        private IPAddress ipAddress;
        private IPEndPoint remoteEndPoint;
        private byte[] bytes;

        public NetworkClient(string address, Log log) {

            this.log = log;

            // Data buffer for incoming data.  
            bytes = new byte[1024];

            // Connect to Device
            try {
                // Get Hostname
                hostname = Dns.GetHostName();
                // Set IP Address
                ipAddress = IPAddress.Parse(address);
                // Set Remote EndPoint
                remoteEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));

                // Create a TCP/IP socket
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try {
                    // Write to Log
                    log.WriteLine("NetworkClient: Connecting");
                    // Connect the Socket
                    socket.Connect(remoteEndPoint);
                    // Write to Log
                    log.WriteLine($"NetworkClient: Connected");
                } catch (ArgumentNullException ane) {
                    // Write to Log
                    log.WriteLine($"NetworkClient Connect: ArgumentNullException: {ane}");
                } catch (SocketException se) {
                    // Write to Log
                    log.WriteLine($"NetworkClient Connect: SocketException: {se}");
                } catch (Exception e) {
                    // Write to Log
                    log.WriteLine($"NetworkClient Connect: Unexpected Exception: {e}");
                }
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Create: Exception: {e}");
            }
        }

        public void SendResumeUpdate(string data) {
            try {
                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes($"ResumeUpdate|{data}<EOF>");
                // Send Data through Socket and Return Bytes Sent
                int sentBytes = socket.Send(msg);
                // Write to Log
                log.WriteLine($"NetworkClient: Sent ResumeUpdate (Bytes: {sentBytes})");
                // Response from Server
                string response = Receive();
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: ArgumentNullException: {ane}");
            } catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: SocketException: {se}");
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: Unexpected Exception: {e}");
            }
        }

        private string Receive() {
            string response = "";
            try {
                // Receive Bytes from Socket
                int bytesRec = socket.Receive(bytes);
                // Convert Bytes into String Response
                response = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                // Write to Log
                log.WriteLine($"NetworkClient: Received data {response}");
                // Close Socket
                Close();
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Receive: ArgumentNullException: {ane}");
            } catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient Receive: SocketException: {se}");
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Receive: Unexpected Exception: {e}");
            }
            // Return Response from Server
            return response;
        }

        private void Close() {
            try {
                // Release the socket
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                // Write to Log
                log.WriteLine("NetworkClient: Socket Closed");
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Close: ArgumentNullException: {ane}");
            } catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient Close: SocketException: {se}");
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Close: Unexpected Exception: {e}");
            }
        }
    }
}

