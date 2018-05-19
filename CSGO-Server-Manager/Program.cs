/******************************************************************/
/*                                                                */
/*                       CSGO Server Manager                      */
/*                                                                */
/*                                                                */
/*  File:          Program.cs                                     */
/*  Description:   Simple CSGO Dedicated Server Manager.          */
/*                                                                */
/*                                                                */
/*  Copyright (C) 2018  Kyle                                      */
/*  2018/05/18 14:16:56                                           */
/*                                                                */
/*  This program is licensed under the MIT License.               */
/*                                                                */
/******************************************************************/


using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net.NetworkInformation;

//https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=730&version={version}&format=vdf

namespace CSGO_Server_Manager
{
    class Global
    {
        public static bool update = false;
        public static bool crash = false;
        public static string backup = null;
        public static Process srcds = null;
        public static Thread tcrash = null;
        public static Thread tupdate = null;
        public static FileSystemWatcher watcher = null;
    }

    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.Title = "CSGO Server Manager v1.0.5.Fix";

            Console.WriteLine(@"     )                                        (        *     ");
            Console.WriteLine(@"  ( /(          (                       (     )\ )   (  `    ");
            Console.WriteLine(@"  )\())  (      )\     (                )\   (()/(   )\))(   ");
            Console.WriteLine(@"|((_)\   )\ )  ((_)   ))\     ___     (((_)   /(_)) ((_)()\  ");
            Console.WriteLine(@"|_ ((_) (()/(   _    /((_)   |___|    )\___  (_))   (_()((_) ");
            Console.WriteLine(@"| |/ /   )(_)) | |  (_))             ((/ __| / __|  |  \/  | ");
            Console.WriteLine(@"  ' <   | || | | |  / -_)             | (__  \__ \  | |\/| | ");
            Console.WriteLine(@" _|\_\   \_, | |_|  \___|              \___| |___/  |_|  |_| ");
            Console.WriteLine(@"         |__/                                                ");
            Console.WriteLine(Environment.NewLine);

            bool configs = Configs.Check();
            if(!configs)
            {
                Console.WriteLine("{0} >>> Configs was not found -> Auto Generated.", DateTime.Now.ToString());
            }

            Helper.WatchFile();

            while(!File.Exists(Configs.srcdsPath))
            {
                OpenFileDialog fileBrowser = new OpenFileDialog()
                {
                    Multiselect = false,
                    Filter = "CSGO Dedicated Server (srcds.exe)|srcds.exe",
                };

                if(fileBrowser.ShowDialog() != DialogResult.OK)
                {
                    MessageBox.Show("Application Exit!\nYou can modify it manually!", "CSGO Server Manager");
                    Environment.Exit(0);
                }
                else
                {
                    Configs.srcdsPath = fileBrowser.FileName;
                    Console.WriteLine("{0} >>> Set SRCDS path -> {1}", DateTime.Now.ToString(), Configs.srcdsPath);
                }
            }

            while(!File.Exists(Configs.steamPath))
            {
                OpenFileDialog fileBrowser = new OpenFileDialog()
                {
                    Multiselect = false,
                    Filter = "SteamCmd (steamcmd.exe)|steamcmd.exe",
                };

                if (fileBrowser.ShowDialog() != DialogResult.OK)
                {
                    MessageBox.Show("Application Exit!\nYou can modify it manually!", "CSGO Server Manager");
                    Environment.Exit(0);
                }
                else
                {
                    Configs.steamPath = fileBrowser.FileName;
                    Console.WriteLine("{0} >>> Set Steam path -> {1}", DateTime.Now.ToString(), Configs.steamPath);
                }
            }

            Process[] process = Process.GetProcessesByName("srcds");
            int srcdsRunning = 0;
            foreach(Process exe in process)
            {
                if(exe.MainModule.FileName.Equals(Configs.srcdsPath))
                {
                    srcdsRunning++;
                }
            }
            if(srcdsRunning > 0)
                Console.WriteLine("{0} >>> {1} SRCDS are running [{2}]", DateTime.Now.ToString(), srcdsRunning, process[0].MainModule.FileName);

