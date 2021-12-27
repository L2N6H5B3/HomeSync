using System;

namespace HomeSync.Classes.Recording {

    public class RecordingRequest {
        public string programTitle { get; set; }
        public string programEpisodeTitle { get; set; }
        public int programSeasonNumber { get; set; }
        public int programEpisodeNumber { get; set; }
        public long fileSize { get; set; }
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
        public string resumePoint { get; set; }
    }
}