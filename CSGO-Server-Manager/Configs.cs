using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Kxnrl.CSM
{
    class Configs
    {
        public static string srcds
        {
            get { return Get("Global", "srcds", null); }
            set { Set("Global", "srcds", value); }
        }

        public static string steam
        {
            get { return Get("Global", "steam", null); }
            set { Set("Global", "steam", value); }
        }

        public static string token
        {
            get { return Get("SteamWorks", "Token", null); }
            set { Set("SteamWorks", "Token", value); }
        }

        public static string groupids
        {
            get { return Get("SteamWorks", "Group", null); }
            set { Set("SteamWorks", "Group", value); }
        }

        public static string ip
        {
            get { return Get("Server", "IP", null); }
            set { Set("Server", "IP", value); }
        }

        public static string port
        {
            get { return Get("Server", "Port", null); }
            set { Set("Server", "Port", value); }
        }

        public static string insecure
        {
            get { return Get("Server", "Insecure", null); }
            set { Set("Server", "Insecure", value); }
        }

        public static string tickrate
        {
            get { return Get("Server", "TickRate", null); }
            set { Set("Server", "TickRate", value); }
        }

        public static string maxplayers
        {
            get { return Get("Server", "MaxPlays", null); }
            set { Set("Server", "MaxPlays", value); }
        }

        public static string nobots
        {
            get { return Get("Server", "NoBotsEx", null); }
            set { Set("Server", "NoBotsEx", value); }
        }

        public static string gametype
        {
            get { return Get("Server", "GameType", null); }
            set { Set("Server", "GameType", value); }
        }

        public static string gamemode
        {
            get { return Get("Server", "GameMode", null); }
            set { Set("Server", "GameMode", value); }
        }

        public static string mapgroup
        {
            get { return Get("Server", "MapGroup", null); }
            set { Set("Server", "MapGroup", value); }
        }

        public static string startmap
        {
            get { return Get("Server", "StartMap", null); }
            set { Set("Server", "StartMap", value); }
        }

        public static string TokenApi
        {
            get { return Get("TokenApi", "ApiKey", null); }
            set { Set("TokenApi", "ApiKey", value); }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WritePrivateProfileString(string section, string key, string val, string filepath);

        [DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retval, int size, string filePath);

        private static StringBuilder stringBuilder = new StringBuilder(1024);
        public static string Get(string section, string key, string defaultValue)
        {
            GetPrivateProfileString(section, key, defaultValue, stringBuilder, 1024, Environment.CurrentDirectory + "\\server_config.ini");
            if (stringBuilder.ToString().Equals("null"))
                return null;
            return stringBuilder.ToString();
        }

        private static bool Create(string section, string key, string val)
        {
            return WritePrivateProfileString(section, key, val, Environment.CurrentDirectory + "\\server_config.ini");
        }

        private static void Set(string section, string key, string val)
        {
            Global.watcher.EnableRaisingEvents = false;
            if (WritePrivateProfileString(section, key, val, Environment.CurrentDirectory + "\\server_config.ini"))
            {
                Global.backup = null;
                Backup();
            }
            Global.watcher.EnableRaisingEvents = true;
        }

        private static string backup = string.Empty;
        private static void Backup()
        {
            using (StreamReader file = new StreamReader(Environment.CurrentDirectory + "\\server_config.ini"))
            {
                backup = file.ReadToEnd();
                if (backup.Length <= 128)
                {
                    Console.WriteLine("{0} >>> Failed to back up configs!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                }
                else Global.backup = backup;
            }
        }

        public static void Restore()
        {
            if (string.IsNullOrEmpty(Global.backup))
                return;

            Global.watcher.EnableRaisingEvents = false;
            using (StreamWriter file = new StreamWriter(Environment.CurrentDirectory + "\\server_config_backup.ini", false, Encoding.Unicode))
            {
                file.Write(Global.backup);
            }
            File.Copy(Environment.CurrentDirectory + "\\server_config_backup.ini", Environment.CurrentDirectory + "\\server_config.ini", true);
            Global.watcher.EnableRaisingEvents = true;
        }

        public static bool Check()
        {
            if (!File.Exists(Environment.CurrentDirectory + "\\server_config.ini"))
            {
                Create("Global", "srcds", Environment.CurrentDirectory + "\\srcds.exe");
                Create("Global", "steam", Environment.CurrentDirectory + "\\steamcmd.exe");

                Create("SteamWorks", "Token", "null");
                Create("SteamWorks", "Group", "null");

                Create("Server", "IP", Helper.GetLocalIpAddress());
                Create("Server", "Port", "null");
                Create("Server", "Insecure", "0");
                Create("Server", "TickRate", "128");
                Create("Server", "MaxPlays", "64");
                Create("Server", "NoBotsEx", "0");
                Create("Server", "GameType", "0");
                Create("Server", "GameMode", "0");
                Create("Server", "MapGroup", "custom_maps");
                Create("Server", "StartMap", "de_dust2");

                Create("TokenApi", "ApiKey", "null");

                return false;
            }

            return true;
        }
    }
}
