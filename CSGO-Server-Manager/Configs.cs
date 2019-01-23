﻿using System;
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

        public static string game
        {
            get { return Get("Global", "game", "csgo"); }
            set { Set("Global", "game", value); }
        }

        public static string appid
        {
            get
            {
                switch (game)
                {
                    case "csgo": return "740";
                    case "left4dead2": return "222850";
                    case "insurgency": return "237410";
                }
                return "740";
            }
        }

        public static string token
        {
            get { return Get("SteamWorks", "Token", null); }
            set { Set("SteamWorks", "Token", value); }
        }

        public static string groupids
        {
            get { return Get("SteamWorks", "Group", null); }
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
        }

        public static string tickrate
        {
            get { return Get("Server", "TickRate", null); }
        }

        public static string maxplayers
        {
            get { return Get("Server", "MaxPlays", null); }
        }

        public static string nobots
        {
            get { return Get("Server", "NoBotsEx", null); }
        }

        public static string gametype
        {
            get { return Get("Server", "GameType", null); }
        }

        public static string gamemode
        {
            get { return Get("Server", "GameMode", null); }
        }

        public static string mapgroup
        {
            get { return Get("Server", "MapGroup", null); }
        }

        public static string startmap
        {
            get { return Get("Server", "StartMap", null); }
            set { Set("Server", "StartMap", value); }
        }

        public static string TokenApi
        {
            get { return Get("TokenApi", "ApiKey", null); }
        }

        public static string SteamApi
        {
            get { return Get("SteamWorks", "ApiKey", null); }
        }

        public static string options
        {
            get { return Get("Server", "Options", null); }
        }

        public static string SCKEY
        {
            get { return Get("ServerChan", "SCKEY", null); }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WritePrivateProfileString(string section, string key, string val, string filepath);

        [DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retval, int size, string filePath);

        private static StringBuilder stringBuilder = new StringBuilder(1024);
        public static string Get(string section, string key, string defaultValue)
        {
            GetPrivateProfileString(section, key, defaultValue, stringBuilder, 1024, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.ini"));
            if (stringBuilder.ToString().Equals("null"))
                return null;
            return stringBuilder.ToString();
        }

        private static bool Create(string section, string key, string val)
        {
            return WritePrivateProfileString(section, key, val, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.ini"));
        }

        private static void Set(string section, string key, string val)
        {
            Global.watcher.EnableRaisingEvents = false;
            if (WritePrivateProfileString(section, key, val, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.ini")))
            {
                Global.backup = null;
                Backup();
            }
            Global.watcher.EnableRaisingEvents = true;
        }

        private static string backup = string.Empty;
        private static void Backup()
        {
            using (StreamReader file = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.ini")))
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
            using (StreamWriter file = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config_backup.ini"), false, Encoding.Unicode))
            {
                file.Write(Global.backup);
            }
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config_backup.ini"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.ini"), true);
            Global.watcher.EnableRaisingEvents = true;
        }

        public static bool Check()
        {
            if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.ini")))
            {
                Create("Global", "srcds", Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "srcds.exe"));
                Create("Global", "steam", Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "steamcmd.exe"));

                if (Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "csgo")))
                {
                    // csgo::740
                    Create("Global", "game", "csgo");
                }
                else if (Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "left4dead2")))
                {
                    // l4d2::222860
                    Create("Global", "game", "left4dead2");
                }
                else if (Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "insurgency")))
                {
                    // insurgency::237410
                    Create("Global", "game", "insurgency");
                }
                else
                {
                    // default
                    Create("Global", "game", "csgo");
                }

                Create("SteamWorks", "Token",    "null");
                Create("SteamWorks", "Group",    "null");
                Create("SteamWorks", "SteamApi", "null");

                Create("Server", "IP", Helper.GetLocalIpAddress());
                Create("Server", "Port", "27015");
                Create("Server", "Insecure", "0");
                Create("Server", "TickRate", "128");
                Create("Server", "MaxPlays", "64");
                Create("Server", "NoBotsEx", "0");
                Create("Server", "GameType", "0");
                Create("Server", "GameMode", "0");
                Create("Server", "MapGroup", "custom_maps");
                Create("Server", "StartMap", "de_dust2");
                Create("Server", "Options", "+exec options.cfg");

                Create("TokenApi", "ApiKey", "null");

                Create("ServerChan", "SCKEY", "null");

                return false;
            }

            return true;
        }
    }
}
