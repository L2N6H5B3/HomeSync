﻿using Microsoft.MediaCenter.Pvr;
using Microsoft.MediaCenter.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using HomeSync.Classes.Recording;
using HomeSync.Classes.Network;
using System.Threading;
using HomeSync.Classes;
using Newtonsoft.Json;

namespace HomeSync.Server
{


    static class Program {

        private static Log log;
        private static List<Retry> retryList;
        private static Settings settings;
        private static ObjectStore TVstore;
        private static Library TVlibrary;
        private static NetworkServer server;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {

            #region Initialise Variables ######################################

            // Create Log Object
            log = new Log();
            // Create RetryList Object
            retryList = new List<Retry>();

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


            #region Start Server ##############################################

            new Thread(() => {
                // Set Thread to Background
                Thread.CurrentThread.IsBackground = true;
                // Create new Server
                server = new NetworkServer(log);
                // Add Event Handler for Server Response
                server.ResponseEvent += Server_ResponseEvent;
                // Add Event Handler for Server Status
                server.StatusEvent += Server_StatusEvent;
                // Add Event Handler for Client Heartbeat
                server.HeartbeatEvent += Server_HeartbeatEvent;
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
                case "RegisterClient":
                    // Send All Resume Points to Client
                    SendAllResumePoints(e.clientIp);
                    // Write to Log
                    log.WriteLine($"Client ({e.clientIp}) Registered");
                    break;
                case "ResumeUpdate":
                    // Receive the Resume Update
                    ReceiveResumeUpdate(JsonConvert.DeserializeObject<RecordingsJson>(e.response.Replace("<EOF>","")));
                    // Distribute the Resume Update to all Clients
                    DistributeResumeUpdate(e.response, e.clientIp);
                    break;
            }

        }

        private static void Server_StatusEvent(object sender, StatusArgs e) {
            settings.SetStatus(e.status);
        }

        private static void Client_RetryEvent(object sender, RetryArgs e) {
            // Check if there is a pending Client Retry
            Retry retry = retryList.FirstOrDefault(xx => xx.ipAddress == e.clientAddress);
            // If there is no Client Retry
            if (retry == null) {
                // Add new Retry to the Retry List
                retryList.Add(new Retry(e.clientAddress, e.data));
            } else {
                // Add new data to Retry
                retry.Add(e.data);
            }
        }

        private static void Server_HeartbeatEvent(object sender, HeartbeatArgs e) {
            // Check if there is a pending Client Retry
            Retry retry = retryList.FirstOrDefault(xx => xx.ipAddress == e.clientIp);
            // If there is a pending Client Retry
            if (retry != null) {
                // Create RecordingsJson Object
                RecordingsJson recordingsJson = new RecordingsJson { recordingEntries = retry.recordingEntries };
                // Remove Old Retry
                retryList.Remove(retry);
                // Retry ResumeUpdate
                RetryResumeUpdate(JsonConvert.SerializeObject(recordingsJson), retry.ipAddress);
            }
        }

        #endregion ############################################################


        #region Resume Update #################################################

        // Send all Resume Points to a new Client
        private static void SendAllResumePoints(string clientIp) {
            // Set current status in Form
            settings.SetStatus("Syncing all resume positions");
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

            // Create Network Client
            NetworkClient client = new NetworkClient(clientIp, log);
            // Add Client RetryEvent Handler
            client.RetryEvent += Client_RetryEvent;
            // Send Resume Request to Client
            bool result = client.SendResumeUpdate(recordingsJsonString);
            // If the Request Completed Successfully
            if (result) {
                // Write to Log
                log.WriteLine($"Client ({clientIp}) Resume Positions Syncronised");
            }
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        // Send a Specific Recording's Resume Point to all Clients
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
                fileSize = libraryRecording.FileSize,
                startTime = libraryRecording.StartTime,
                endTime = libraryRecording.EndTime,
                resumePoint = libraryRecording.GetBookmark("MCE_shell").ToString()
            });
            // Serialise RecordingsJson to String
            string recordingsJsonString = JsonConvert.SerializeObject(recordingsJson);

            // Iterate through each Client in RegisteredClients
            foreach (string clientIp in server.GetRegisteredClients()) {
                // Create Network Client
                NetworkClient client = new NetworkClient(clientIp, log);
                // Add Client RetryEvent Handler
                client.RetryEvent += Client_RetryEvent;
                // Send Resume Request to Client
                bool result = client.SendResumeUpdate(recordingsJsonString);
                // If the Request Completed Successfully
                if (result) {
                    // Write to Log
                    log.WriteLine($"Sent \"{libraryRecording.Program.Title}\" resume position to Client ({clientIp})");
                }
            }
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        // Distribute a Resume Point Update made by a Client to all Other Clients
        private static void DistributeResumeUpdate(string recordingsJsonString, string fromIp) {
            // Set current status in Form
            settings.SetStatus("Forwarding resume position");
            // Iterate through each Client in RegisteredClients
            foreach (string clientIp in server.GetRegisteredClients().Where(xx => xx != fromIp)) {
                // Write to Log
                log.WriteLine($"Contacting client: {clientIp}");
                // Create Network Client
                NetworkClient client = new NetworkClient(clientIp, log);
                // Add Client RetryEvent Handler
                client.RetryEvent += Client_RetryEvent;
                // Send Resume Request to Client
                bool result = client.SendResumeUpdate(recordingsJsonString);
                // If the Request Completed Successfully
                if (result) {
                    // Write to Log
                    log.WriteLine($"Forwarded resume position to Client ({clientIp})");
                }
            }
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        // Retry Distribution of Resume Point Update
        private static void RetryResumeUpdate(string recordingsJsonString, string clientIp) {
            // Set current status in Form
            settings.SetStatus("Distributing resume position");
            // Write to Log
            log.WriteLine($"Contacting client: {clientIp}");
            // Create Network Client
            NetworkClient client = new NetworkClient(clientIp, log);
            // Add Client RetryEvent Handler
            client.RetryEvent += Client_RetryEvent;
            // Send Resume Request to Client
            bool result = client.SendResumeUpdate(recordingsJsonString);
            // If the Request Completed Successfully
            if (result) {
                // Write to Log
                log.WriteLine($"Sent retry resume positions to Client ({clientIp})");
            }
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        // Receive a Resume Point Update made by a Client
        private static void ReceiveResumeUpdate(RecordingsJson received) {
            // Write to Log
            log.WriteLine($"Setting Resume Points");
            var libraryRecordings = TVlibrary.Recordings;
            int currentIndex = 1;
            foreach (RecordingEntry entry in received.recordingEntries) {
                // Write to Log
                log.WriteLine($"({currentIndex} of {received.recordingEntries.Count}): \"{entry.programTitle}\"");
                // Set current status in Form
                settings.SetStatus($"Processing recording {currentIndex} of {received.recordingEntries.Count}");
                libraryRecordings.FirstOrDefault(xx =>
                    xx.Program.Title == entry.programTitle &&
                    xx.Program.EpisodeTitle == entry.programEpisodeTitle &&
                    xx.Program.SeasonNumber == entry.programSeasonNumber &&
                    xx.Program.EpisodeNumber == entry.programEpisodeNumber &&
                    xx.FileSize == entry.fileSize &&
                    xx.StartTime == entry.startTime &&
                    xx.EndTime == entry.endTime
                )?.SetBookmark("MCE_shell", TimeSpan.Parse(entry.resumePoint));
                currentIndex++;
            }
            // Write to Log
            log.WriteLine($"Resume Positions Up to Date");
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        #endregion ############################################################

    }
}