            if(string.IsNullOrEmpty(Configs.wwip) || !IPAddress.TryParse(Configs.wwip, out IPAddress ipadr))
            {
                do
                {
                    Console.WriteLine("Please input your Game Server IP ...");
                    Configs.wwip = Console.ReadLine();
                }
                while(!IPAddress.TryParse(Configs.wwip, out ipadr));
            }

            if(string.IsNullOrEmpty(Configs.port) || !int.TryParse(Configs.port, out int port))
            {
                do
                {
                    Console.WriteLine("Please input your Game Server Port (1 - 65535) ...");
                    Configs.port = Console.ReadLine();
                }
                while(!int.TryParse(Configs.port, out port));
            }

            if(!configs)
            {
                Console.WriteLine("{0} >>> Configs was initialized -> You can modify it manually!", DateTime.Now.ToString());
            }

            if(!Helper.PortAvailable(port))
            {
                Console.WriteLine("{0} >>> Port[{1}] is unavailable! Finding Application...", DateTime.Now.ToString(), port);

                try
                {
                    Process exe = Helper.GetAppByPort(port);
                    Console.WriteLine("{0} >>> Trigger SRCDS Quit -> App[{1}] PID[{2}]", DateTime.Now.ToString(), exe.MainWindowTitle, exe.Id);
                    Helper.KillSRCDS(exe);
                }
                catch(Exception e)
                {
                    Console.WriteLine("{0} >>> Not found Application: {1}", DateTime.Now.ToString(), e.Message);
                }
            }

            if(!string.IsNullOrEmpty(Configs.TKApikey))
            {
                uint times = 0;
                while(SteamApi.checkTokens(true) <= 0)
                {
                    Console.WriteLine("{0} >>> TokenApi -> Checking ... {1}", DateTime.Now.ToString(), times++);
                }
                Console.WriteLine("{0} >>> TokenApi -> feature is available.", DateTime.Now.ToString());
                new Thread(Thread_CheckToken).Start();
            }
            else
            {
                Console.WriteLine("{0} >>> TokenApi -> ApiKey was not found.", DateTime.Now.ToString());
            }

            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.Start();

            Thread.Sleep(3000);

            while(true)
            {
                string input = Console.ReadLine();
                if(Global.update)
                {
                    Console.WriteLine("Updating ...");
                    Thread.Sleep(3000);
                    continue;
                }
                if(Global.crash)
                {
                    Console.WriteLine("Restarting ...");
                    Thread.Sleep(3000);
                    continue;
                }
                switch(input.ToLower())
                {
                    case "show":
                        Window.Show(Global.srcds.MainWindowHandle.ToInt32());
                        Console.WriteLine("Show SRCDS window.");
                        break;
                    case "hide":
                        Window.Hide(Global.srcds.MainWindowHandle.ToInt32());
                        Console.WriteLine("Hide SRCDS window.");
                        break;
                    case "exec":
                        string cmds = Console.ReadLine();
                        if(cmds.Length > 1)
                        {
                            Message.Write(Global.srcds.MainWindowHandle, cmds);
                            Message.Send(Global.srcds.MainWindowHandle);
                            Console.WriteLine("Execute server command: {0}", cmds);
                        }
                        else
                        {
                            Console.WriteLine("Command is invalid.");
                        }
                        break;
                    case "quit":
                        Global.tupdate.Abort();
                        Global.tupdate = null;
                        Global.tcrash.Abort();
                        Global.tcrash = null;
                        Helper.KillSRCDS();
                        MessageBox.Show("SRCDS exit!", "Message");
                        Environment.Exit(0);
                        break;
                    case "update":
                        Global.update = true;
                        Console.WriteLine("Begin update.");
                        Global.tupdate.Abort();
                        Global.tupdate = null;
                        Global.tcrash.Abort();
                        Global.tcrash = null;
                        new Thread(Thread_UpdateCSGO).Start();
                        break;
                    default:
                        if(input.StartsWith("exec "))
                        {
                            string cmd = input.Replace("exec ", "");
                            if(cmd.Length > 1)
                            {
                                Message.Write(Global.srcds.MainWindowHandle, cmd);
                                Message.Send(Global.srcds.MainWindowHandle);
                                Console.WriteLine("Execute server command: {0}", cmd);
                            }
                            else
                            {
                                Console.WriteLine("Command is invalid.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Commands: ");
                            Console.WriteLine("show   - show srcds console window.");
                            Console.WriteLine("hide   - hide srcds console window.");
                            Console.WriteLine("exec   - exec command into srcds.");
                            Console.WriteLine("quit   - quit srcds and application.");
                            Console.WriteLine("exit   - quit srcds and application.");
                            Console.WriteLine("update - force srcds update.");
                        }
                        break;
                }
            }
        }

