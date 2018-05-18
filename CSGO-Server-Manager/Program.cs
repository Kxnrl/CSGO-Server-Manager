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

//https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=730&version={version}&format=vdf

namespace CSGO_Server_Manager
{
    class Global
    {
        public static string WalkPath = null;
        public static Process srcds = null;
        public static bool update = false;
        public static bool crash = false;
        public static string args = null;
        public static Thread tcrash = null;
        public static Thread tupdate = null;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "CSGO Server Manager v1.0";

            Console.WriteLine("");
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

            Global.WalkPath = Environment.CurrentDirectory;
            if (!Configs.Check())
            {
                MessageBox.Show("Please check your game server config\n", "Fatal Error");
                Environment.Exit(-1);
            }

            string srcdspath = Configs.ContentValue("Global", "srcdsPath", Global.WalkPath + "\\srcds.exe");
            if (!File.Exists(srcdspath))
            {
                MessageBox.Show("Please check your path of SRCDS\n", "Fatal Error");
                Environment.Exit(-2);
            }

            Process[] process = Process.GetProcessesByName("srcds");
            foreach (Process exe in process)
            {
                if (exe.MainModule.FileName.Equals(srcdspath))
                {
                    Console.WriteLine("{0} >>> Force Srcds Quit! pid[{1}] path[{2}]", DateTime.Now.ToString(), exe.Id, exe.MainModule.FileName);
                    Helper.ExitSRCDS(exe.MainWindowHandle);
                    Thread.Sleep(5000);
                    if(!exe.HasExited)
                    {
                        Console.WriteLine("{0} >>> Force Srcds Kill! pid[{1}]", DateTime.Now.ToString(), exe.Id);
                        exe.Kill();
                    }
                    break;
                }
            }

            string accounts = Configs.ContentValue("SteamWorks", "Token", null);
            string groupids = Configs.ContentValue("SteamWorks", "Group", null);

            string ip = Configs.ContentValue("Server", "IP", null);
            if (string.IsNullOrEmpty(ip))
            {
                do
                {
                    MessageBox.Show("Please input your Game Server IP\n", "Fatal Error");
                    ip = Console.ReadLine();
                    Configs.Write("Server", "IP", ip);
                }
                while (!IPAddress.TryParse(ip, out IPAddress ipadr));
            }

            string Port = Configs.ContentValue("Server", "Port", null);
            if (string.IsNullOrEmpty(Port) || !int.TryParse(Port, out int port))
            {
                do
                {
                    MessageBox.Show("Please input your Game Server Port (27000 - 27099) \n", "Fatal Error");
                    Port = Console.ReadLine();
                    Configs.Write("Server", "IP", Port);
                }
                while (!int.TryParse(Port, out port));
            }

            if (A2S.Query(true))
            {
                Console.WriteLine("{0} >>> Port[{1}] has been used...", DateTime.Now.ToString(), port);
                Console.ReadKey(false);
            }

            string insecure = Configs.ContentValue("Server", "Insecure", null);
            string tickrate = Configs.ContentValue("Server", "TickRate", null);
            string maxplays = Configs.ContentValue("Server", "MaxPlays", null);
            string nobotsex = Configs.ContentValue("Server", "NoBotsEx", null);
            string gametype = Configs.ContentValue("Server", "GameType", null);
            string gamemode = Configs.ContentValue("Server", "GameMode", null);
            string mapgroup = Configs.ContentValue("Server", "MapGroup", null);
            string startmap = Configs.ContentValue("Server", "StartMap", null);

            Global.args = "-console -game csgo" + " "
                        + "-ip " + ip + " "
                        + "-port " + port + " "
                        + ((!string.IsNullOrEmpty(insecure) && int.TryParse(insecure, out int novalveac) && novalveac == 1) ? "-insecure " : " ")
                        + ((!string.IsNullOrEmpty(tickrate) && int.TryParse(tickrate, out int TickRate)) ? string.Format("-tickrate {0} ", TickRate) : " ")
                        + ((!string.IsNullOrEmpty(maxplays) && int.TryParse(maxplays, out int maxPlays)) ? string.Format("-maxplayers_override {0} ", maxPlays) : " ")
                        + ((!string.IsNullOrEmpty(nobotsex) && int.TryParse(nobotsex, out int nobots) && nobots == 1) ? "-nobots " : " ")
                        + ((!string.IsNullOrEmpty(gametype) && int.TryParse(gametype, out int gameType)) ? string.Format("+gametype {0} ", gameType) : " ")
                        + ((!string.IsNullOrEmpty(gamemode) && int.TryParse(gamemode, out int gameMode)) ? string.Format("+gamemode {0} ", gameMode) : " ")
                        + ((!string.IsNullOrEmpty(mapgroup)) ? string.Format("+mapgroup {0} ", mapgroup) : " ")
                        + ((!string.IsNullOrEmpty(startmap)) ? string.Format("+map {0} ", startmap) : " ")
                        + ((!string.IsNullOrEmpty(accounts)) ? string.Format("+sv_setsteamaccount {0} ", accounts) : " ")
                        + ((!string.IsNullOrEmpty(groupids)) ? string.Format("+sv_steamgroup {0} ", groupids) : " ");


            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.Start();

            Thread.Sleep(3000);

