using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HomeSync.Classes.Recording {

    public class RecordingsJson {
        public List<RecordingEntry> recordingEntries { get; set; }
    }

    public class RecordingEntry {

        public string programTitle { get; set; }
        public string programEpisodeTitle { get; set; }
        public int programSeasonNumber { get; set; }
        public int programEpisodeNumber { get; set; }
        public string fileName { get; set; }
        public long fileSize { get; set; }
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
        public string resumePoint { get; set; }
    }
}