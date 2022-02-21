using HomeSync.Agent.Classes;
using HomeSync.Classes.Recording;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HomeSync.Classes.Network.Client {
    class NetworkClient {

        private readonly Log log;
        private readonly string destAddress;
        private readonly int destPort;
        private readonly string passkey;
        public event EventHandler<RetryArgs> RetryEvent;
        public event EventHandler<StatusArgs> StatusEvent;

        public NetworkClient(string ip, int port, string passkey, Log log) {
            // Add Destination IP Info
            this.destAddress = ip;
            this.destPort = port;
            // Add Shared Passkey
            this.passkey = passkey;
            // Add Log
            this.log = log;
        }

        public RecordingsJson SendRequest(ClientRequest clientRequest, string data = null) {

            // Try and Catch Exceptions
            try {

                #region Connect ###############################################

                // Create TCP Client
                TcpClient client = new TcpClient(destAddress, destPort);
                // Get Network Stream
                NetworkStream stream = client.GetStream();

                #endregion ####################################################

                #region Authenticate ##########################################

                // Convert Message to Byte Array
                byte[] data = Encoding.ASCII.GetBytes(GetRequestData(ClientRequest.Authenticate, passkey));
                // Send Data to the Server
                stream.Write(data, 0, data.Length);

                // Create Buffer for Received Data
                data = new byte[256];
                // Read Data from Server
                int bytes = stream.Read(data, 0, data.Length);
                // Get Response Data
                NetworkServerResponse response = GetResponseData(Encoding.ASCII.GetString(data, 0, bytes));

                #endregion ####################################################

                #region Send Request ##########################################

                // If the Response is OK
                if (response.data == "ok") {
                    // Convert Message to Byte Array
                    data = Encoding.ASCII.GetBytes(GetRequestData(clientRequest, data));
                    // Send Data to the Server
                    stream.Write(data, 0, data.Length);

                    // Create Buffer for Received Data
                    data = new byte[256];
                    // Read Data from Server
                    bytes = stream.Read(data, 0, data.Length);
                    // Get Response Data
                    response = GetResponseData(Encoding.ASCII.GetString(data, 0, bytes));
                    // If the Response is OK
                    if (response.data == "ok") {
                        // Return the RecordingsJson
                        return JsonConvert.DeserializeObject<RecordingsJson>(response.data);
                    }
                }

                #endregion ####################################################

                #region Close Connection ######################################

                // Close Stream
                stream.Close();
                // Close Client
                client.Close();

                #endregion ####################################################

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

            return null;
        }

        private string GetRequestData(ClientRequest clientRequest, dynamic data) {
            // Create new Network Client Request
            NetworkClientRequest request = new NetworkClientRequest { intent = clientRequest.ToString(), data = data };
            // Serialise and Return the Requests
            return JsonConvert.SerializeObject(request);
        }

        private NetworkServerResponse GetResponseData(string data) {
            // Deserialise and Return the Network Server Response Data
            return JsonConvert.DeserializeObject<NetworkServerResponse>(data);
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
    }

    class RetryArgs : EventArgs {
        public string data;
    }

    class StatusArgs : EventArgs {
        public string status;
    }
}