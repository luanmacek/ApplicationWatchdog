using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationWatchdog.Core
{
    public static class Logger
    {
        public static void WriteErrorLog(string Message)
        {
            CreateDirectoriesIfNecessary(ConfigurationManager.AppSettings["LogPath"]);
            StreamWriter sw = new StreamWriter(Path.Combine(ConfigurationManager.AppSettings["LogPath"], ConfigurationManager.AppSettings["LogFile"]), true);
            sw.WriteLine(DateTime.Now.ToString() + ": " + Message);
            sw.Flush();
            sw.Close();
        }

        private static void CreateDirectoriesIfNecessary(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch { }
        }

    }
}
