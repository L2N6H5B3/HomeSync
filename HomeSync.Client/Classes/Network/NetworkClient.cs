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

        private Log log;
        private Socket socket;
        private IPAddress ipAddress;
        private IPEndPoint remoteEndPoint;
        private byte[] bytes;
        private Thread HeartbeatThread;

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
                log.WriteLine($"NetworkClient: Exception: {e}");
            }
        }

        public void Connect() {
            try {
                // Set IP Address
                ipAddress = IPAddress.Parse(ConfigurationManager.AppSettings.Get("server-address"));
                // Set Remote EndPoint
                remoteEndPoint = new IPEndPoint(ipAddress, int.Parse(ConfigurationManager.AppSettings.Get("server-port")));
                // Write to Log
                log.WriteLine("Connecting to HomeSync Server");
                // Connect the Socket
                socket.Connect(remoteEndPoint);
                // Write to Log
                log.WriteLine($"Connected to HomeSync Server");
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
        }

        public void Register() {
            try {
                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes($"RegisterClient|<EOF>");
                // Send Data through Socket and Return Bytes Sent
                int sentBytes = socket.Send(msg);
                // Write to Log
                log.WriteLine($"Sent RegisterClient ({sentBytes} bytes) to HomeSync Server");
                // Response from Server
                string response = Receive();
                // Create Heartbeat
                CreateHeartbeat();
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Register: ArgumentNullException: {ane}");
            } catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient Register: SocketException: {se}");
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
                log.WriteLine($"Sent ResumeUpdate ({sentBytes} bytes) to HomeSync Server");
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
                log.WriteLine($"Received {response} from HomeSync Server");
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
                log.WriteLine("Disconnected from HomeSync Server");
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
                            // Encode the data string into a byte array.  
                            byte[] msg = Encoding.ASCII.GetBytes($"Heartbeat|<EOF>");
                            // Send Data through Socket and Return Bytes Sent
                            int sentBytes = socket.Send(msg);
                            // Write to Log
                            log.WriteLine($"Sent Heartbeat ({sentBytes} bytes) to HomeSync Server");

                            try {
                                // Receive Bytes from Socket
                                int bytesRec = socket.Receive(bytes);
                                // Convert Bytes into String Response
                                string response = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                                // Write to Log
                                log.WriteLine($"Received Heartbeat {response} from HomeSync Server");
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
                        }
                    }
                }
            });
            // Start the Heartbeat Thread
            HeartbeatThread.Start();
        }
    }
}

