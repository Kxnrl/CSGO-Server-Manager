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
using Microsoft.Win32;

//https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=730&version={version}&format=vdf

namespace CSGO_Server_Manager
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
    }

    class Program
    {
        class tray
        {
            public static ContextMenu notifyMenu;
            public static NotifyIcon notifyIcon;
            public static MenuItem showHide;
            public static MenuItem exitButton;
        }

        public static int myHandle = 0;

        [STAThread]
        static void Main(string[] args)
        {
            // check run once
            Mutex self = new Mutex(true, Application.StartupPath.GetHashCode().ToString(), out bool allow);
            if (!allow)
            {
                MessageBox.Show("CSM is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            myHandle = (int)Process.GetCurrentProcess().MainWindowHandle;

            // Event
            Application.ThreadException += new ThreadExceptionEventHandler(ExceptionHandler_CurrentThread);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler_AppDomain);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(ApplicationHandler_OnExit);
            SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(ApplicationHandler_PowerModeChanged);
            ConsoleCTRL.ConsoleClosed(new ConsoleCTRL.HandlerRoutine(ApplicationHandler_OnClose));
            PowerMode.NoSleep();

            Console.Title = "CSGO Server Manager v1.1.4";

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

            if(!Configs.Check())
            {
                Console.WriteLine("{0} >>> Configs was initialized -> You can modify it manually!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            }

            A2S.CheckFirewall();
            Logger.Create();
            Helper.WatchFile();

            while(!File.Exists(Configs.srcdsPath))
            {
                using (OpenFileDialog fileBrowser = new OpenFileDialog())
                {
                    fileBrowser.Multiselect = false;
                    fileBrowser.Filter = "CSGO Dedicated Server (srcds.exe)|srcds.exe";

                    if(fileBrowser.ShowDialog() != DialogResult.OK)
                    {
                        MessageBox.Show("Application Exit!\nYou can modify it manually!", "CSGO Server Manager");
                        Environment.Exit(0);
                    }
                    else
                    {
                        Configs.srcdsPath = fileBrowser.FileName;
                        Console.WriteLine("{0} >>> Set SRCDS path -> {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Configs.srcdsPath);
                    }
                }
            }

            while(!File.Exists(Configs.steamPath))
            {
                using (OpenFileDialog fileBrowser = new OpenFileDialog())
                {
                    fileBrowser.Multiselect = false;
                    fileBrowser.Filter = "SteamCmd (steamcmd.exe)|steamcmd.exe";

                    if (fileBrowser.ShowDialog() != DialogResult.OK)
                    {
                        MessageBox.Show("Application Exit!\nYou can modify it manually!", "CSGO Server Manager");
                        Environment.Exit(0);
                    }
                    else
                    {
                        Configs.steamPath = fileBrowser.FileName;
                        Console.WriteLine("{0} >>> Set Steam path -> {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Configs.steamPath);
                    }
                }
            }

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

            Global.ipep = new IPEndPoint(ipadr, port);

            if(!Helper.PortAvailable(port))
            {
                Console.WriteLine("{0} >>> Port[{1}] is unavailable! Finding Application...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), port);

                try
                {
                    Process exe = Helper.GetAppByPort(port);
                    Console.WriteLine("{0} >>> Trigger SRCDS Quit -> App[{1}] PID[{2}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), exe.MainWindowTitle, exe.Id);
                    Helper.KillSRCDS(exe);
                }
                catch(Exception e)
                {
                    Console.WriteLine("{0} >>> Not found Application: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.Message);
                }
            }

            Process[] process = Process.GetProcessesByName("srcds");
            foreach (Process exe in process)
            {
                if (exe.MainModule.FileName.Equals(Configs.srcdsPath))
                {
                    exe.Kill();
                    Console.WriteLine("{0} >>> Force close old srcds before new srcds start.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                }
            }
            Console.WriteLine("{0} >>> {1} SRCDS are running on current host.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), process.Length);

            if (!string.IsNullOrEmpty(Configs.TKApikey))
            {
                while(TokenApi.CheckTokens(true) <= 0)
                {
                    Console.WriteLine("{0} >>> TokenApi -> Checking ...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                }
                Console.WriteLine("{0} >>> TokenApi -> feature is available.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                new Thread(Thread_CheckToken).Start();
            }
            else
            {
                Console.WriteLine("{0} >>> TokenApi -> ApiKey was not found.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            }

            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.IsBackground = true;
            Global.tcrash.Name = "Crash Thread";
            Global.tcrash.Start();

            Thread.Sleep(8000);

            new Thread(
                delegate ()
                {
                    tray.notifyMenu = new ContextMenu();
                    tray.showHide = new MenuItem("Show");
                    tray.exitButton = new MenuItem("Exit");
                    tray.notifyMenu.MenuItems.Add(0, tray.showHide);
                    tray.notifyMenu.MenuItems.Add(1, tray.exitButton);

                    tray.notifyIcon = new NotifyIcon()
                    {
                        BalloonTipIcon = ToolTipIcon.Info,
                        ContextMenu = tray.notifyMenu,
                        Text = "CSGO Server Manager",
                        Icon = Properties.Resources.icon,
                        Visible = true,
                    };

                    tray.showHide.Click += new EventHandler(ApplicationHandler_TrayIcon);
                    tray.exitButton.Click += new EventHandler(ApplicationHandler_TrayIcon);

                    Application.Run();
                }
            ).Start();

            Thread.Sleep(1000);

            tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";
            tray.notifyIcon.BalloonTipText = "Server Started!";
            tray.notifyIcon.ShowBalloonTip(5000);
            Window.Hide(myHandle);
            currentShow = false;

            string input;
            while (true)
            {
                input = Console.ReadLine();

                if(Global.update)
                {
                    Console.WriteLine("Updating ...");
                    continue;
                }

                if(Global.crash)
                {
                    Console.WriteLine("Restarting ...");
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
                    case "quit":
                        Environment.Exit(0);
                        break;
                    case "exit":
                        Environment.Exit(0);
                        break;
                    case "update":
                        for(int cd = 60; cd > 0; cd--)
                        {
                            Console.WriteLine("Server restart in " + cd + " seconds");
                            Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                            Message.Send(Global.srcds.MainWindowHandle);
                            Thread.Sleep(1000);

                            if(Global.crash)
                                break;
                        }
                        Console.WriteLine("Begin update.");
                        Global.update = true;
                        Global.tupdate.Abort();
                        Global.tupdate = null;
                        new Thread(Thread_UpdateCSGO).Start();
                        break;
                    case "restart":
                        Console.WriteLine("Trigger server restart.");
                        Global.tupdate.Abort();
                        Global.tcrash.Abort();
                        Global.tupdate = null;
                        Global.tcrash = null;
                        Helper.KillSRCDS(true);
                        Global.tcrash = new Thread(Thread_CheckCrashs);
                        Global.tcrash.IsBackground = true;
                        Global.tcrash.Name = "Crash Thread";
                        Global.tcrash.Start();
                        break;
                    default:
                        if(input.StartsWith("exec "))
                        {
                            input = input.Replace("exec ", "");
                            if(input.Length > 1)
                            {
                                Message.Write(Global.srcds.MainWindowHandle, input);
                                Message.Send(Global.srcds.MainWindowHandle);
                                Console.WriteLine("Execute server command: {0}", input);
                            }
                            else
                            {
                                Console.WriteLine("Command is invalid.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Commands: ");
                            Console.WriteLine("show    - show srcds console window.");
                            Console.WriteLine("hide    - hide srcds console window.");
                            Console.WriteLine("exec    - exec command into srcds.");
                            Console.WriteLine("quit    - quit srcds and application.");
                            Console.WriteLine("exit    - quit srcds and application.");
                            Console.WriteLine("update  - force srcds update.");
                            Console.WriteLine("restart - force srcds restart.");
                        }
                        break;
                }
            }
        }

        private static bool currentShow = true;
        private static void ApplicationHandler_TrayIcon(object sender, EventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            if(item == tray.exitButton)
            {
                tray.notifyIcon.Visible = false;
                tray.notifyIcon.Dispose();
                Thread.Sleep(50);
                Environment.Exit(0);
            }
            else if(item == tray.showHide)
            {
                tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";

                if (currentShow)
                {
                    currentShow = false;
                    Window.Hide(myHandle);
                    tray.showHide.Text = "Show";
                    tray.notifyIcon.BalloonTipText = "Hide Window, Click icon to recovery window";
                    if(Global.srcds != null && !Global.srcds.HasExited)
                    {
                        Window.Hide(Global.srcds.MainWindowHandle.ToInt32());
                    }
                }
                else
                {
                    currentShow = true;
                    Window.Show(myHandle);
                    tray.showHide.Text = "Hide";
                    tray.notifyIcon.BalloonTipText = "Show Window, Click icon to hide window";
                }
                tray.notifyIcon.ShowBalloonTip(5000);
            }
        }

        static void ApplicationHandler_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            SystemEvents.PowerModeChanged -= ApplicationHandler_PowerModeChanged;

            if (e.Mode == PowerModes.StatusChange)
            {
                Process.Start("powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                Thread.Sleep(5000);
            }

            SystemEvents.PowerModeChanged += ApplicationHandler_PowerModeChanged;
        }

        static bool ApplicationHandler_OnClose(ConsoleCTRL.CtrlTypes CtrlType)
        {
            if (CtrlType == ConsoleCTRL.CtrlTypes.CTRL_CLOSE_EVENT || CtrlType == ConsoleCTRL.CtrlTypes.CTRL_SHUTDOWN_EVENT)
            {
                if (Global.tcrash != null)
                {
                    Global.tcrash.Abort();
                }
                if (Global.tupdate != null)
                {
                    Global.tupdate.Abort();
                }

                Helper.KillSRCDS(false);
                Logger.Log("Exit by closing.");
            }

            return true;
        }

        static void ApplicationHandler_OnExit(object sender, EventArgs e)
        {
            if(Global.tcrash != null)
            {
                Global.tcrash.Abort();
            }
            if(Global.tupdate != null)
            {
                Global.tupdate.Abort();
            }
 
            Helper.KillSRCDS(false);
            Logger.Log("Exit by others.");
        }

        static void ExceptionHandler_AppDomain(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = args.ExceptionObject as Exception;
            Logger.Error("\n----------------------------------------\nThread: "+ Thread.CurrentThread.Name +"\nException: " + e.GetType() + "\nMessage: " + e.Message + "\nStackTrace:\n" + e.StackTrace);
        }

        static void ExceptionHandler_CurrentThread(object sender, ThreadExceptionEventArgs args)
        {
            Exception e = args.Exception;
            Logger.Error("\n----------------------------------------\nThread: " + Thread.CurrentThread.Name + "\nException: " + e.GetType() + "\nMessage: " + e.Message + "\nStackTrace:\n" + e.StackTrace);
        }

        static void Thread_CheckCrashs()
        {
            Thread.Sleep(500);

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
                Global.srcds = new Process();
                Global.srcds.StartInfo.FileName = Configs.srcdsPath;
                Global.srcds.StartInfo.Arguments = args;
                Global.srcds.StartInfo.UseShellExecute = false;
                Global.srcds.EnableRaisingEvents = true;
                Global.srcds.Start();
                Global.srcds.Exited += new EventHandler(Srcds_OnExited);

                Thread.Sleep(1000);
            }
            catch(Exception e)
            {
                Console.WriteLine("SRCDS start failed: {0}", e.Message);
                Console.WriteLine("StackTrace:{0}{1}", Environment.NewLine, e.StackTrace);
                Console.ReadKey(false);
                Environment.Exit(-4);
            }

            //IntPtr hwnd = Window.FindWindow("ConsoleWindowClass", Global.srcds.MainWindowTitle);
            //if(hwnd != IntPtr.Zero)
            //{
            //    Console.WriteLine("FindWindow -> " + hwnd);
            //    Console.WriteLine("MainWindow -> " + Global.srcds.MainWindowHandle);
            //}

            Logger.Log("Srcds Started!");

            Console.WriteLine("{0} >>> Srcds Started!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            Console.WriteLine("Start  Info: pid[{0}] path[{1}]", Global.srcds.Id, Global.srcds.MainModule.FileName);
            Console.WriteLine("CommandLine: {0}", args);
            Console.WriteLine("");
            Console.WriteLine("Commands: ");
            Console.WriteLine("show    - show srcds console window.");
            Console.WriteLine("hide    - hide srcds console window.");
            Console.WriteLine("exec    - exec command into srcds.");
            Console.WriteLine("quit    - quit srcds and application.");
            Console.WriteLine("exit    - quit srcds and application.");
            Console.WriteLine("update  - force srcds update.");
            Console.WriteLine("restart - force srcds restart.");
            Console.Write(Environment.NewLine);

            Thread.Sleep(5000);
            Window.Hide(Global.srcds.MainWindowHandle.ToInt32());

            Global.tupdate = new Thread(Thread_UpdateCheck);
            Global.tupdate.IsBackground = true;
            Global.tupdate.Name = "Update Thread";
            Global.tupdate.Start();

            Global.crash = false;
            uint a2stimeout = 0;

            while(true)
            {
                Thread.Sleep(2000);

                if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.AbortRequested || Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                    return;

                if(Global.update)
                {
                    Global.tcrash = null;
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
                    if(Global.srcds.MainWindowTitle.Length > 5)
                    {
                        Console.Title = "CSGO Server Manager - " + Global.srcds.MainWindowTitle;
                        tray.notifyIcon.Text = Global.srcds.MainWindowTitle;
                    }
                }

                if(a2stimeout < 10)
                    continue;

                tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";
                tray.notifyIcon.BalloonTipText = "Srcds crashed!";
                tray.notifyIcon.ShowBalloonTip(5000);
                Logger.Log("Srcds crashed!");
                Console.WriteLine("{0} >>> SRCDS crashed!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                Global.crash = true;
                Global.tupdate.Abort();
                Global.tcrash = null;
                break;
            }

            if (!Global.srcds.HasExited)
                Global.srcds.Kill();

            Thread.Sleep(1500);
            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.IsBackground = true;
            Global.tcrash.Name = "Crash Thread";
            Global.tcrash.Start();
        }

        private static void Srcds_OnExited(object sender, EventArgs e)
        {
            tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";
            tray.notifyIcon.BalloonTipText = "Srcds crashed!";
            tray.notifyIcon.ShowBalloonTip(5000);
            Logger.Log("Srcds crashed!");
            Console.WriteLine("{0} >>> SRCDS crashed!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            Global.crash = true;
            Global.tcrash.Abort();
            Global.tupdate.Abort();
            Global.tcrash = null;

            Global.srcds.Close();
            Global.srcds.Dispose();
            Global.srcds = null;

            Thread.Sleep(1500);
            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.IsBackground = true;
            Global.tcrash.Name = "Crash Thread";
            Global.tcrash.Start();
        }

        static void Thread_UpdateCheck()
        {
            do
            {
                if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.AbortRequested || Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                    return;

                if (!SteamApi.GetLatestVersion())
                {
                    for(int cd = 60; cd > 0; cd--)
                    {
                        Console.WriteLine("Server restart in " + cd + " seconds");
                        Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                        Message.Send(Global.srcds.MainWindowHandle);
                        Thread.Sleep(1000);

                        if(Global.crash)
                            break;
                    }
                    Global.update = true;
                    Global.tupdate = null;
                    new Thread(Thread_UpdateCSGO).Start();
                    return;
                }

                Thread.Sleep(300000);
            }
            while(true);
        }

        static void Thread_UpdateCSGO()
        {
            Thread.CurrentThread.Name = "Updating Thread";
            Thread.Sleep(500);

            Helper.KillSRCDS(true);
            Console.WriteLine("{0} >>> Starting Update!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            Logger.Log("Srcds begin update!");

            try
            {
                Console.Write(Environment.NewLine);

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = Configs.steamPath;
                    process.StartInfo.Arguments = "+login anonymous +force_install_dir \"" + Environment.CurrentDirectory + "\" " + "+app_update 740 +exit";
                    process.Start();
                    process.WaitForExit();
                    /*StreamReader reader = process.StandardOutput;
                    string line = reader.ReadLine();
                    Console.WriteLine(line);
                    while(!reader.EndOfStream)
                    {
                        line = reader.ReadLine();
                        Console.WriteLine(line);
                        if(line.ToLower().StartsWith("error!"))
                            throw new Exception("Update Error: " + line);
                    }
                    Console.Write(Environment.NewLine);*/
                }

                Console.WriteLine("{0} >>> Update successful!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                Logger.Log("Update successful!");
            }
            catch(Exception e)
            {
                Console.WriteLine("Update Failed: {0}", e.Message);
            }
            finally
            {
                Thread.Sleep(1000);

                Global.update = false;
                Global.tcrash = new Thread(Thread_CheckCrashs);
                Global.tcrash.IsBackground = true;
                Global.tcrash.Name = "Crash Thread";
                Global.tcrash.Start();
            }
        }

        static void Thread_CheckToken()
        {
            Thread.CurrentThread.IsBackground = true;
            Thread.CurrentThread.Name = "Token Thread";

            while (true)
            {
                Thread.Sleep(1200000);

                if(TokenApi.CheckTokens() == 1)
                {
                    Global.tupdate.Abort();
                    Global.tupdate = null;

                    Logger.Log("Update successful!");

                    for (int cd = 60; cd > 0; cd--)
                    {
                        Console.WriteLine("Server restart in " + cd + " seconds");
                        Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                        Message.Send(Global.srcds.MainWindowHandle);
                        Thread.Sleep(1000);

                        if(Global.crash)
                            break;
                    }

                    if(Global.crash)
                        continue;

                    Global.tcrash.Abort();
                    Global.tcrash = null;
                    Helper.KillSRCDS(true);

                    Thread.Sleep(1000);

                    Global.tcrash = new Thread(Thread_CheckCrashs);
                    Global.tcrash.IsBackground = true;
                    Global.tcrash.Name = "Crash Thread";
                    Global.tcrash.Start();
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
            if(WritePrivateProfileString(section, key, val, Environment.CurrentDirectory + "\\server_config.ini"))
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
        private const uint SW_HIDE = 0;
        private const uint SW_SHOW = 1;
        private const uint GW_HWNDNEXT = 2; // The next window is below the specified window
        private const uint GW_HWNDPREV = 3; // The previous window is above

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        public extern static IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        static extern bool ShowWindow(int hwnd, uint nCmdShow);
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

    class ConsoleCTRL
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        public static void ConsoleClosed(HandlerRoutine Handler)
        {
            SetConsoleCtrlHandler(Handler, true);
        }

        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
    }

    class PowerMode
    {
        private const uint ES_CONTINUOUS      = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        public static void NoSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
        }
    }

    class Message
    {
        [DllImport("User32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);
        [DllImport("User32.dll", EntryPoint = "PostMessage")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private static byte[] bytes = new byte[1000 * 256];
        public static void Write(IntPtr hWnd, string message)
        {
            bytes = Encoding.Unicode.GetBytes(message);
            foreach(byte b in bytes)
            {
                SendMessage(hWnd, 0x0102, b, 0);
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
            IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());

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
            using (Process netstats = new Process())
            {
                netstats.StartInfo.FileName = "netstat.exe";
                netstats.StartInfo.Arguments = "-a -n -o";
                netstats.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                netstats.StartInfo.UseShellExecute = false;
                netstats.StartInfo.RedirectStandardInput = true;
                netstats.StartInfo.RedirectStandardOutput = true;
                netstats.StartInfo.RedirectStandardError = true;
                netstats.Start();
                netstats.WaitForExit(1000);

                using (StreamReader sr = netstats.StandardOutput)
                {
                    string output = sr.ReadToEnd();
                    if (netstats.ExitCode != 0)
                        throw new Exception("netstats ExitCode = " + netstats.ExitCode);

                    string[] lines = Regex.Split(output, "\r\n");
                    foreach (var line in lines)
                    {
                        // first line 嘻嘻
                        if (line.Trim().StartsWith("Proto"))
                            continue;

                        string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length < 2)
                            continue;

                        if (!int.TryParse(parts[parts.Length - 1], out int pid))
                            continue;

                        if (!int.TryParse(parts[1].Split(':').Last(), out int port))
                            continue;

                        if (port != checkPort)
                            continue;

                        //Console.WriteLine("Find Result: Protocol[{0}]  Port[{1}]  PID[{2}]", parts[0], port, pid);

                        return Process.GetProcessById(pid);
                    }
                }
            }

            throw new Exception("Not Found in list [netstat.exe]");
        }

        public static void KillSRCDS(bool kickPlayer)
        {
            if(Global.srcds == null || Global.srcds.HasExited)
                return;

            Global.srcds.Refresh();

            if (kickPlayer)
            {
                Message.Write(Global.srcds.MainWindowHandle, "sm_kick @all \"Server Restart\"");
                Message.Send(Global.srcds.MainWindowHandle);
                Thread.Sleep(2000);
            }

            Message.Write(Global.srcds.MainWindowHandle, "quit");
            Message.Send(Global.srcds.MainWindowHandle);

            uint sec = 0;
            while(!Global.srcds.HasExited)
            {
                Thread.Sleep(1000);
                if(++sec >= 5)
                {
                    Console.WriteLine("{0} >>> Timeout -> Force Kill SRCDS! pid[{1}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Global.srcds.Id);
                    Global.srcds.Kill();
                    break;
                }
            }

            Global.srcds.Close();
            Global.srcds.Dispose();
            Global.srcds = null;
            Thread.Sleep(500);
        }

        public static void KillSRCDS(Process srcds)
        {
            Message.Write(srcds.MainWindowHandle, "quit");
            Message.Send(srcds.MainWindowHandle);

            srcds.Refresh();

            uint sec = 0;
            while(!srcds.HasExited)
            {
                Thread.Sleep(1000);
                if(++sec >= 5)
                {
                    Console.WriteLine("{0} >>> Timeout -> Force Kill SRCDS! pid[{1}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), srcds.Id);
                    srcds.Kill();
                    break;
                }
            }

            srcds.Close();
            srcds.Dispose();
            Thread.Sleep(500);
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
            Console.WriteLine("{0} >>> Detected: Configs file 'server_config.ini' was deleted.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            Console.WriteLine("If you want to delete 'server_config.ini', please quit the application first.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            Configs.Restore();
        }

        private static void ConfigFile_OnChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("{0} >>> Detected: Configs file 'server_config.ini' was {1}.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.ChangeType.ToString().ToLower());
            Console.WriteLine("If you want to edit 'server_config.ini', please quit the application first.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            Configs.Restore();
        }
    }

    class A2S
    {
        private static readonly byte[] request_a2sping = new byte[9] { 0xFF, 0xFF, 0xFF, 0xFF, 0x69, 0xFF, 0xFF, 0xFF, 0xFF };
        private static readonly byte[] request_a2scsm  = new byte[9] { 0xFF, 0xFF, 0xFF, 0xFF, 0x66, 0xFF, 0xFF, 0xFF, 0xFF };
        private static byte[] response = new byte[128];
        private static string results = null;
 
        public static bool Query(bool start)
        {
            Array.Clear(response, 0, response.Length);

            using (Socket serverSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                serverSock.SendTimeout = 100;
                serverSock.ReceiveTimeout = 100;

                try
                {
                    if (Global.A2SFireWall)
                    {
                        serverSock.SendTo(request_a2scsm, Global.ipep);
                        serverSock.Receive(response);
                        results = Encoding.UTF8.GetString(response).Trim();

                        if (results.Length <= 5)
                            return false;

                        if (Global.currentMap == null)
                        {
                            Global.currentMap = results;
                            Console.WriteLine("{0} >>> Started srcds with map {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Global.currentMap);
                        }
                        else if (!results.Equals(Global.currentMap))
                        {
                            Console.WriteLine("{0} >>> Changed Map to {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), results);
                            Configs.startmap = results;
                            Global.currentMap = results;
                        }
                        return true;
                    }
                    else
                    {
                        serverSock.SendTo(request_a2sping, Global.ipep);
                        serverSock.Receive(response);
                        return (response[4] == 0x6A);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0} >>> Failed to A2S Query: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.Message);
                }
            }

            return false;
        }

        public static void CheckFirewall()
        {
            if(File.Exists(Environment.CurrentDirectory + "\\csgo\\addons\\sourcemod\\extensions\\A2SFirewall.ext.dll"))
            {
                Global.A2SFireWall = true;
                CheckAutoLoad();
            }
        }

        private static void CheckAutoLoad()
        {
            if(!File.Exists(Environment.CurrentDirectory + "\\csgo\\addons\\sourcemod\\extensions\\A2SFirewall.autoload"))
            {
                using(FileStream file = File.Create(Environment.CurrentDirectory + "\\csgo\\addons\\sourcemod\\extensions\\A2SFirewall.autoload"))
                {
                    response = Encoding.UTF8.GetBytes("This file created by CSGO Server Manager.");
                    file.Write(response, 0, response.Length);
                }
            }
        }
    }

    class SteamApi
    {
        private static string buffer = null;
        private static string result = null;

        private static string GetCurrentVersion()
        {
            using (StreamReader sr = new StreamReader(Environment.CurrentDirectory + "\\csgo\\steam.inf"))
            {
                buffer = null;
                while ((buffer = sr.ReadLine()) != null)
                {
                    if (!buffer.StartsWith("PatchVersion"))
                        continue;

                    return buffer.Replace("PatchVersion=", "");
                }
            }
            return "0.0.0.0";
        }

        public static bool GetLatestVersion()
        {
            try
            {
                result = null;
                using (WebClient http = new WebClient())
                {
                    result = http.DownloadString("https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=730&version=" + GetCurrentVersion() + "&format=json");
                    if (!result.Contains("\"success\":true"))
                    {
                        Console.WriteLine("{0} >>> Failed to check SteamApi: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), result);
                        return true;
                    }
                }
                return result.Equals("{\"response\":{\"success\":true,\"up_to_date\":true,\"version_is_listable\":true}}");
            }
            catch(Exception e)
            {
                Console.WriteLine("{0} >>> Failed to check SteamApi: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.Message);
            }

            return true;
        }
    }

    class TokenApi
    {
        private static int result = 0;
        private static string buffer = null;

        public static int CheckTokens(bool consoleLog = false)
        {
            try
            {
                result = 0;
                using (WebClient http = new WebClient())
                {
                    buffer = http.DownloadString("https://csgotokens.com/token-api.php?ip=" + Configs.wwip + ":" + Configs.port + "&key=" + Configs.TKApikey);

                    if (consoleLog)
                    {
                        Console.WriteLine("{0} >>> TokenApi -> Init {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), result);
                    }

                    if (buffer.Equals(Configs.accounts))
                    {
                        if (consoleLog)
                        {
                            Console.WriteLine("{0} >>> TokenApi -> Token status is OK.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                        }
                        result = 2;
                    }
                    else
                    {
                        if (buffer.Length == 32)
                        {
                            Logger.Log("Token was banned -> old token [" + Configs.accounts + "] -> new token [" + buffer + "]");
                            Console.WriteLine("{0} >>> Token was banned -> old token [{1}] -> new token [{2}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Configs.accounts, buffer);
                            Configs.accounts = buffer;
                            result = 1;
                        }
                        else
                        {
                            Console.WriteLine("{0} >>> TokenApi Response: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), result);
                            result = 0;
                        }
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} >>> TokenApi Exception: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.Message);
                return -2;
            }
        }
    }

    class Logger
    {
        private static readonly string logFile = Environment.CurrentDirectory + "\\server_log.log";
        private static readonly string errFile = Environment.CurrentDirectory + "\\server_err.log";
        private static readonly string mapFile = Environment.CurrentDirectory + "\\server_map.log";

        public static void Create()
        {
            if(!File.Exists(logFile))
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
            }
        }

        public static void Error(string err)
        {
            using (StreamWriter writer = new StreamWriter(errFile, true))
            {
                writer.WriteLine("[" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] >>> " + err);
            }
        }

        public static void Map(string map)
        {
            using (StreamWriter writer = new StreamWriter(mapFile, true))
            {
                writer.WriteLine("[" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] >>> Changed Map to " + map);
            }
        }
    }
}
