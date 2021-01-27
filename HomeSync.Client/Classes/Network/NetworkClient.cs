using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HomeSync.Classes.Network {
    class NetworkClient {

        private readonly Log log;
        private readonly byte[] bytes;
        private Socket socket;
        private IPAddress ipAddress;
        private IPEndPoint remoteEndPoint;
        private Thread HeartbeatThread;
        public event EventHandler<RetryArgs> RetryEvent;
        public event EventHandler<StatusArgs> StatusEvent;
        public event EventHandler ServerConnectEvent;

        public NetworkClient(Log log) {
            // Add Log
            this.log = log;
            // Data buffer for incoming data.  
            bytes = new byte[1024];
        }

        public void Connect() {
            try {
                // Set IP Address
                ipAddress = IPAddress.Parse(ConfigurationManager.AppSettings.Get("server-address"));
                // Set Remote EndPoint
                remoteEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));
                // Create a TCP/IP socket
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                // Set current status in Form
                SetConnectionStatus("Connecting");
                // Connect the Socket
                socket.Connect(remoteEndPoint);
                // Write to Log
                log.WriteLine($"Server Connected");
                // Set current status in Form
                SetConnectionStatus("Connected");
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Connect: ArgumentNullException: {ane}");
            } catch (SocketException) {
                // Write to Log
                log.WriteLine($"Server Connection Failed");
                // Set Status
                SetConnectionStatus("Disconnected");
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Connect: Unexpected Exception: {e}");
            }
        }

        public void Register() {
            // Trigger Server Connected Updates
            ServerConnected();
            try {
                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes($"RegisterClient|<EOF>");
                // Send Data through Socket and Return Bytes Sent
                int sentBytes = socket.Send(msg);
                // Write to Log
                log.WriteLine($"Registering Client");
                // Response from Server
                string response = Receive();
                // Create Heartbeat
                CreateHeartbeat();
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Register: ArgumentNullException: {ane}");
            } catch (SocketException) {
                // Write to Log
                log.WriteLine($"Registering Client Failed");
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Register: Unexpected Exception: {e}");
            }
        }

        public void SendResumeUpdate(string data) {
            try {
                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes($"ResumeUpdate|{data}<EOF>");
                // Send Data through Socket and Return Bytes Sent
                int sentBytes = socket.Send(msg);
                // Write to Log
                log.WriteLine($"Sending Resume Update");
                // Response from Server
                string response = Receive();
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: ArgumentNullException: {ane}");
            } catch (SocketException) {
                // Write to Log
                log.WriteLine($"Sending Resume Update Failed");
                // Create new Response Args
                RetryArgs args = new RetryArgs {
                    // Set the StatusArgs Response Data
                    data = data
                };
                // Write to Log
                log.WriteLine($"Unable to contact Server, will retry later");
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

        public bool IsConnected() {
            return socket.Connected;
        }

        private void CreateHeartbeat() {
            // Create Heartbeat Thread
            HeartbeatThread = new Thread(() => {
                // Set Thread to Background
                Thread.CurrentThread.IsBackground = true;
                // While True
                while (true) {
                    // Sleep the Thread for one minute
                    Thread.Sleep(60000);
                    // If the socket is disconnected and not in use
                    if (!socket.Connected) {
                        // Connect the socket
                        Connect();
                        // If the socket is connected
                        if (socket.Connected) {
                            // Set Status
                            SetConnectionStatus("Ready");
                            try {
                                // Encode the data string into a byte array.  
                                byte[] msg = Encoding.ASCII.GetBytes($"Heartbeat|<EOF>");
                                // Send Data through Socket and Return Bytes Sent
                                int sentBytes = socket.Send(msg);
                                // Write to Log
                                log.WriteLine($"Sent Heartbeat");
                                // Receive Bytes from Socket
                                int bytesRec = socket.Receive(bytes);
                                // Convert Bytes into String Response
                                string response = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                                // Close Socket
                                Close();
                            } catch (ArgumentNullException ane) {
                                // Write to Log
                                log.WriteLine($"NetworkClient Receive: ArgumentNullException: {ane}");
                            } catch (SocketException) {
                                // Write to Log
                                log.WriteLine("Sending Heartbeat Failed");
                            } catch (Exception e) {
                                // Write to Log
                                log.WriteLine($"NetworkClient Receive: Unexpected Exception: {e}");
                            }
                        }
                    }
                }
            });
            // Start the Heartbeat Thread
            HeartbeatThread.Start();
        }
        private void SetConnectionStatus(string status) {
            // Create new Response Args
            StatusArgs args = new StatusArgs();
            // Set the StatusArgs Response Data
            args.status = status;
            // Raise Response Event
            StatusEvent(this, args);
        }

        private void ServerConnected() {
            // Create new ServerConnectArgs
            ServerConnectArgs args = new ServerConnectArgs();
            // Set the ResponseArgs Response Data
            args.serverAvailable = true;
            // Raise Response Event
            ServerConnectEvent(this, args);
        }
    }

    class RetryArgs : EventArgs {
        public string data;
    }

    class StatusArgs : EventArgs {
        public string status;
    }

    class ServerConnectArgs : EventArgs {
        public bool serverAvailable;
    }
}

