using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeSync.Classes.Recording {
    public class RecordingState {
        [Index(0)]
        public string FileName { get; set; }
        [Index(1)]
        public DateTime DateAdded { get; set; }
        [Index(2)]
        public TimeSpan ResumeTime { get; set; }
    }
}
