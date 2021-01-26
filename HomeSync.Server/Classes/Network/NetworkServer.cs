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
        private bool listening = false;
        private Socket socket;
        private List<string> clients;
        private IPHostEntry ipHostInfo;
        private IPAddress ipAddress;
        private IPEndPoint localEndPoint;
        public event EventHandler<ResponseArgs> ResponseEvent;

        public NetworkServer() {
            // Establish the local endpoint for the socket.  
            ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            ipAddress = ipHostInfo.AddressList.First(xx => xx.AddressFamily == AddressFamily.InterNetwork);
            localEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));

            // Create a TCP/IP socket
            socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Create a RegisteredClients List
            clients = new List<string>();
        }

        public void Start() {
            // Data buffer for incoming data.  
            byte[] bytes = new Byte[1024];

            // Bind the socket to the local endpoint and listen for incoming connections
            try {
                socket.Bind(localEndPoint);
                socket.Listen(10);
                listening = true;

                // Start listening for connections
                while (true) {
                    System.Diagnostics.Debug.WriteLine("Waiting for a connection...");
                    // Program is suspended while waiting for an incoming connection.  
                    Socket client = socket.Accept();
                    data = null;

                    // Get Client IP Address
                    string clientAddress = (client.RemoteEndPoint as IPEndPoint).Address.ToString();


                    // If Client IP is not in Registered Clients
                    if (!clients.Contains(clientAddress)) {
                        // Add Client IP to Registered Clients
                        clients.Add(clientAddress);
                        System.Diagnostics.Debug.WriteLine($"New Client Connected: {clientAddress}");
                    } else {
                        System.Diagnostics.Debug.WriteLine($"Old Client Connected: {clientAddress}");

                    }

                    // An incoming connection needs to be processed.  
                    while (true) {
                        int bytesRec = client.Receive(bytes);
                        data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (data.IndexOf("<EOF>") > -1) {
                            break;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Received: {data} from {clientAddress}");

                    // Process Data
                    ProcessRequest(data, client);

                    System.Diagnostics.Debug.WriteLine("Client request complete, closing connection... ");

                    listening = false;
                    // Close Client Socket
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }

            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }


        public List<string> GetRegisteredClients() {
            return clients;
        }

        private void ProcessRequest(string data, Socket client) {
            // Split Message
            string[] dataArray = data.Split('|');
            // Get Client IP Address
            string clientAddress = (client.RemoteEndPoint as IPEndPoint).Address.ToString();

            // Convert OK Data
            byte[] msg = Encoding.ASCII.GetBytes("ok");
            // Send OK to Client
            client.Send(msg);

            // Create new Response Args
            ResponseArgs args = new ResponseArgs();
            // Set the Response Args Response Data
            args.responseType = dataArray[0];
            args.response = dataArray[1];
            args.clientIp = clientAddress;
            // Raise Response Event
            ResponseEvent(this, args);
        }

        public bool IsListening() {
            return listening;
        }
    }
    
    class ResponseArgs : EventArgs {
        public string responseType;
        public string response;
        public string clientIp;
    }
}

