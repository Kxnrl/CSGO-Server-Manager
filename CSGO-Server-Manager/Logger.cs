using System;
using System.IO;
using System.Text;

namespace CSGO_Server_Manager
{
    class Logger
    {
        private static readonly string logFile = Environment.CurrentDirectory + "\\server_log.log";
        private static readonly string errFile = Environment.CurrentDirectory + "\\server_err.log";
        private static readonly string mapFile = Environment.CurrentDirectory + "\\server_map.log";

        public static void Create()
        {
            if (!File.Exists(logFile))
            {
                using (FileStream fs = File.Create(logFile))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes("----------------------------------------\nCSGO Server Manager log File\nDescription: Server log in chronological order.\nThis file was auto generate by CSM.\n----------------------------------------\nYYYY/MM/DD HH:MM:SS | Event\n----------------------------------------\n");
                    fs.Write(bytes, 0, bytes.Length);
                }
            }

            if (!File.Exists(errFile))
            {
                using (FileStream fs = File.Create(errFile))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes("----------------------------------------\nCSGO Server Manager log File\nDescription: Server error log in chronological order.\nThis file was auto generate by CSM.\n----------------------------------------\nYYYY/MM/DD HH:MM:SS | Event\n----------------------------------------\n");
                    fs.Write(bytes, 0, bytes.Length);
                }
            }

            if (!File.Exists(mapFile))
            {
                using (FileStream fs = File.Create(mapFile))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes("----------------------------------------\nCSGO Server Manager log File\nDescription: Server map log in chronological order.\nThis file was auto generate by CSM.\n----------------------------------------\nYYYY/MM/DD HH:MM:SS | Event\n----------------------------------------\n");
                    fs.Write(bytes, 0, bytes.Length);
                }
            }
        }

        public static void Log(string log)
        {
            using (StreamWriter writer = new StreamWriter(logFile, true))
            {
                writer.WriteLine("[" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] >>> " + log);
                Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " >>> " + log);
            }
        }

        public static void Error(string err)
        {
            using (StreamWriter writer = new StreamWriter(errFile, true))
            {
                writer.WriteLine("[" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] >>> " + err);
                Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " >>> " + err);
            }
        }

        public static void Map(string map)
        {
            using (StreamWriter writer = new StreamWriter(mapFile, true))
            {
                writer.WriteLine("[" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] >>> Changed Map to " + map);
                Console.WriteLine("{0} >>> Changed Map to {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), map);
            }
        }
    }
}
