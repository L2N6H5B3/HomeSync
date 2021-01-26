using Microsoft.MediaCenter.Pvr;
using Microsoft.MediaCenter.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using HomeSync.Classes.Recording;
using HomeSync.Classes.Network;
using System.Threading;

namespace HomeSync.Server {


    static class Program {

        private static ObjectStore TVstore;
        private static Library TVlibrary;
        private static NetworkServer server;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {

            #region Open WMC ObjectStore ######################################

            string s = "Unable upgrade recording state.";
            byte[] bytes = Convert.FromBase64String("FAAODBUITwADRicSARc=");

            byte[] buffer2 = Encoding.ASCII.GetBytes(s);
            for (int i = 0; i != bytes.Length; i++) {
                bytes[i] = (byte)(bytes[i] ^ buffer2[i]);
            }

            string clientId = ObjectStore.GetClientId(true);
            SHA256Managed managed = new SHA256Managed();
            byte[] buffer = Encoding.Unicode.GetBytes(clientId);
            clientId = Convert.ToBase64String(managed.ComputeHash(buffer));
            string FriendlyName = Encoding.ASCII.GetString(bytes);
            string DisplayName = clientId;
            // Get TVStore
            TVstore = ObjectStore.Open("", FriendlyName, DisplayName, true);
            // Create TVLibrary
            TVlibrary = new Library(TVstore, true, false);
            // Add Store Listener
            TVstore.StoredObjectUpdated += TVstore_StoredObjectUpdated;

            #endregion ########################################################


            #region Start Server ##############################################

            new Thread(() => {
                // Set Thread to Background
                Thread.CurrentThread.IsBackground = true;

                server = new NetworkServer();
                server.ResponseEvent += Server_ResponseEvent;
                server.Start();

            }).Start();

            #endregion ########################################################


            #region Run Application ###########################################

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Settings());

            #endregion ########################################################

        }

        #region Event Handlers ################################################

        private static void TVstore_StoredObjectUpdated(object sender, StoredObjectEventArgs e) {
            // Get list of Recordings
            var libraryRecordings = TVlibrary.Recordings;
            // Check if Object ID matches a Recording
            var recording = libraryRecordings.FirstOrDefault(xx => xx.Id == e.ObjectId);
            // If Recording Exists
            if (recording != null) {
                System.Diagnostics.Debug.WriteLine("Updating Recording Resume Time");
                // Send the Resume Point Update
                SendResumeUpdate(recording);
            }
        }

        private static void Server_ResponseEvent(object sender, ResponseArgs e) {
            switch (e.responseType) {
                case "RegisterClient":
                    // Send All Resume Points to Client
                    SendAllResumePoints(e.clientIp);
                    break;
                case "ResumeUpdate":
                    // Receive the Resume Update
                    ReceiveResumeUpdate(JsonConvert.DeserializeObject<RecordingsJson>(e.response.Replace("<EOF>","")));
                    // Distribute the Resume Update to all Clients
                    DistributeResumeUpdate(e.response, e.clientIp);
                    break;
            }

        }

        #endregion ############################################################


        #region Resume Update #################################################

        // Send all Resume Points to a new Client
        private static void SendAllResumePoints(string clientIp) {
            // Get list of Recordings
            var libraryRecordings = TVlibrary.Recordings;
            // Create RecordingsJson Object
            RecordingsJson recordingsJson = new RecordingsJson { recordingEntries = new List<RecordingEntry>() };
            // Iterate through all Recordings
            foreach (Recording libraryRecording in libraryRecordings) {
                // Add RecordingEntry to RecordingsJson
                recordingsJson.recordingEntries.Add(new RecordingEntry {
                    programTitle = libraryRecording.Program.Title,
                    programEpisodeTitle = libraryRecording.Program.EpisodeTitle,
                    programSeasonNumber = libraryRecording.Program.SeasonNumber,
                    programEpisodeNumber = libraryRecording.Program.EpisodeNumber,
                    fileSize = libraryRecording.FileSize,
                    startTime = libraryRecording.StartTime,
                    endTime = libraryRecording.EndTime,
                    resumePoint = libraryRecording.GetBookmark("MCE_shell").ToString()
                });
            }
            
            // Serialise RecordingsJson to String
            string recordingsJsonString = JsonConvert.SerializeObject(recordingsJson);

            System.Diagnostics.Debug.WriteLine($"Sending Resume Points to Client {clientIp}");
            // Create Network Client
            NetworkClient client = new NetworkClient(clientIp);
            // Send Resume Request to Client
            client.SendResumeUpdate(recordingsJsonString);
        }

        // Send a Specific Recording's Resume Point to all Clients
        private static void SendResumeUpdate(Recording libraryRecording) {
            // Create RecordingsJson Object
            RecordingsJson recordingsJson = new RecordingsJson { recordingEntries = new List<RecordingEntry>() };
            // Add RecordingEntry to RecordingsJson
            recordingsJson.recordingEntries.Add(new RecordingEntry {
                programTitle = libraryRecording.Program.Title,
                programEpisodeTitle = libraryRecording.Program.EpisodeTitle,
                programSeasonNumber = libraryRecording.Program.SeasonNumber,
                programEpisodeNumber = libraryRecording.Program.EpisodeNumber,
                fileSize = libraryRecording.FileSize,
                startTime = libraryRecording.StartTime,
                endTime = libraryRecording.EndTime,
                resumePoint = libraryRecording.GetBookmark("MCE_shell").ToString()
            });
            // Serialise RecordingsJson to String
            string recordingsJsonString = JsonConvert.SerializeObject(recordingsJson);
            System.Diagnostics.Debug.WriteLine(recordingsJsonString);

            // Iterate through each Client in RegisteredClients
            foreach (string clientIp in server.GetRegisteredClients()) {
                System.Diagnostics.Debug.WriteLine($"Sending to Client {clientIp}");
                // Create Network Client
                NetworkClient client = new NetworkClient(clientIp);
                // Send Resume Request to Client
                client.SendResumeUpdate(recordingsJsonString);
            }
        }

        // Distribute a Resume Point Update made by a Client to all Other Clients
        private static void DistributeResumeUpdate(string recordingsJsonString, string fromIp) {
            // Iterate through each Client in RegisteredClients
            foreach (string clientIp in server.GetRegisteredClients().Where(xx => xx != fromIp)) {
                // Create Network Client
                NetworkClient client = new NetworkClient(clientIp);
                // Send Resume Request to Client
                client.SendResumeUpdate(recordingsJsonString);
            }
        }

        // Receive a Resume Point Update made by a Client
        private static void ReceiveResumeUpdate(RecordingsJson received) {
            var libraryRecordings = TVlibrary.Recordings;
            foreach (RecordingEntry entry in received.recordingEntries) {
                Recording rec = libraryRecordings.FirstOrDefault(xx =>
                    xx.Program.Title == entry.programTitle &&
                    xx.Program.EpisodeTitle == entry.programEpisodeTitle &&
                    xx.Program.SeasonNumber == entry.programSeasonNumber &&
                    xx.Program.EpisodeNumber == entry.programEpisodeNumber &&
                    xx.FileSize == entry.fileSize &&
                    xx.StartTime == entry.startTime &&
                    xx.EndTime == entry.endTime
                );
                if (rec != null) {
                    System.Diagnostics.Debug.WriteLine($"Found Recording: {rec.Program.Title}");
                    rec.SetBookmark("MCE_shell", TimeSpan.Parse(entry.resumePoint));
                }
                
            }
        }

        #endregion ############################################################

    }
}
