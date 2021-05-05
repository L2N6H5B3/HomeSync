using HomeSync.Classes;
using HomeSync.Classes.Network;
using HomeSync.Classes.Recording;
using Microsoft.MediaCenter.Pvr;
using Microsoft.MediaCenter.Store;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HomeSync.Client {


    static class Program {

        private static Log log;
        private static Retry retry;
        private static Settings settings;
        private static ObjectStore TVstore;
        private static Library TVlibrary;
        private static NetworkServer server;
        private static List<string> WatchedFolders;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {

            #region Initialise Variables ######################################

            // Create Log Object
            log = new Log();
            // Create Retry Object
            retry = new Retry();
            // Create WatchedFolders List
            WatchedFolders = new List<string>();

            #endregion ########################################################


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
            // Write to Log
            log.WriteLine("Opening WMC Store");
            // Get TVStore
            TVstore = ObjectStore.Open("", FriendlyName, DisplayName, true);
            // Create TVLibrary
            TVlibrary = new Library(TVstore, true, false);
            // Write to Log
            log.WriteLine("Listening for WMC Store Updates");
            // Add Store Listener
            TVstore.StoredObjectUpdated += TVstore_StoredObjectUpdated;

            #endregion ########################################################


            #region Configure Application #####################################

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            settings = new Settings();

            #endregion ########################################################


            #region Register Client and Start Server ##########################

            new Thread(() => {
                // Set Thread to Background
                Thread.CurrentThread.IsBackground = true;

                // Create Network Client
                NetworkClient client = new NetworkClient(log);
                // Add Event Handler for Server Retry
                client.RetryEvent += Client_RetryEvent;
                // Add Event Handler for Connection Status
                client.StatusEvent += Client_StatusEvent;
                // Connect Client
                client.Connect(retry);
                // Continue to Attempt to connect Client
                while (!client.IsConnected()) {
                    Thread.Sleep(10000);
                    client.Connect(retry);
                }

                // If Client is Connected
                if (client.IsConnected()) {
                    // Register Client
                    client.Register();
                }
                // Create Heartbeat
                client.CreateHeartbeat(retry);

                // Create new Server
                server = new NetworkServer(log);
                // Add Event Handler for Server Response
                server.ResponseEvent += Server_ResponseEvent;
                // Set current status in Form
                settings.SetStatus("Ready");
                // Start Server
                server.Start();
            }).Start();

            #endregion ########################################################


            #region Run Application ###########################################

            Application.Run(settings);

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
                // Send the Resume Point Update
                SendResumeUpdate(recording);
            }
        }

        private static void Server_ResponseEvent(object sender, ResponseArgs e) {
            switch (e.responseType) {
                case "ResumeUpdate":
                    // Receive the Resume Update
                    ReceiveResumeUpdate(JsonConvert.DeserializeObject<RecordingsJson>(e.response.Replace("<EOF>", "")));
                    break;
            }
        }

        private static void Client_RetryEvent(object sender, RetryArgs e) {
            // Add new RecordingEntries to the Retry
            retry.Add(e.data);
        }

        private static void Client_StatusEvent(object sender, StatusArgs e) {
            // Set Status
            settings.SetStatus(e.status);
        }

        #endregion ############################################################


        #region Resume Update #################################################

        // Send a Specific Recording's Resume Point to Server
        private static void SendResumeUpdate(Recording libraryRecording) {
            // Write to Log
            log.WriteLine($"Syncronising resume position: \"{libraryRecording.Program.Title}\"");
            // Set current status in Form
            settings.SetStatus("Syncing resume position");

            // Create RecordingsJson Object
            RecordingsJson recordingsJson = new RecordingsJson { recordingEntries = new List<RecordingEntry>() };
            // Add RecordingEntry to RecordingsJson
            recordingsJson.recordingEntries.Add(new RecordingEntry {
                programTitle = libraryRecording.Program.Title,
                programEpisodeTitle = libraryRecording.Program.EpisodeTitle,
                programSeasonNumber = libraryRecording.Program.SeasonNumber,
                programEpisodeNumber = libraryRecording.Program.EpisodeNumber,
                fileName = libraryRecording.FileName,
                fileSize = libraryRecording.FileSize,
                startTime = libraryRecording.StartTime,
                endTime = libraryRecording.EndTime,
                resumePoint = libraryRecording.GetBookmark("MCE_shell").ToString()
            });
            // Serialise RecordingsJson to String
            string recordingsJsonString = JsonConvert.SerializeObject(recordingsJson);

            // Create Network Client
            NetworkClient client = new NetworkClient(log);
            // Add Event Handler for Server Retry
            client.RetryEvent += Client_RetryEvent;
            // Add Event Handler for Connection Status
            client.StatusEvent += Client_StatusEvent;
            // Connect Client
            client.Connect(retry);
            // Send Resume Request to Server
            bool result = client.SendResumeUpdate(recordingsJsonString);
            // If the Request Completed Successfully
            if (result) {
                // Write to Log
                log.WriteLine($"Synchronised resume position: \"{libraryRecording.Program.Title}\"");
            }
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        // Receive a Resume Point Update from Server
        private static void ReceiveResumeUpdate(RecordingsJson received) {
            // Write to Log
            log.WriteLine($"Setting Resume Points");
            var libraryRecordings = TVlibrary.Recordings;
            // Set Index
            int currentIndex = 1;
            // Iterate and Set WatchedFolders and Add to Library
            foreach (RecordingEntry entry in received.recordingEntries) {
                // Write to Log
                log.WriteLine($"({currentIndex} of {received.recordingEntries.Count}): Importing \"{entry.programTitle}\"");
                // Set current status in Form
                settings.SetStatus($"Importing recording {currentIndex} of {received.recordingEntries.Count}");
                // Add Recording Path to WatchedFolders
                AddWatchedFolder(entry.fileName);
                // Add Recording to WMC Database
                TVlibrary.GetOrAddRecordingForFile(entry.fileName);
                // Increment Index
                currentIndex++;
            }
            // Reset Index
            currentIndex = 1;
            // Iterate and Set Resume Points
            foreach (RecordingEntry entry in received.recordingEntries) {
                // Write to Log
                log.WriteLine($"({currentIndex} of {received.recordingEntries.Count}): Setting \"{entry.programTitle}\"");
                // Set current status in Form
                settings.SetStatus($"Processing recording {currentIndex} of {received.recordingEntries.Count}");
                libraryRecordings.FirstOrDefault(xx =>
                    xx.FileName.ToLower() == entry.fileName.ToLower() &&
                    xx.State == RecordingState.Recorded
                )?.SetBookmark("MCE_shell", TimeSpan.Parse(entry.resumePoint));
                // Increment Index
                currentIndex++;
            }
            // Write to Log
            log.WriteLine($"Resume Positions Up to Date");
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        #endregion ############################################################


        #region Watched Folders ###############################################

        public static void AddWatchedFolder(string fileName) {
            // Get Directory
            string directory = Path.GetDirectoryName(fileName);
            // Check if Directory exists in WatchedFolders List
            if (!WatchedFolders.Contains(directory)) {
                // Get Windows Media Center Recording Key
                RegistryKey recordingKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Media Center\Service\Recording", true);
                // Get Data from WatchedFolders Value
                List<string> data = new List<string>((string[])recordingKey.GetValue("WatchedFolders"));
                // Check if Directory exists in WatchedFolders Value
                string location = data.FirstOrDefault(xx => xx.ToLower() == directory.ToLower());
                // If Directory doesn't exist
                if (location == null) {
                    // Add Directory
                    data.Add(directory);
                    // Synthesise WatchedFolders Value
                    recordingKey.SetValue("WatchedFolders", data.ToArray());
                    // Add Directory to WatchedFolders List
                    WatchedFolders.Add(directory);
                }
                // Close access to the Registry Key
                recordingKey.Close();
            }
        }
        #endregion ############################################################
    }
}
