﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HomeSync.Classes.Network {
    class NetworkServer {

        private string data = null;
        private readonly Log log;
        private readonly Socket socket;
        private readonly IPHostEntry ipHostInfo;
        private readonly IPAddress ipAddress;
        private readonly IPEndPoint localEndPoint;
        public event EventHandler<ResponseArgs> ResponseEvent;

        public NetworkServer(Log log) {

            this.log = log;

            // Establish the local endpoint for the socket.  
            ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            ipAddress = ipHostInfo.AddressList.First(xx => xx.AddressFamily == AddressFamily.InterNetwork);
            localEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));

            // Create a TCP/IP socket.  
            socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start() {
            // Data buffer for incoming data 
            byte[] bytes = new Byte[1024];

            // Bind the socket to the local endpoint and listen for incoming connections
            try {
                socket.Bind(localEndPoint);
                socket.Listen(10);

                // Start listening for connections
                while (true) {
                    // Program is suspended while waiting for an incoming connection
                    Socket client = socket.Accept();
                    data = null;
                    // Get Client IP Address
                    string clientAddress = (client.RemoteEndPoint as IPEndPoint).Address.ToString();

                    // An incoming connection needs to be processed
                    while (true) {
                        int bytesRec = client.Receive(bytes);
                        data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (data.IndexOf("<EOF>") > -1) {
                            break;
                        }
                    }

                    // Convert OK Data
                    byte[] msg = Encoding.ASCII.GetBytes("OK");
                    // Send OK to Client
                    client.Send(msg);

                    // Close Client Socket
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();

                    // Process Data
                    ProcessRequest(data);
                }
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"Unexpected Exception: {e}");
            }
        }

        private void ProcessRequest(string data) {
            // Split Message
            string[] dataArray = data.Split('|');

            // Create new ResponseArgs
            ResponseArgs args = new ResponseArgs {
                // Set the ResponseArgs Response Data
                responseType = dataArray[0],
                response = dataArray[1]
            };
            // Raise Response Event
            ResponseEvent(this, args);
        }
    }
    
    class ResponseArgs : EventArgs {
        public string responseType;
        public string response;
    }
}

