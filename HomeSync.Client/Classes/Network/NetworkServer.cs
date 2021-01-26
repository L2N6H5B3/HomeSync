using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HomeSync.Classes.Network {
    class NetworkServer {

        private string data = null;
        private Socket socket;
        private IPHostEntry ipHostInfo;
        private IPAddress ipAddress;
        private IPEndPoint localEndPoint;
        public event EventHandler<ResponseArgs> ResponseEvent;

        public NetworkServer() {
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
                    System.Diagnostics.Debug.WriteLine("ClientSocket: Socket Ready");
                    // Program is suspended while waiting for an incoming connection.  
                    Socket client = socket.Accept();
                    data = null;

                    // An incoming connection needs to be processed.  
                    while (true) {
                        int bytesRec = client.Receive(bytes);
                        data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (data.IndexOf("<EOF>") > -1) {
                            break;
                        }
                    }

                    // Show the data on the console.  
                    System.Diagnostics.Debug.WriteLine($"ClientSocket: Received: {data} from Server");

                    // Process Data
                    ProcessRequest(data, client);

                    System.Diagnostics.Debug.WriteLine("ClientSocket: Socket Closed");

                    // Close Client Socket
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }

            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }

        private void ProcessRequest(string data, Socket client) {
            // Split Message
            string[] dataArray = data.Split('|');

            // Convert OK Data
            byte[] msg = Encoding.ASCII.GetBytes("ok");
            // Send OK to Client
            client.Send(msg);

            // Create new Response Args
            ResponseArgs args = new ResponseArgs();
            // Set the Response Args Response Data
            args.responseType = dataArray[0];
            args.response = dataArray[1];
            // Raise Response Event
            ResponseEvent(this, args);
        }
    }
    
    class ResponseArgs : EventArgs {
        public string responseType;
        public string response;
    }
}

