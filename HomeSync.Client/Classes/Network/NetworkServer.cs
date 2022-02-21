using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HomeSync.Classes.Network.Server {
    class NetworkServer {

        private string passkey;
        private string data = null;
        private readonly Log log;
        private readonly Socket socket;
        private readonly IPHostEntry ipHostInfo;
        private readonly IPAddress ipAddress;
        private readonly IPEndPoint localEndPoint;
        public event EventHandler<ResponseArgs> ResponseEvent;
        public event EventHandler<StatusArgs> StatusEvent;
        public event EventHandler<HeartbeatArgs> HeartbeatEvent;

        public NetworkServer(string passkey, Log log) {
            // Set PassKey
            this.passkey = passkey;
            // Set Log
            this.log = log;

            // Establish the local endpoint for the socket.  
            ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            ipAddress = ipHostInfo.AddressList.First(xx => xx.AddressFamily == AddressFamily.InterNetwork);
            localEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));

            // Create a TCP/IP socket
            socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start() {
            // Data buffer for incoming data.  
            byte[] bytes = new Byte[1024];

            // Bind the socket to the local endpoint and listen for incoming connections
            try {
                socket.Bind(localEndPoint);
                socket.Listen(10);

                // Start listening for connections
                while (true) {
                    // Write to Log
                    log.WriteLine($"Ready for connection by other agents");
                    // Set Status
                    RefreshServerStatus("Ready");
                    // Program is suspended while waiting for an incoming connection
                    Socket client = socket.Accept();
                    data = null;

                    // Get Client IP Address
                    string clientAddress = (client.RemoteEndPoint as IPEndPoint).Address.ToString();
                    // Write to Log
                    log.WriteLine($"Client ({clientAddress}) Connected ");
                    // Set Status
                    RefreshServerStatus("Agent Downloading");

                    // An incoming connection needs to be processed
                    while (true) {
                        int bytesRec = client.Receive(bytes);
                        data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (data.IndexOf("<EOF>") > -1) {
                            break;
                        }
                    }

                    // Remove <EOF> from the Request
                    data = data.Replace("<EOF>", "");
                    // Deserialise the request
                    NetworkClientRequest request = JsonConvert.DeserializeObject<NetworkClientRequest>(data);

                    // If the client is part of this pool of agents
                    if (request.passkey == passkey) {
                        // Send client data as
                        switch (request.intent) {
                            case "sendAll":
                                GetData("");
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

                    // If the Client Intent is not Heartbeat
                    if (clientIntent != "Heartbeat") {
                        // Process Data
                        ProcessRequest(data, clientAddress);
                    } else {
                        // Create new HeartbeatArgs
                        HeartbeatArgs args = new HeartbeatArgs {
                            // Set the ResponseArgs Response Data
                            clientIp = clientAddress
                        };
                        // Raise Response Event
                        HeartbeatEvent(this, args);
                    }
                }
            }
            catch (Exception e) {
                // Write to Log
                log.WriteLine($"Unexpected Exception: {e}");
                // Set Status
                RefreshServerStatus("Stopped");
            }
        }

        public void UpdateAuthenticationKey(string passkey) {
            // Set Updated PassKey
            this.passkey = passkey;
        }
        
        private string GetData(string requestType) {
            // Create new ResponseArgs
            ResponseArgs args = new ResponseArgs {
                // Set the ResponseArgs Response Data
                requestType = requestType
            };
            // Return the ResponseData from the Event
            return args.responseData;
        }

        private void ProcessRequest(string data, string clientAddress) {
            // Split Message
            string[] dataArray = data.Split('|');

            // Create new ResponseArgs
            ResponseArgs args = new ResponseArgs {
                // Set the ResponseArgs Response Data
                requestType = dataArray[0],
                response = dataArray[1],
                clientIp = clientAddress
            };
            // Raise Response Event
            var result = ResponseEvent(this, args);
        }

        private void RefreshServerStatus(string status) {
            // Create new Response Args
            StatusArgs args = new StatusArgs {
                // Set the StatusArgs Response Data
                status = status
            };
            // Raise Response Event
            StatusEvent(this, args);
        }
    }
    
    class ResponseArgs : EventArgs {
        public string requestType;
        public string responseData;
    }

    class StatusArgs : EventArgs {
        public string status;
    }

    class HeartbeatArgs : EventArgs {
        public string clientIp;
    }
}

