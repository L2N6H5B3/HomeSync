using HomeSync.Classes.Recording;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HomeSync.Classes {
    class Retry {
        public string ipAddress { get; set; }
        public List<RecordingEntry> recordingEntries { get; set; }

        public Retry(string clientIp, string recordingsJson) {
            ipAddress = clientIp;
            Add(recordingsJson);
        }

        public void Add(string recordingsJson) {
            // Deserialise RecordingsJson
            List<RecordingEntry> jsonRecordingEntries = JsonConvert.DeserializeObject<RecordingsJson>(recordingsJson).recordingEntries;
            // Iterate through each RecordingEntry in JSON
            foreach (RecordingEntry jsonRecordingEntry in jsonRecordingEntries) {
                // Check if a RecordingEntry already exists for this Programme
                RecordingEntry recordingEntry = recordingEntries.FirstOrDefault(xx =>
                    xx.programTitle == jsonRecordingEntry.programTitle &&
                    xx.programEpisodeTitle == jsonRecordingEntry.programEpisodeTitle &&
                    xx.programSeasonNumber == jsonRecordingEntry.programSeasonNumber &&
                    xx.programEpisodeNumber == jsonRecordingEntry.programEpisodeNumber &&
                    xx.startTime == jsonRecordingEntry.startTime &&
                    xx.endTime == jsonRecordingEntry.endTime
                );
                if (recordingEntry == null) {
                    // Add the RecordingEntry
                    recordingEntries.Add(jsonRecordingEntry);
                } else {
                    // Remove Old RecordingEntry
                    recordingEntries.Remove(recordingEntry);
                    // Add New RecordingEntry
                    recordingEntries.Add(jsonRecordingEntry);
                }
            }
        }
    }
}
