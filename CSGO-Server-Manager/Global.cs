﻿using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace Kxnrl.CSM
{
    class Global
    {
        public static bool A2SFireWall = false;
        public static bool update = false;
        public static bool crash = false;
        public static string backup = null;
        public static string currentMap = null;
        public static Process srcds = null;
        public static Thread tcrash = null;
        public static Thread tupdate = null;
        public static FileSystemWatcher watcher = null;
        public static IPEndPoint ipep;
        public static uint currentPlayers = 0;
        public static uint maximumPlayers = 0;
        public static string hostname = "SOURCE DEDICATE SERVER";
    }
}
