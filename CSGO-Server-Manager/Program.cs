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
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Net;
using Microsoft.Win32;

namespace CSGO_Server_Manager
{
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
            Win32Api.ConsoleCTRL.ConsoleClosed(new Win32Api.ConsoleCTRL.HandlerRoutine(ApplicationHandler_OnClose));
            Win32Api.PowerMode.NoSleep();

            Console.Title = "CSGO Server Manager v1.2";

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

            Logger.Create();
            Helper.WatchFile();

            while(!File.Exists(Configs.srcds))
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
                        Configs.srcds = fileBrowser.FileName;
                        Console.WriteLine("{0} >>> Set SRCDS path -> {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Configs.srcds);
                    }
                }
            }

            while(!File.Exists(Configs.steam))
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
                        Configs.steam = fileBrowser.FileName;
                        Console.WriteLine("{0} >>> Set Steam path -> {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Configs.steam);
                    }
                }
            }

            if(string.IsNullOrEmpty(Configs.ip) || !IPAddress.TryParse(Configs.ip, out IPAddress ipadr))
            {
                do
                {
                    Console.WriteLine("Please input your Game Server IP ...");
                    Configs.ip = Console.ReadLine();
                }
                while(!IPAddress.TryParse(Configs.ip, out ipadr));
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
                if (exe.MainModule.FileName.Equals(Configs.srcds))
                {
                    exe.Kill();
                    Console.WriteLine("{0} >>> Force close old srcds before new srcds start.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                }
            }
            Console.WriteLine("{0} >>> {1} SRCDS are running on current host.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), process.Length);

            if (!string.IsNullOrEmpty(Configs.TokenApi))
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

            // check a2s key
            A2S.CheckFirewall();

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
            Win32Api.Window.Hide(myHandle);
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
                        Win32Api.Window.Show(Global.srcds.MainWindowHandle.ToInt32());
                        Console.WriteLine("Show SRCDS window.");
                        break;
                    case "hide":
                        Win32Api.Window.Hide(Global.srcds.MainWindowHandle.ToInt32());
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
                            Win32Api.Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                            Win32Api.Message.Send(Global.srcds.MainWindowHandle);
                            Thread.Sleep(1000);

                            if(Global.crash)
                                break;
                        }
                        Logger.Log("Trigger server update.");
                        Global.update = true;
                        Global.tupdate.Abort();
                        Global.tupdate = null;
                        new Thread(Thread_UpdateCSGO).Start();
                        break;
                    case "restart":
                        Logger.Log("Trigger server restart.");
                        Global.tupdate.Abort();
                        Global.tcrash.Abort();
                        Global.tupdate = null;
                        Global.tcrash = null;
                        Global.srcds.EnableRaisingEvents = false;
                        Global.srcds.Exited -= Srcds_OnExited;
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
                                Win32Api.Message.Write(Global.srcds.MainWindowHandle, input);
                                Win32Api.Message.Send(Global.srcds.MainWindowHandle);
                                Logger.Log("Execute server command: " + input);
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
                    Win32Api.Window.Hide(myHandle);
                    tray.showHide.Text = "Show";
                    tray.notifyIcon.BalloonTipText = "Hide Window, Click icon to recovery window";
                    if(Global.srcds != null && !Global.srcds.HasExited)
                    {
                        Win32Api.Window.Hide(Global.srcds.MainWindowHandle.ToInt32());
                    }
                }
                else
                {
                    currentShow = true;
                    Win32Api.Window.Show(myHandle);
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

        static bool ApplicationHandler_OnClose(Win32Api.ConsoleCTRL.CtrlTypes CtrlType)
        {
            if (CtrlType == Win32Api.ConsoleCTRL.CtrlTypes.CTRL_CLOSE_EVENT || CtrlType == Win32Api.ConsoleCTRL.CtrlTypes.CTRL_SHUTDOWN_EVENT)
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
                Logger.Log("Exit by closing window.");
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
                        + "-ip " + Configs.ip + " "
                        + "-port " + Configs.port + " "
                        + ((!string.IsNullOrEmpty(Configs.insecure)   && int.TryParse(Configs.insecure,   out int novalveac) && novalveac == 1) ? "-insecure " : "")
                        + ((!string.IsNullOrEmpty(Configs.tickrate)   && int.TryParse(Configs.tickrate,   out int TickRate)) ?  string.Format("-tickrate {0} ", TickRate) : "")
                        + ((!string.IsNullOrEmpty(Configs.maxplayers) && int.TryParse(Configs.maxplayers, out int maxPlays)) ?  string.Format("-maxplayers_override {0} ", maxPlays) : "")
                        + ((!string.IsNullOrEmpty(Configs.nobots)     && int.TryParse(Configs.nobots,     out int nobots)    && nobots == 1) ? "-nobots " : "")
                        + ((!string.IsNullOrEmpty(Configs.gametype)   && int.TryParse(Configs.gametype,   out int gameType)) ?  string.Format("+gametype {0} ", gameType) : "")
                        + ((!string.IsNullOrEmpty(Configs.gamemode)   && int.TryParse(Configs.gamemode,   out int gameMode)) ?  string.Format("+gamemode {0} ", gameMode) : "")
                        + ((!string.IsNullOrEmpty(Configs.mapgroup))  ?  string.Format("+mapgroup {0} ", Configs.mapgroup) : "")
                        + ((!string.IsNullOrEmpty(Configs.startmap))  ?  string.Format("+map {0} ", Configs.startmap) : "")
                        + ((!string.IsNullOrEmpty(Configs.token))     ?  string.Format("+sv_setsteamaccount {0} ", Configs.token) : "")
                        + ((!string.IsNullOrEmpty(Configs.groupids))  ?  string.Format("+sv_steamgroup {0} ", Configs.groupids) : "")
                        + ((!string.IsNullOrEmpty(A2S.a2skey) && A2S.a2skey.Length >= 6) ? string.Format("-a2skey {0} ", A2S.a2skey) : "");

            try
            {
                Global.srcds = new Process();
                Global.srcds.StartInfo.FileName = Configs.srcds;
                Global.srcds.StartInfo.Arguments = args;
                Global.srcds.StartInfo.UseShellExecute = false;
                Global.srcds.EnableRaisingEvents = true;
                Global.srcds.Exited += new EventHandler(Srcds_OnExited);
                Global.srcds.Start();

                Thread.Sleep(1000);
            }
            catch(Exception e)
            {
                Console.WriteLine("SRCDS start failed: {0}", e.Message);
                Console.WriteLine("StackTrace:{0}{1}", Environment.NewLine, e.StackTrace);
                Console.ReadKey(false);
                Environment.Exit(-4);
            }

            Logger.Log("Srcds Started! -> pid["+ Global.srcds.Id + "] path["+ Global.srcds.MainModule.FileName + "]");

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
            Win32Api.Window.Hide(Global.srcds.MainWindowHandle.ToInt32());

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
                Logger.Log("Srcds timeout crashed!");
                Global.crash = true;
                Global.tupdate.Abort();
                Global.tcrash = null;
                break;
            }

            if (!Global.srcds.HasExited)
            {
                Global.srcds.EnableRaisingEvents = false;
                Global.srcds.Exited -= Srcds_OnExited;
                Global.srcds.Kill();
            }

            Thread.Sleep(1500);
            Global.tcrash = new Thread(Thread_CheckCrashs);
            Global.tcrash.IsBackground = true;
            Global.tcrash.Name = "Crash Thread";
            Global.tcrash.Start();
        }

        public static void Srcds_OnExited(object sender, EventArgs e)
        {
            tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";
            tray.notifyIcon.BalloonTipText = "Srcds crashed!";
            tray.notifyIcon.ShowBalloonTip(5000);
            Logger.Log("Srcds unexpectedly crashed!");

            Global.crash = true;

            if (Global.tcrash != null)
            {
                Global.tcrash.Abort();
                Global.tcrash = null;
            }
            
            if(Global.tupdate != null)
            {
                Global.tupdate.Abort();
                Global.tupdate = null;
            }

            if(Global.srcds != null)
            {
                Global.srcds.Close();
                Global.srcds.Dispose();
                Global.srcds = null;
            }

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
                        Win32Api.Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                        Win32Api.Message.Send(Global.srcds.MainWindowHandle);
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
                    process.StartInfo.FileName = Configs.steam;
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
                        Win32Api.Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                        Win32Api.Message.Send(Global.srcds.MainWindowHandle);
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
}
