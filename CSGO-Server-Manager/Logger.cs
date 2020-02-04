using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Kxnrl.CSM
{
    class Logger
    {
        private static readonly string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_log.log");
        private static readonly string errFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_err.log");
        private static readonly string mapFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_map.log");
        private static readonly string NewLine = "  " + Environment.NewLine;

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

        public static void Push(string e, string m)
        {
            string desp = "### " + Global.hostname + NewLine
                + "时间:" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + NewLine
                + "人数:" + Global.currentPlayers.ToString() + "/" + Global.maximumPlayers.ToString() + NewLine
                + "地图:" + Global.currentMap + NewLine
                + "事件:" + e + NewLine
                + "原因:" + m;

            POST("https://sc.ftqq.com/" + Configs.SCKEY + ".send", new NameValueCollection
            {
                { "text", Configs.game.First().ToString().ToUpper() + Configs.game.Substring(1) + "." + "SRCDS" + "." + "Crashed" },
                { "desp", desp }
            });
        }

        private static void POST(string url, NameValueCollection form)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                    client.UploadValues(url, "POST", form);
                }
            }
            catch (Exception e)
            {
                Error("Failed to push log: " + e.Message);
            }
        }

        public static void Check()
        {
            if (Configs.SCKEY != null)
            {
                Console.WriteLine("{0} >>> ServerChan -> Message pushing is available.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            }
            else
            {
                Console.WriteLine("{0} >>> ServerChan -> SCKEY was not found. -> to create https://sc.ftqq.com", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            }
        }
    }
}
