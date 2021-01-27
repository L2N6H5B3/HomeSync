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
        private string clientAddress;
        private Socket socket;
        private IPAddress ipAddress;
        private IPEndPoint remoteEndPoint;
        private byte[] bytes;
        public event EventHandler<RetryArgs> RetryEvent;

        public NetworkClient(string clientAddress, Log log) {
            // Add Client IP Address
            this.clientAddress = clientAddress;
            // Add Log
            this.log = log;

            // Data buffer for incoming data.  
            bytes = new byte[1024];

            // Connect to Device
            try {
                // Get Hostname
                hostname = Dns.GetHostName();
                // Set IP Address
                ipAddress = IPAddress.Parse(clientAddress);
                // Set Remote EndPoint
                remoteEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));

                // Create a TCP/IP socket
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try {
                    // Connect the Socket
                    socket.Connect(remoteEndPoint);
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
                log.WriteLine($"Sent ResumeUpdate ({sentBytes} bytes) to Client ({clientAddress})");
                // Response from Server
                string response = Receive();
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: ArgumentNullException: {ane}");
            } catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: SocketException: {se}");
                // Create new Response Args
                RetryArgs args = new RetryArgs();
                // Set the StatusArgs Response Data
                args.clientAddress = clientAddress;
                args.data = data;
                // Write to Log
                log.WriteLine($"Unable to contact Client ({clientAddress}), will retry later");
                // Raise Response Event
                RetryEvent(this, args);
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

    class RetryArgs : EventArgs {
        public string clientAddress;
        public string data;
    }
}