        static void Thread_CheckCrashs()
        {
            string args = "-console -game csgo" + " "
                        + "-ip " + Configs.wwip + " "
                        + "-port " + Configs.port + " "
                        + ((!string.IsNullOrEmpty(Configs.insecure) && int.TryParse(Configs.insecure, out int novalveac) && novalveac == 1) ? "-insecure " : "")
                        + ((!string.IsNullOrEmpty(Configs.tickrate) && int.TryParse(Configs.tickrate, out int TickRate)) ? string.Format("-tickrate {0} ", TickRate) : "")
                        + ((!string.IsNullOrEmpty(Configs.maxplays) && int.TryParse(Configs.maxplays, out int maxPlays)) ? string.Format("-maxplayers_override {0} ", maxPlays) : "")
                        + ((!string.IsNullOrEmpty(Configs.nobotsex) && int.TryParse(Configs.nobotsex, out int nobots) && nobots == 1) ? "-nobots " : "")
                        + ((!string.IsNullOrEmpty(Configs.gametype) && int.TryParse(Configs.gametype, out int gameType)) ? string.Format("+gametype {0} ", gameType) : "")
                        + ((!string.IsNullOrEmpty(Configs.gamemode) && int.TryParse(Configs.gamemode, out int gameMode)) ? string.Format("+gamemode {0} ", gameMode) : "")
                        + ((!string.IsNullOrEmpty(Configs.mapgroup)) ? string.Format("+mapgroup {0} ", Configs.mapgroup) : "")
                        + ((!string.IsNullOrEmpty(Configs.startmap)) ? string.Format("+map {0} ", Configs.startmap) : "")
                        + ((!string.IsNullOrEmpty(Configs.accounts)) ? string.Format("+sv_setsteamaccount {0} ", Configs.accounts) : "")
                        + ((!string.IsNullOrEmpty(Configs.groupids)) ? string.Format("+sv_steamgroup {0} ", Configs.groupids) : "");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Configs.srcdsPath;
                startInfo.Arguments = args;
                startInfo.UseShellExecute = false;

                Global.srcds = Process.Start(startInfo);

                Thread.Sleep(1000);
            }
            catch(Exception e)
            {
                Console.WriteLine("SRCDS start failed: {0}", e.Message);
                Console.WriteLine("Trace: {0}", e.StackTrace);
                Console.ReadKey(false);
                Environment.Exit(-4);
            }

            //IntPtr hwnd = Window.FindWindow("ConsoleWindowClass", Global.srcds.MainWindowTitle);
            //if(hwnd != IntPtr.Zero)
            //{
            //    Console.WriteLine("FindWindow -> " + hwnd);
            //    Console.WriteLine("MainWindow -> " + Global.srcds.MainWindowHandle);
            //}

            Console.WriteLine("{0} >>> Srcds Started!", DateTime.Now.ToString());
            Console.WriteLine("Start  Info: pid[{0}] path[{1}]", Global.srcds.Id, Global.srcds.MainModule.FileName);
            Console.WriteLine("CommandLine: {0}", args);
            Console.WriteLine("");
            Console.WriteLine("Commands: ");
            Console.WriteLine("show   - show srcds console window.");
            Console.WriteLine("hide   - hide srcds console window.");
            Console.WriteLine("exec   - exec command into srcds.");
            Console.WriteLine("quit   - quit srcds and application.");
            Console.WriteLine("exit   - quit srcds and application.");
            Console.WriteLine("update - force srcds update.");
            Console.Write(Environment.NewLine);

