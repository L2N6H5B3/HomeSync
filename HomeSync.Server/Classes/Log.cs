using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HomeSync.Classes {
    class Log {

        StreamWriter streamWriter;

        public Log() {
            // Create Log File Name
            string logFileName = "log" + "-" + DateTime.Now.ToString("yyyyMMdd") + "." + "txt";
            // Create Log File Path
            string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeSync Server");
            // Create Log File Directory
            Directory.CreateDirectory(logFilePath);
            // Create StreamWriter Object
            streamWriter = new StreamWriter(Path.Combine(logFilePath, logFileName), true);
            // Write Separator to Log
            WriteLine($"--- HomeSync Log | Start {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} ---", false);
        }

        public void WriteLine(string message, bool dateTime = true) {
            try {
                // Create a new StringBuilder
                StringBuilder builder = new StringBuilder();
                // Display DateTime by Default
                if (dateTime) {
                    // Write DateTime to StringBuilder 
                    builder.Append($"{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")}  :  ");
                }
                // Write Message to StringBuilder 
                builder.Append(message.PadLeft(2, '0').PadRight(3, ' '));
                // Write StringBuilder to StreamWriter
                streamWriter.WriteLine(builder.ToString().Trim());
                // Write StreamWriter contents to Log
                streamWriter.Flush();
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine($"Log exception: {e}");
            }
        }
    }
}