            while (true)
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
                switch (input.ToLower())
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
                        if (cmds.Length > 1)
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
                        Helper.ExitSRCDS();
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
                        if (input.StartsWith("exec "))
                        {
                            string cmd = input.Replace("exec ", "");
                            if (cmd.Length > 1)
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
                            Console.WriteLine("quit   - quit srcds and close app.");
                            Console.WriteLine("update - force srcds update.");
                        }
                        break;
                }
            }
        }

        static void Thread_CheckCrashs()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Configs.ContentValue("Global", "srcdsPath", Global.WalkPath + "\\srcds.exe");
                startInfo.Arguments = Global.args;
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
            //if (hwnd != IntPtr.Zero)
            //{
            //    Console.WriteLine("FindWindow -> " + hwnd);
            //    Console.WriteLine("MainWindow -> " + Global.srcds.MainWindowHandle);
            //}

            Console.WriteLine("{0} >>> Srcds Started!", DateTime.Now.ToString());
            Console.WriteLine("Start  Info: pid[{0}] path[{1}]", Global.srcds.Id, Global.srcds.MainModule.FileName);
            Console.WriteLine("CommandLine: {0}", Global.args);
            Console.WriteLine("Commands: ");
            Console.WriteLine("show   - show srcds console window.");
            Console.WriteLine("hide   - hide srcds console window.");
            Console.WriteLine("exec   - exec command into srcds.");
            Console.WriteLine("quit   - quit srcds and close app.");
            Console.WriteLine("update - force srcds update.");
            Console.Write(Environment.NewLine);

            Thread.Sleep(3000);
            Window.Hide((int)Global.srcds.MainWindowHandle);

            Global.tupdate = new Thread(Thread_UpdateCheck);
            Global.tupdate.Start();

            Global.crash = false;
            int a2stimeout = 0;

            while (true)
            {
                Thread.Sleep(1234);

                if (Global.update)
                    break;

                if (!A2S.Query(false))
                {
                    a2stimeout++;
                    Console.Title = "CSGO Server Manager - [TimeOut] " + Global.srcds.MainWindowTitle;
                }
                else
                {
                    a2stimeout = 0;
                    Console.Title = "CSGO Server Manager - " + Global.srcds.MainWindowTitle;
                }

                if (a2stimeout < 10)
                    continue;

                Console.WriteLine("{0} >>> SRCDS crashed!", DateTime.Now.ToString());
                Global.crash = true;
                Global.tupdate.Abort();
                Global.tcrash = null;
                break;
            }

            if (!Global.crash)
                return;

            if (!Global.srcds.HasExited)
                Helper.ExitSRCDS();

            Thread.Sleep(1000);
            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.Start();
            Thread.CurrentThread.Abort();
        }

        static void Thread_UpdateCheck()
        {
            do
            {
                if (!SteamApi.latestVersion())
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
            while (true);
        }

        static void Thread_UpdateCSGO()
        {
            Helper.ExitSRCDS();
            Console.WriteLine("{0} >>> Starting Update!", DateTime.Now.ToString());

            Thread.Sleep(4000);

            string steamcmd = Configs.ContentValue("Global", "steamcmds", "null");
            if(!File.Exists(steamcmd))
            {
                MessageBox.Show("SteamCmd.exe does not exists!", "Fatal Error");
                Environment.Exit(-3);
            }

            Thread.Sleep(2000);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = steamcmd;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.Arguments = "+login anonymous +force_install_dir \"" + Global.WalkPath + "\" " + "+app_update 740 +quit";

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
    }

    class Configs
    {
        [DllImport("kernel32.dll")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filepath);

        [DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retval, int size, string filePath);

        public static string ContentValue(string section, string key, string defaultValue)
        {
            StringBuilder temp = new StringBuilder(1024);
            GetPrivateProfileString(section, key, defaultValue, temp, 1024, Global.WalkPath + "\\server_config.ini");
            if(temp.ToString().Equals("null"))
                return null;
            return temp.ToString();
        }

        public static void Write(string section, string key, string val)
        {
            WritePrivateProfileString(section, key, val, Global.WalkPath + "\\server_config.ini");
        }

        public static bool Check()
        {
            if (!File.Exists(Global.WalkPath + "\\server_config.ini"))
            {
                Write("Global", "srcdsPath", Global.WalkPath + "\\srcds.exe");
                Write("Global", "steamCmds", Global.WalkPath + "\\steamcmd.exe");

                Write("SteamWorks", "Token", "null");
                Write("SteamWorks", "Group", "null");

                Write("Server", "IP", Helper.GetLocalIpAddress());
                Write("Server", "Port", "27015");
                Write("Server", "Insecure", "0");
                Write("Server", "TickRate", "128");
                Write("Server", "MaxPlays", "64");
                Write("Server", "NoBotsEx", "0");
                Write("Server", "GameType", "0");
                Write("Server", "GameMode", "0");
                Write("Server", "MapGroup", "custom_maps");
                Write("Server", "StartMap", "de_dust2");

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

        public static void ExitSRCDS()
        {
            if(Global.srcds.HasExited)
                return;

            Message.Write(Global.srcds.MainWindowHandle, "quit");
            Message.Send(Global.srcds.MainWindowHandle);
            Thread.Sleep(2000);
            Global.srcds = null;
        }

        public static void ExitSRCDS(IntPtr srcds)
        {
            Message.Write(srcds, "quit");
            Message.Send(srcds);
            Thread.Sleep(2333);
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
                serverSock.SendTo(request, new IPEndPoint(IPAddress.Parse(Configs.ContentValue("Server", "IP", null)), int.Parse(Configs.ContentValue("Server", "Port", null))));
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
            catch (Exception e)
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
            StreamReader sr = new StreamReader(Global.WalkPath + "\\csgo\\steam.inf");
            string line = string.Empty;
            while ((line = sr.ReadLine()) != null)
            {
                if (!line.StartsWith("PatchVersion"))
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
                if (!result.Contains("\"success\":true"))
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
    }
}