            Thread.Sleep(3000);
            Window.Hide((int)Global.srcds.MainWindowHandle);

            Global.tupdate = new Thread(Thread_UpdateCheck);
            Global.tupdate.Start();

            Global.crash = false;
            int a2stimeout = 0;

            while(true)
            {
                Thread.Sleep(1234);

                if(Global.update)
                {
                    Global.tcrash = null;
                    Global.tcrash.Abort();
                    return;
                }

                if(!A2S.Query(false))
                {
                    a2stimeout++;
                    Console.Title = "CSGO Server Manager - [TimeOut] " + Global.srcds.MainWindowTitle;
                }
                else
                {
                    a2stimeout = 0;
                    Console.Title = "CSGO Server Manager - " + Global.srcds.MainWindowTitle;
                }

                if(a2stimeout < 10)
                    continue;

                Console.WriteLine("{0} >>> SRCDS crashed!", DateTime.Now.ToString());
                Global.crash = true;
                Global.tupdate.Abort();
                Global.tcrash = null;
                break;
            }

            if(!Global.srcds.HasExited)
                Helper.KillSRCDS();

            Thread.Sleep(1000);
            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.Start();
            Thread.CurrentThread.Abort();
        }

        static void Thread_UpdateCheck()
        {
            do
            {
                if(!SteamApi.latestVersion())
                {
                    Global.update = true;
                    Global.tupdate = null;
                    Global.tcrash.Abort();
                    Global.tcrash = null;
                    new Thread(Thread_UpdateCSGO).Start();
                    break;
                }

                Thread.Sleep(120000);
            }
            while(true);
        }

        static void Thread_UpdateCSGO()
        {
            Helper.KillSRCDS();
            Console.WriteLine("{0} >>> Starting Update!", DateTime.Now.ToString());

            Thread.Sleep(4000);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Configs.steamPath;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.Arguments = "+login anonymous +force_install_dir \"" + Environment.CurrentDirectory + "\" " + "+app_update 740 +quit";

                Process process = Process.Start(startInfo);
                StreamReader outputStreamReader = process.StandardOutput;
                process.WaitForExit();
                Console.Write(Environment.NewLine);
                Console.Write(outputStreamReader.ReadToEnd());
                Console.Write(Environment.NewLine);
                process.Close();
            }
            catch(Exception e)
            {
                Console.WriteLine("Update Failed: {0}", e.Message);
            }

            Console.WriteLine("{0} >>> Update successful!", DateTime.Now.ToString());

            Thread.Sleep(3000);

            Global.update = false;
            new Thread(Thread_CheckCrashs).Start();
        }

        static void Thread_CheckToken()
        {
            while(true)
            {
                Thread.Sleep(600000);

                if(SteamApi.checkTokens() == 1)
                {
                    Global.tupdate.Abort();
                    Global.tupdate = null;

                    for(int cd = 60; cd > 0; cd--)
                    {
                        Message.Write(Global.srcds.MainWindowHandle, "Server restart in " + cd + " seconds");
                        Message.Send(Global.srcds.MainWindowHandle);
                        Thread.Sleep(1000);

                        if(Global.crash)
                            break;
                    }

                    if(Global.crash)
                        continue;

                    Message.Write(Global.srcds.MainWindowHandle, "sm_kick @all \"Server Restart\"");
                    Message.Send(Global.srcds.MainWindowHandle);

                    Helper.KillSRCDS();
                }
            }
        }
    }

    class Configs
    {
        public static string srcdsPath
        {
            get { return Get("Global", "srcdsPath", null); }
            set { Set("Global", "srcdsPath", value); }
        }

        public static string steamPath
        {
            get { return Get("Global", "steamPath", null); }
            set { Set("Global", "steamPath", value); }
        }

        public static string accounts
        {
            get { return Get("SteamWorks", "Token", null); }
            set { Set("SteamWorks", "Token", value); }
        }

        public static string groupids
        {
            get { return Get("SteamWorks", "Group", null); }
            set { Set("SteamWorks", "Group", value); }
        }

        public static string wwip
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

        public static string maxplays
        {
            get { return Get("Server", "MaxPlays", null); }
            set { Set("Server", "MaxPlays", value); }
        }

        public static string nobotsex
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

        public static string TKApikey
        {
            get { return Get("CSGOtokens.com", "ApiKey", null); }
            set { Set("CSGOtokens.com", "ApiKey", value); }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WritePrivateProfileString(string section, string key, string val, string filepath);

        [DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retval, int size, string filePath);

        public static string Get(string section, string key, string defaultValue)
        {
            StringBuilder temp = new StringBuilder(1024);
            GetPrivateProfileString(section, key, defaultValue, temp, 1024, Environment.CurrentDirectory + "\\server_config.ini");
            if(temp.ToString().Equals("null"))
                return null;
            return temp.ToString();
        }

        static bool Create(string section, string key, string val)
        {
            return WritePrivateProfileString(section, key, val, Environment.CurrentDirectory + "\\server_config.ini");
        }

        public static bool Set(string section, string key, string val)
        {
            Global.watcher.EnableRaisingEvents = false;
            bool result = WritePrivateProfileString(section, key, val, Environment.CurrentDirectory + "\\server_config.ini");
            if(result)
            {
                Backup();
            }
            Global.watcher.EnableRaisingEvents = true;
            return result;
        }

        private static void Backup()
        {
            StreamReader file = new StreamReader(Environment.CurrentDirectory + "\\server_config.ini");
            Global.backup = file.ReadToEnd();
            file.Close();
            file.Dispose();
        }

        public static void Restore()
        {
            Global.watcher.EnableRaisingEvents = false;
            StreamWriter file = new StreamWriter(Environment.CurrentDirectory + "\\server_config_backup.ini", false, Encoding.Unicode);
            file.Write(Global.backup);
            file.Close();
            File.Copy(Environment.CurrentDirectory + "\\server_config_backup.ini", Environment.CurrentDirectory + "\\server_config.ini", true);
            Global.watcher.EnableRaisingEvents = true;
        }

        public static bool Check()
        {
            if(!File.Exists(Environment.CurrentDirectory + "\\server_config.ini"))
            {
                Create("Global", "srcdsPath", Environment.CurrentDirectory + "\\srcds.exe");
                Create("Global", "steamPath", Environment.CurrentDirectory + "\\steamcmd.exe");

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

                Create("CSGOtokens.com", "ApiKey", "null");

                return false;
            }

            return true;
        }
    }

    class Window
    {
        const int SW_HIDE = 0;
        const int SW_SHOW = 1;
        const int GW_HWNDNEXT = 2; // The next window is below the specified window
        const int GW_HWNDPREV = 3; // The previous window is above

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        public extern static IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        static extern bool ShowWindow(int hwnd, int nCmdShow);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetWindow", SetLastError = true)]
        static extern IntPtr GetNextWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.U4)] int wFlag);
        [DllImport("user32.dll")]
        static extern IntPtr GetTopWindow(IntPtr hWnd);

        public static void Show(int hwnd)
        {
            ShowWindow(hwnd, SW_SHOW);
        }
        public static void Hide(int hwnd)
        {
            ShowWindow(hwnd, SW_HIDE);
        }
    }

    class Message
    {
        [DllImport("User32.dll")]
        public static extern Int32 SendMessage(IntPtr hWnd, int Msg, int wParam, [MarshalAs(UnmanagedType.LPStr)] string lParam);
        [DllImport("User32.dll", EntryPoint = "PostMessage")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public static void Write(IntPtr hWnd, string message)
        {
            char[] cs = message.ToCharArray();
            foreach(char c in cs)
            {
                PostMessage(hWnd, 0x0102, c, 0);
            }
        }

        public static bool Send(IntPtr hWnd)
        {
           return PostMessage(hWnd, 0x0100, 13, 0);
        }
    }

    class Helper
    {
        public static string GetLocalIpAddress()
        {
            string hostName = Dns.GetHostName();
            IPHostEntry IpEntry = Dns.GetHostEntry(hostName);

            for(int i = 0; i < IpEntry.AddressList.Length; i++)
            {
                if(IpEntry.AddressList[i].AddressFamily != AddressFamily.InterNetwork)
                    continue;

                string ip = IpEntry.AddressList[i].ToString();

                if(ip.StartsWith("10.") || ip.StartsWith("172.") || ip.StartsWith("192."))
                    continue;

                return ip;
            }
            return "Invalid Local Ip Adress (伺服器沒有公網IP)";
        }

        public static bool PortAvailable(int port)
        {
            return !((from p in IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners() where p.Port == port select p).Count() == 1);
        }

        public static Process GetAppByPort(int checkPort)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "netstat.exe";
            startInfo.Arguments = "-a -n -o";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            Process netstats = new Process();
            netstats.StartInfo = startInfo;
            netstats.Start();
            netstats.WaitForExit();

            StreamReader sr = netstats.StandardOutput;
            string output = sr.ReadToEnd();
            if(netstats.ExitCode != 0)
                throw new Exception("netstats ExitCode = " + netstats.ExitCode);

            string[] lines = Regex.Split(output, "\r\n");
            foreach(var line in lines)
            {
                // first line 嘻嘻
                if(line.Trim().StartsWith("Proto"))
                    continue;

                string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if(parts.Length < 2)
                    continue;

                if(!int.TryParse(parts[parts.Length - 1], out int pid))
                    continue;

                if(!int.TryParse(parts[1].Split(':').Last(), out int port))
                    continue;

                if(port != checkPort)
                    continue;

                //Console.WriteLine("Find Result: Protocol[{0}]  Port[{1}]  PID[{2}]", parts[0], port, pid);

                return Process.GetProcessById(pid);
            }

            throw new Exception("Not Found in list[" + (lines.Length-1) + "]");
        }

        public static void KillSRCDS()
        {
            if(Global.srcds.HasExited)
                return;

            Message.Write(Global.srcds.MainWindowHandle, "quit");
            Message.Send(Global.srcds.MainWindowHandle);
            Thread.Sleep(6666);

            if(!Global.srcds.HasExited)
            {
                Console.WriteLine("{0} >>> Timeout -> Force Kill SRCDS! pid[{1}]", DateTime.Now.ToString(), Global.srcds.Id);
                Global.srcds.Kill();
            }

            Global.srcds = null;
            Thread.Sleep(666);
        }

        public static void KillSRCDS(Process srcds)
        {
            Message.Write(srcds.MainWindowHandle, "quit");
            Message.Send(srcds.MainWindowHandle);
            Thread.Sleep(6666);

            if(!srcds.HasExited)
            {
                Console.WriteLine("{0} >>> Timeout -> Force Kill SRCDS! pid[{1}]", DateTime.Now.ToString(), srcds.Id);
                srcds.Kill();
            }

            Thread.Sleep(666);
        }

        public static void WatchFile()
        {
            Global.watcher = new FileSystemWatcher(Environment.CurrentDirectory, "server_config.ini");
            Global.watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName;
            Global.watcher.Changed += new FileSystemEventHandler(ConfigFile_OnChanged);
            Global.watcher.Created += new FileSystemEventHandler(ConfigFile_OnChanged);
            Global.watcher.Deleted += new FileSystemEventHandler(ConfigFile_OnChanged);
            Global.watcher.Renamed += new RenamedEventHandler(ConfigFile_OnRenamed);
            Global.watcher.EnableRaisingEvents = true;
        }

        private static void ConfigFile_OnRenamed(object sender, RenamedEventArgs e)
        {
            Console.WriteLine("{0} >>> Detected: Configs file 'server_config.ini' was deleted.", DateTime.Now.ToString());
            Console.WriteLine("If you want to delete 'server_config.ini', please quit the application first.", DateTime.Now.ToString());
            Configs.Restore();
        }

        private static void ConfigFile_OnChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} >>> Detected: Configs file 'server_config.ini' was {1}.", DateTime.Now.ToString(), e.ChangeType.ToString().ToLower());
            Console.WriteLine("If you want to edit 'server_config.ini', please quit the application first.", DateTime.Now.ToString());
            Configs.Restore();
        }
    }

    class A2S
    {
        static byte[] request = new byte[9] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF };
        static Socket serverSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        public static bool Query(bool start)
        {
            serverSock.SendTimeout = 100;
            serverSock.ReceiveTimeout = 100;
            try
            {
                serverSock.SendTo(request, new IPEndPoint(IPAddress.Parse(Configs.wwip), int.Parse(Configs.port)));
            }
            catch(Exception e)
            {
                Console.WriteLine("{0} >>> A2S Send Failed: {1}", DateTime.Now.ToShortTimeString(), e.Message);
                return false;
            }

            byte[] serverResponse = new byte[9];
            try
            {
                serverSock.Receive(serverResponse);
            }
            catch(Exception e)
            {
                if(!start)
                {
                    Console.WriteLine("{0} >>> A2S Recv Failed: {1}", DateTime.Now.ToShortTimeString(), e.Message);
                }
                return false;
            }

            return true;
        }
    }

    class SteamApi
    {
        static WebClient http = new WebClient();

        private static string getVersion()
        {
            StreamReader sr = new StreamReader(Environment.CurrentDirectory + "\\csgo\\steam.inf");
            string line = string.Empty;
            while((line = sr.ReadLine()) != null)
            {
                if(!line.StartsWith("PatchVersion"))
                    continue;

                return line.Replace("PatchVersion=", "");
            }
            return "0.0.0.0";
        }

        public static bool latestVersion()
        {
            string uri = "https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=730&version=" + getVersion() + "&format=json";
            try
            {
                string result = http.DownloadString(new Uri(uri));
                if(!result.Contains("\"success\":true"))
                {
                    Console.WriteLine("{0} >>> SteamApi Failed: {1}", DateTime.Now.ToShortTimeString(), result);
                    return true;
                }
                return result.Equals("{\"response\":{\"success\":true,\"up_to_date\":true,\"version_is_listable\":true}}");
            }
            catch(Exception e)
            {
                Console.WriteLine("{0} >>> SteamApi Failed: {1}", DateTime.Now.ToShortTimeString(), e.Message);
            }

            return true;
        }

        public static int checkTokens(bool consoleLog = false)
        {
            string result = null;
            try
            {
                result = http.DownloadString(new Uri("https://csgotokens.com/token-api.php?ip=" + Configs.wwip + ":" + Configs.port + "&key=" + Configs.TKApikey));

                if(consoleLog)
                {
                    Console.WriteLine("{0} >>> TokenApi result -> {1}", DateTime.Now.ToString(), result);
                }

                if(result.Equals(Configs.accounts))
                {
                    if(consoleLog)
                    {
                        Console.WriteLine("{0} >>> TokenApi -> Token status is OK.", DateTime.Now.ToString());
                    }
                    return 2;
                }
                else
                {
                    if(result.Length == 32)
                    {
                        Console.WriteLine("{0} >>> Token was banned -> old token [{1}] -> new token [{2}]", DateTime.Now.ToString(), Configs.accounts, result);
                        Configs.accounts = result;
                        return 1;
                    }
                    else
                    {
                        Console.WriteLine("{0} >>> TokenApi Response: {1}", DateTime.Now.ToString(), result);
                        return 0;
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("{0} >>> TokenApi Exception: {0}", DateTime.Now.ToString(), e.Message);
                return -2;
            }
        }
    }
}
