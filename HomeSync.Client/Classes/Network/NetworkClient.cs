using HomeSync.Classes.Recording;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HomeSync.Classes.Network.Client {
    class NetworkClient {

        private readonly Log log;
        private readonly string destAddress;
        private readonly int destPort;
        private byte[] bytes;
        private IPAddress ipAddress;
        private IPEndPoint remoteEndPoint;
        private Socket socket;
        private Thread HeartbeatThread;
        public event EventHandler<RetryArgs> RetryEvent;
        public event EventHandler<StatusArgs> StatusEvent;

        public NetworkClient(string ip, int port, Log log) {
            // Add Destination IP Address
            this.destAddress = ip;
            this.destPort = port;
            // Add Log
            this.log = log;

            // Data buffer for incoming data.  
            bytes = new byte[1024];

            // Connect to Device
            try {
                // Set IP Address
                ipAddress = IPAddress.Parse(clientAddress);
                // Set Remote EndPoint
                remoteEndPoint = new IPEndPoint(ipAddress, clientPort);

                // Create a TCP/IP socket
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try {
                    // Connect the Socket
                    socket.Connect(remoteEndPoint);
                }
                catch (ArgumentNullException ane) {
                    // Write to Log
                    log.WriteLine($"NetworkClient Connect: ArgumentNullException: {ane}");
                }
                catch (SocketException) {
                    // Write to Log
                    log.WriteLine($"Client Unreachable");
                }
                catch (Exception e) {
                    // Write to Log
                    log.WriteLine($"NetworkClient Connect: Unexpected Exception: {e}");
                }
            }
            catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Create: Exception: {e}");
            }
        }

        public bool IsConnected() {
            return socket.Connected;
        }

        public void Connect(Retry retry) {
            // Set IP Address
            ipAddress = IPAddress.Parse(ConfigurationManager.AppSettings.Get("server-address"));
            // Set Remote EndPoint
            remoteEndPoint = new IPEndPoint(ipAddress, destPort);

            // Catch Exceptions
            try {
                // Create Data Buffer
                bytes = new byte[1024];
                // Create a TCP/IP socket
                socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                // Set current status in Form
                SetConnectionStatus("Connecting");
                // Connect the Socket
                socket.Connect(remoteEndPoint);
                // Set current status in Form
                SetConnectionStatus("Connected");
                // Check for Retries
                if (retry.recordingEntries.Count > 0) {
                    // Write to Log
                    log.WriteLine("Retry Sending Resume Updates");
                    // Retry Send
                    RetrySend(retry);
                }
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Connect: ArgumentNullException: {ane}");
            } catch (SocketException) {
                // Write to Log
                log.WriteLine($"Server Unreachable");
                // Set current status in Form
                SetConnectionStatus("Disconnected");
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Connect: Unexpected Exception: {e}");
            }
        }

        public void RetrySend(Retry retry) {
            // Create RecordingsJson String
            string recordingsJsonString = JsonConvert.SerializeObject(new RecordingsJson { recordingEntries = retry.recordingEntries });
            // Reset Retry RecordingEntry List
            retry.recordingEntries = new List<RecordingEntry>();

            // Set current status in Form
            SetConnectionStatus("Syncing resume position");

            // Send Resume Request to Server
            SendResumeUpdate(recordingsJsonString);

            // Connect Client Ready for RegisterClient
            Connect(retry);
            // Continue to Attempt to connect Client
            while (!IsConnected()) {
                Thread.Sleep(10000);
                Connect(retry);
            }
        }

        public void Register() {
            try {
                // Send Request
                string response = SendData(socket, "RegisterClient");
                // Write to Log
                log.WriteLine($"Registered Client");
                // Close Socket
                CloseSocket();
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

        public bool SendResumeUpdate(string data) {
            try {
                // Send Request
                string response = SendData(socket, "ResumeUpdate", data);
                // Write to Log
                log.WriteLine($"Sent Resume Update");
                // Close Socket
                CloseSocket();
            } catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: ArgumentNullException: {ane}");
            } catch (SocketException) {
                // Write to Log
                log.WriteLine($"Sending Resume Update Failed");
                // Create new RetryArgs
                RetryArgs args = new RetryArgs {
                    // Set the RetryArgs Response Data
                    data = data
                };
                // Write to Log
                log.WriteLine($"Unable to contact Server, will retry later");
                // Raise Response Event
                RetryEvent(this, args);
                // Return False to Indicate Failure
                return false;
            } catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: Unexpected Exception: {e}");
            }
            // Return True to Indicate Success
            return true;
        }

        private void CloseSocket() {
            // Close Socket
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        private string SendData(Socket socket, string intent, string data = "") {
            // Encode the data string into a byte array.  
            byte[] msg = Encoding.ASCII.GetBytes($"{intent}|{data}<EOF>");
            // Send Data through Socket and Return Bytes Sent
            socket.Send(msg);

            // Receive Bytes from Socket
            int bytesRec = socket.Receive(bytes);
            // Convert Bytes into String Response
            string response = Encoding.ASCII.GetString(bytes, 0, bytesRec);

            // Return Response
            return response;
        }


        public void CreateHeartbeat(Retry retry) {
            // Create Heartbeat Thread
            HeartbeatThread = new Thread(() => {
                // Set Thread to Background
                Thread.CurrentThread.IsBackground = true;
                // While True
                while (true) {
                    // Sleep the Thread for one minute
                    Thread.Sleep(60000);

                    // Connect to Server
                    Connect(retry);

                    try {
                        // Send Heartbeat
                        string response = SendData(socket, "Heartbeat");

                        // Close Socket
                        CloseSocket();
                    } catch (ArgumentNullException ane) {
                        // Write to Log
                        log.WriteLine($"NetworkClient Receive: ArgumentNullException: {ane}");
                    } catch (SocketException) {
                        // Write to Log
                        log.WriteLine("Server Unreachable");
                    } catch (Exception e) {
                        // Write to Log
                        log.WriteLine($"NetworkClient Receive: Unexpected Exception: {e}");
                    }
                    
                }
            });
            // Start the Heartbeat Thread
            HeartbeatThread.Start();
        }

        private void SetConnectionStatus(string status) {
            // Create new Response Args
            StatusArgs args = new StatusArgs {
                // Set the StatusArgs Response Data
                status = status
            };
            // Raise Response Event
            StatusEvent(this, args);
        }

        public bool SendResumeUpdate(string data) {
            try {
                // Encode the data string into a byte array.  
                byte[] msg = Encoding.ASCII.GetBytes($"ResumeUpdate|{data}<EOF>");
                // Send Data through Socket and Return Bytes Sent
                int sentBytes = socket.Send(msg);
                // Write to Log
                log.WriteLine($"Sent ResumeUpdate ({sentBytes} bytes) to Client ({destAddress})");
                // Response from Server
                string response = Receive();
            }
            catch (SocketException) {
                // Write to Log
                log.WriteLine($"Sending Resume Update Failed");

                // Create new Response Args
                RetryArgs args = new RetryArgs {
                    // Set the StatusArgs Response Data
                    clientAddress = destAddress,
                    data = data
                };

                // Write to Log
                log.WriteLine($"Unable to contact Client ({destAddress}), will retry later");
                // Raise Response Event
                RetryEvent(this, args);
                // Return False to Indicate Failure
                return false;
            }
            catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient SendResumeUpdate: Unexpected Exception: {e}");
            }
            // Return True to Indicate Success
            return true;
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
            }
            catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Receive: ArgumentNullException: {ane}");
            }
            catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient Receive: SocketException: {se}");
            }
            catch (Exception e) {
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
            }
            catch (ArgumentNullException ane) {
                // Write to Log
                log.WriteLine($"NetworkClient Close: ArgumentNullException: {ane}");
            }
            catch (SocketException se) {
                // Write to Log
                log.WriteLine($"NetworkClient Close: SocketException: {se}");
            }
            catch (Exception e) {
                // Write to Log
                log.WriteLine($"NetworkClient Close: Unexpected Exception: {e}");
            }
        }

    }

    class RetryArgs : EventArgs {
        public string data;
    }

    class StatusArgs : EventArgs {
        public string status;
    }
}