﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HomeSync.Classes.Network {
    class NetworkClient {

        private Socket socket;
        private IPAddress ipAddress;
        private IPEndPoint remoteEndPoint;
        private byte[] bytes;

        public NetworkClient() {

            // Data buffer for incoming data.  
            bytes = new byte[1024];

            // Connect to Device
            try {
                // Set IP Address
                ipAddress = IPAddress.Parse(ConfigurationManager.AppSettings.Get("server-address"));
                // Create a TCP/IP socket
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
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

                System.Diagnostics.Debug.WriteLine($"ServerSocket: Socket Connected");

            } catch (ArgumentNullException ane) {
                System.Diagnostics.Debug.WriteLine($"ServerSocket: ArgumentNullException: {ane}");
            } catch (SocketException se) {
                System.Diagnostics.Debug.WriteLine($"ServerSocket: SocketException: {se}");
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine($"ServerSocket: Unexpected exception: {e}");
            }
        }

        public void Register() {
            // Encode the data string into a byte array.  
            byte[] msg = Encoding.ASCII.GetBytes($"RegisterClient|<EOF>");
            // Send Data through Socket and Return Bytes Sent
            int sentBytes = socket.Send(msg);
            // Response from Server
            string response = Receive();
        }

        public string Send(string data) {
            // Encode the data string into a byte array.  
            byte[] msg = Encoding.ASCII.GetBytes($"{data}<EOF>");
            // Send Data through Socket and Return Bytes Sent
            int sentBytes = socket.Send(msg);
            // Return the response from Server
            return Receive();
        }

        public void SendResumeUpdate(string data) {
            // Encode the data string into a byte array.  
            byte[] msg = Encoding.ASCII.GetBytes($"ResumeUpdate|{data}<EOF>");
            // Send Data through Socket and Return Bytes Sent
            int sentBytes = socket.Send(msg);

            System.Diagnostics.Debug.WriteLine($"ServerSocket: Server Sent {sentBytes} Bytes");

            // Response from Server
            string response = Receive();
        }

        private string Receive() {
            // Receive Bytes from Socket
            int bytesRec = socket.Receive(bytes);
            // Convert Bytes into String Response
            string response = Encoding.ASCII.GetString(bytes, 0, bytesRec);

            System.Diagnostics.Debug.WriteLine($"ServerSocket: Response: {response}");

            // Close Socket
            Close();

            // Return Response
            return response;
        }

        private void Close() {
            // Release the socket.
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            System.Diagnostics.Debug.WriteLine($"ServerSocket: Socket Closed");
        }

        public bool IsConnected() {
            return socket.Connected;
        }
    }
}

