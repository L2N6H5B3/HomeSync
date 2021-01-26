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
        private Socket socket;
        private IPAddress ipAddress;
        private IPEndPoint remoteEndPoint;
        private byte[] bytes;

        public NetworkClient(Log log) {

            this.log = log;

            // Data buffer for incoming data.  
            bytes = new byte[1024];

            // Connect to Device
            try {
                // Set IP Address
                ipAddress = IPAddress.Parse(ConfigurationManager.AppSettings.Get("server-address"));
                // Create a TCP/IP socket
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Exception: {e}");
            }
        }

        public void Connect() {
            try {
                // Set IP Address
                ipAddress = IPAddress.Parse(ConfigurationManager.AppSettings.Get("server-address"));
                // Set Remote EndPoint
                remoteEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));
                // Connect the Socket
                socket.Connect(remoteEndPoint);

                System.Diagnostics.Debug.WriteLine("ServerSocket: Socket Connected");

            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient ArgumentNullException: {ane}");
            } catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient SocketException: {se}");
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Unexpected Exception: {e}");
            }
        }

        public void Register() {
            // Encode the data string into a byte array.  
            byte[] msg = Encoding.ASCII.GetBytes($"RegisterClient|<EOF>");
            // Send Data through Socket and Return Bytes Sent
            int sentBytes = socket.Send(msg);

            // Write to Log
            log.WriteLine($"NetworkClient Sent RegisterClient (Bytes: {sentBytes})");

            // Response from Server
            string response = Receive();
        }

        //public string Send(string data) {
        //    // Encode the data string into a byte array.  
        //    byte[] msg = Encoding.ASCII.GetBytes($"{data}<EOF>");
        //    // Send Data through Socket and Return Bytes Sent
        //    int sentBytes = socket.Send(msg);
        //    // Return the response from Server
        //    return Receive();
        //}

        public void SendResumeUpdate(string data) {
            // Encode the data string into a byte array.  
            byte[] msg = Encoding.ASCII.GetBytes($"ResumeUpdate|{data}<EOF>");
            // Send Data through Socket and Return Bytes Sent
            int sentBytes = socket.Send(msg);

            // Write to Log
            log.WriteLine($"NetworkClient Sent ResumeUpdate (Bytes: {sentBytes})");

            // Response from Server
            string response = Receive();
        }

        private string Receive() {
            // Receive Bytes from Socket
            int bytesRec = socket.Receive(bytes);
            // Convert Bytes into String Response
            string response = Encoding.ASCII.GetString(bytes, 0, bytesRec);

            // Write to Log
            log.WriteLine($"NetworkClient Received: {response}");

            // Close Socket
            Close();

            // Return Response
            return response;
        }

        private void Close() {
            // Release the socket.
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            // Write to Log
            log.WriteLine("NetworkClient Socket Closed");
        }

        public bool IsConnected() {
            return socket.Connected;
        }
    }
}

