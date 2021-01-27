﻿using Microsoft.MediaCenter.Pvr;
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
using HomeSync.Classes;

namespace HomeSync.Client {


    static class Program {

        private static Log log;
        private static Retry retry;
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
            // Create Retry Object
            retry = new Retry();

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
                // Add Event Handler for Server Heartbeat
                client.ServerConnectEvent += Client_ServerConnectEvent;
                // Connect Client
                client.Connect();

                // Continue to Attempt to connect Client
                while (!client.IsConnected()) {
                    Thread.Sleep(10000);
                    client.Connect();
                }

                // If Client is Connected
                if (client.IsConnected()) {
                    // Register Client
                    client.Register();
                }

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
            settings.SetStatus(e.status);
        }

        private static void Client_ServerConnectEvent(object sender, EventArgs e) {
            // Check if there is a pending Server Retry
            if (retry.recordingEntries.Count > 0) {
                // Write to Log
                log.WriteLine("Retries Exist, Sending Resume Updates");
                // Create RecordingsJson Object
                RecordingsJson recordingsJson = new RecordingsJson { recordingEntries = retry.recordingEntries };
                // Reset Retry RecordingEntry List
                retry.recordingEntries = new List<RecordingEntry>();
                // Retry ResumeUpdate
                RetryResumeUpdate(JsonConvert.SerializeObject(recordingsJson));
            }
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
            client.Connect();
            // Send Resume Request to Server
            client.SendResumeUpdate(recordingsJsonString);
            // Write to Log
            log.WriteLine($"Synchronised resume position: \"{libraryRecording.Program.Title}\"");
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        // Retry Distribution of Resume Point Update
        private static void RetryResumeUpdate(string recordingsJsonString) {
            // Set current status in Form
            settings.SetStatus("Syncing resume position");
            // Create Network Client
            NetworkClient client = new NetworkClient(log);
            // Add Client RetryEvent Handler
            client.RetryEvent += Client_RetryEvent;
            // Add Event Handler for Connection Status
            client.StatusEvent += Client_StatusEvent;
            // Connect Client
            client.Connect();
            // Send Resume Request to Server
            client.SendResumeUpdate(recordingsJsonString);
            // Write to Log
            log.WriteLine($"Sent retry resume positions");
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        // Receive a Resume Point Update from Server
        private static void ReceiveResumeUpdate(RecordingsJson received) {
            var libraryRecordings = TVlibrary.Recordings;
            int currentIndex = 1;
            foreach (RecordingEntry entry in received.recordingEntries) {
                // Write to Log
                log.WriteLine($"Processing recording ({currentIndex} of {received.recordingEntries.Count}): \"{entry.programTitle}\"");
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
            // Set current status in Form
            settings.SetStatus("Ready");
        }

        #endregion ############################################################
    }
}
