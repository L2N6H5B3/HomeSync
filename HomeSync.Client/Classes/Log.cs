using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HomeSync.Classes {
    class Log {

        FileStream fileStream;
        StreamWriter streamWriter;

        public Log() {
            // Create Log File Name
            string logFileName = "log" + "-" + DateTime.Today.ToString("yyyyMMdd") + "." + "txt";
            // Create FileStream Object
            fileStream = new FileStream(logFileName, FileMode.Append);
            // Create StreamWriter Object
            streamWriter = new StreamWriter(fileStream);
        }

        public void WriteLine(string message) {
            try {
                streamWriter.WriteLine(message);
                StringBuilder builder = new StringBuilder();
                builder.Append(message.PadLeft(2, '0').PadRight(3, ' '));
                streamWriter.WriteLine(builder.ToString().Trim());
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine($"Log exception: {e}");
            }
        }
    }
}
