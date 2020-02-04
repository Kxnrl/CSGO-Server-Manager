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
using System.Text;
using Microsoft.Win32;
using System.Reflection;

namespace Kxnrl.CSM
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

        public static IntPtr myHwnd = IntPtr.Zero;

        public static readonly string version = Assembly.GetExecutingAssembly().GetName().Version.ToString().TrimEnd(new char[] { '.', '0' });

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

            Process myProc = Process.GetCurrentProcess();
            myProc.PriorityClass = ProcessPriorityClass.BelowNormal;
            myHwnd = myProc.MainWindowHandle;

            // Event
            Application.ThreadException += new ThreadExceptionEventHandler(ExceptionHandler_CurrentThread);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler_AppDomain);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(ApplicationHandler_OnExit);
            SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(ApplicationHandler_PowerModeChanged);
            Win32Api.ConsoleCTRL.ConsoleClosed(new Win32Api.ConsoleCTRL.HandlerRoutine(ApplicationHandler_OnClose));
            Win32Api.PowerMode.NoSleep();

            Console.Title = "CSGO Server Manager v" + version;

            bool conf = Configs.Check();
            if (!conf)
            {
                Console.WriteLine("{0} >>> Configs was initialized -> You can modify it manually!", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            }

            Logger.Create();
            Helper.WatchFile();

            while (!File.Exists(Configs.srcds))
            {
                using (OpenFileDialog fileBrowser = new OpenFileDialog())
                {
                    fileBrowser.Multiselect = false;
                    fileBrowser.Filter = "CSGO Dedicated Server (srcds.exe)|srcds.exe";

                    if (fileBrowser.ShowDialog() != DialogResult.OK)
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

            while (!File.Exists(Configs.steam))
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

            if (string.IsNullOrEmpty(Configs.ip) || !IPAddress.TryParse(Configs.ip, out IPAddress ipadr))
            {
                do
                {
                    Console.WriteLine("Please input your Game Server IP ...");
                    Configs.ip = Console.ReadLine();
                }
                while (!IPAddress.TryParse(Configs.ip, out ipadr));
            }

            if (string.IsNullOrEmpty(Configs.port) || !int.TryParse(Configs.port, out int port))
            {
                do
                {
                    Console.WriteLine("Please input your Game Server Port (1 - 65535) ...");
                    Configs.port = Console.ReadLine();
                }
                while (!int.TryParse(Configs.port, out port));
            }

            Global.ipep = new IPEndPoint(ipadr, port);

            if (!Helper.PortAvailable(port))
            {
                Console.WriteLine("{0} >>> Port[{1}] is unavailable! Finding Application...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), port);

                try
                {
                    Process exe = Helper.GetAppByPort(port);
                    Console.WriteLine("{0} >>> Trigger SRCDS Quit -> App[{1}] PID[{2}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), exe.MainWindowTitle, exe.Id);
                    Helper.ForceQuit(exe);
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0} >>> Not found Application: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.Message);
                }
            }

            Process[] process = Process.GetProcessesByName("srcds");
            foreach (Process exe in process)
            {
                if (exe.MainModule.FileName.Equals(Configs.srcds))
                {
                    Helper.ForceQuit(exe);
                    Console.WriteLine("{0} >>> Force close old srcds before new srcds start.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                }
            }
            Console.WriteLine("{0} >>> {1} SRCDS are running on current host.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), process.Length);

            if (!string.IsNullOrEmpty(Configs.TokenApi))
            {
                while (TokenApi.CheckTokens(true) <= 0)
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

            // Open editor?
            if (string.IsNullOrEmpty(Configs.token) && !conf)
            {
                Console.WriteLine("Do you want to edit server config manually? [Y/N]");
                if (Console.ReadKey().Key == ConsoleKey.Y)
                {
                    Process proc = null;
                    try
                    {
                        proc = Process.Start(new ProcessStartInfo() { FileName = "notepad++.exe", Arguments = " \"" + Path.Combine(Application.StartupPath, "server_config.ini") + "\" ", WindowStyle = ProcessWindowStyle.Minimized });
                        MessageBox.Show("Please Edit server config in Notepad++!" + Environment.NewLine + "Don't forget to click save button!", "CSGO Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    catch
                    {

                        if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kxnrl", "Notepad", "notepad++.exe")))
                        {
                            proc = Process.Start(new ProcessStartInfo() { FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kxnrl", "Notepad", "notepad++.exe"), Arguments = " \"" + Path.Combine(Application.StartupPath, "server_config.ini") + "\" ", WindowStyle = ProcessWindowStyle.Minimized });
                            MessageBox.Show("Please Edit server config in Notepad++!" + Environment.NewLine + "Don't forget to click save button!", "CSGO Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                        else
                        {
                            proc = Process.Start(new ProcessStartInfo() { FileName = "notepad.exe", Arguments = " \"" + Path.Combine(Application.StartupPath, "server_config.ini") + "\" ", WindowStyle = ProcessWindowStyle.Minimized });
                            MessageBox.Show("Please Edit server config in Notepad!" + Environment.NewLine + "Don't forget to click save button!", "CSGO Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                    }
                    finally
                    {
                        if (proc != null)
                        {
                            // reverse
                            Win32Api.Window.Show(proc.MainWindowHandle);
                            Win32Api.Window.Active(proc.MainWindowHandle);
                        }

                        Environment.Exit(0);
                    }
                }
            }

            // check a2s key
            A2S.CheckFirewall();


            // check server chan
            Logger.Check();

            // current
            Win32Api.Window.Hide(myHwnd);
            currentShow = false;

            Global.tcrash = new Thread(Thread_CheckCrashs)
            {
                IsBackground = true,
                Name = "Crash Thread"
            };
            Global.tcrash.Start();

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

            Thread.Sleep(5000);

            tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";
            tray.notifyIcon.BalloonTipText = "Server Started!";
            tray.notifyIcon.ShowBalloonTip(5000);

            string input;
            while (true)
            {
                input = Console.ReadLine();

                if (Global.update)
                {
                    Console.WriteLine("Updating ...");
                    continue;
                }

                if (Global.crash)
                {
                    Console.WriteLine("Restarting ...");
                    continue;
                }

                switch (input.ToLower())
                {
                    case "show":
                        Win32Api.Window.Show(Global.srcds.MainWindowHandle);
                        Console.WriteLine("Show SRCDS window.");
                        break;
                    case "hide":
                        Win32Api.Window.Hide(Global.srcds.MainWindowHandle);
                        Console.WriteLine("Hide SRCDS window.");
                        break;
                    case "quit":
                        Environment.Exit(0);
                        break;
                    case "exit":
                        Environment.Exit(0);
                        break;
                    case "update":
                        for (int cd = 60; cd > 0; cd--)
                        {
                            Console.WriteLine("Server restart in " + cd + " seconds");
                            Win32Api.Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                            Win32Api.Message.Send(Global.srcds.MainWindowHandle);
                            Thread.Sleep(1000);

                            if (Global.crash)
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
                        Global.tcrash = new Thread(Thread_CheckCrashs)
                        {
                            IsBackground = true,
                            Name = "Crash Thread"
                        };
                        Global.tcrash.Start();
                        break;
                    default:
                        if (input.StartsWith("exec "))
                        {
                            input = input.Replace("exec ", "");
                            if (input.Length > 1)
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
            if (item == tray.exitButton)
            {
                tray.notifyIcon.Visible = false;
                tray.notifyIcon.Dispose();
                Thread.Sleep(50);
                Environment.Exit(0);
            }
            else if (item == tray.showHide)
            {
                tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";

                if (currentShow)
                {
                    currentShow = false;
                    Win32Api.Window.Hide(myHwnd);
                    tray.showHide.Text = "Show";
                    tray.notifyIcon.BalloonTipText = "Hide Window, Click icon to recovery window";
                    if (Global.srcds != null && !Global.srcds.HasExited)
                    {
                        Win32Api.Window.Hide(Global.srcds.MainWindowHandle);
                    }
                }
                else
                {
                    currentShow = true;
                    Win32Api.Window.Show(myHwnd);
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
                Logger.Push("Server unexpectedly crashed", "Exited by closing window.");
            }

            return true;
        }

        static void ApplicationHandler_OnExit(object sender, EventArgs e)
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
            Logger.Log("Exit by others.");
            Logger.Push("Server unexpectedly crashed", "Exit by others.");
        }

        static void ExceptionHandler_AppDomain(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = args.ExceptionObject as Exception;
            Logger.Error("\n----------------------------------------\nThread: " + Thread.CurrentThread.Name + "\nException: " + e.GetType() + "\nMessage: " + e.Message + "\nStackTrace:\n" + e.StackTrace);
        }

        static void ExceptionHandler_CurrentThread(object sender, ThreadExceptionEventArgs args)
        {
            Exception e = args.Exception;
            Logger.Error("\n----------------------------------------\nThread: " + Thread.CurrentThread.Name + "\nException: " + e.GetType() + "\nMessage: " + e.Message + "\nStackTrace:\n" + e.StackTrace);
        }

        static void Thread_CheckCrashs()
        {
            // version validate
            if (!SteamApi.GetLatestVersion())
            {
                Global.update = true;
                Global.tcrash = null;
                new Thread(Thread_UpdateCSGO).Start();
                return;
            }

            // Check maps valid
            if (!string.IsNullOrEmpty(Configs.startmap))
            {
                if (!Configs.game.Equals("insurgency"))
                {
                    if (!File.Exists(Path.Combine(Path.GetDirectoryName(Configs.srcds), Configs.game, "maps", Configs.startmap + ".bsp")))
                    {
                        string[] maps = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Configs.srcds), Configs.game, "maps"), "*.bsp");

                        if (maps.Length < 1)
                        {
                            Logger.Error("There are no valid maps in your maps folder. please add maps!");
                            Console.WriteLine("Press any key to continue ...");
                            Console.ReadKey();
                            Environment.Exit(0);
                        }

                        Configs.startmap = Path.GetFileNameWithoutExtension(maps[0]);
                    }
                }
                else
                {
                    string[] sp = Configs.startmap.Split(' ');

                    if (!File.Exists(Path.Combine(Path.GetDirectoryName(Configs.srcds), Configs.game, "maps", sp[0] + ".bsp")))
                    {
                        string[] maps = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Configs.srcds), Configs.game, "maps"), "*.bsp");

                        if (maps.Length < 1)
                        {
                            Logger.Error("There are no valid maps in your maps folder. please add maps!");
                            Console.WriteLine("Press any key to continue ...");
                            Console.ReadKey();
                            Environment.Exit(0);
                        }

                        Logger.Log("Not Found startup map: " + Configs.startmap);
                        Configs.startmap = Path.GetFileNameWithoutExtension(maps[0]) + " " + (sp.Length > 1 ? sp[1] : "push");
                    }
                    else if (sp.Length < 2)
                    {
                        Configs.startmap += " " + (Configs.startmap.Contains("_coop") ? "checkpoint" : "push");
                    }
                }
            }

            string args = "-console" + " "
                        + "-ip "      + Configs.ip   + " "
                        + "-port "    + Configs.port + " "
                        + "-game \""  + Configs.game + "\" "
                        + "-csm "     + version      + " "
                        + ((!string.IsNullOrEmpty(Configs.insecure)   && int.TryParse(Configs.insecure,   out int insecure) && insecure > 0) ? "-insecure " : "")
                        + ((!string.IsNullOrEmpty(Configs.tickrate)   && int.TryParse(Configs.tickrate,   out int TickRate) && TickRate > 0) ? string.Format("-tickrate {0} ", TickRate) : "")
                        + ((!string.IsNullOrEmpty(Configs.maxplayers) && int.TryParse(Configs.maxplayers, out int maxPlays) && maxPlays > 0) ? string.Format("-maxplayers_override {0} ", maxPlays) : "")
                        + ((!string.IsNullOrEmpty(Configs.nobots)     && int.TryParse(Configs.nobots,     out int nobotsex) && nobotsex > 0) ? "-nobots " : "")
                        + ((!string.IsNullOrEmpty(Configs.gametype)   && int.TryParse(Configs.gametype,   out int gameType) && gameType > 0) ? string.Format("+gametype {0} ", gameType) : "")
                        + ((!string.IsNullOrEmpty(Configs.gamemode)   && int.TryParse(Configs.gamemode,   out int gameMode) && gameMode > 0) ? string.Format("+gamemode {0} ", gameMode) : "")
                        + ((!string.IsNullOrEmpty(Configs.SteamApi))   ? string.Format("-authkey {0} ",            Configs.SteamApi) : "")
                        + ((!string.IsNullOrEmpty(Configs.mapgroup))   ? string.Format("+mapgroup {0} ",           Configs.mapgroup) : "")
                        + ((!string.IsNullOrEmpty(Configs.startmap))   ? string.Format("+map \"{0}\" ",            Configs.startmap) : "")
                        + ((!string.IsNullOrEmpty(Configs.token))      ? string.Format("+sv_setsteamaccount {0} ", Configs.token)    : "")
                        + ((!string.IsNullOrEmpty(Configs.groupids))   ? string.Format("+sv_steamgroup {0} ",      Configs.groupids) : "")
                        + ((!string.IsNullOrEmpty(Configs.options))    ? Configs.options : "")
                        ;

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

                Logger.Log("Srcds Started! -> pid[" + Global.srcds.Id + "] path[" + Global.srcds.MainModule.FileName + "]");

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

                Thread.Sleep(6666);
                if (!currentShow)
                {
                    Win32Api.Window.Hide(Global.srcds.MainWindowHandle);
                }

                // Set to High
                Global.srcds.PriorityClass = ProcessPriorityClass.High;

            }
            catch (Exception e)
            {
                Console.WriteLine("SRCDS start failed: {0}", e.Message);
                Console.WriteLine("StackTrace:{0}{1}", Environment.NewLine, e.StackTrace);
                Console.ReadKey(false);
                Environment.Exit(-4);
            }

            Global.tupdate = new Thread(Thread_UpdateCheck)
            {
                IsBackground = true,
                Name = "Update Thread"
            };
            Global.tupdate.Start();

            Global.crash = false;
            uint a2stimeout = 0;
            string srcdsError = null;

            while (true)
            {
                Thread.Sleep(3000);

                if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.AbortRequested || Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                    return;

                if (Global.update)
                {
                    Global.tcrash = null;
                    return;
                }

                srcdsError = SrcdsError(out string errType);

                if (srcdsError != null)
                {
                    Logger.Push("Server unexpectedly crashed", errType + ": " + srcdsError);
                    Logger.Log("Srcds crashed -> " + errType + ": " + srcdsError);
                    goto crashed;
                }
                else if (!A2S.Query(false))
                {
                    a2stimeout++;
                    Console.Title = "[TimeOut] " + Global.srcds.MainWindowTitle;
                }
                else
                {
                    if (Global.A2SFireWall)
                    {
                        Console.Title = "[" + Global.currentPlayers + "/" + Global.maximumPlayers + "]" + "  -  " + Global.hostname;
                    }
                    else
                    {
                        byte[] titles = Encoding.Default.GetBytes(Global.srcds.MainWindowTitle);
                        Console.Title = "[" + Global.currentPlayers + "/" + Global.maximumPlayers + "]" + "  -  " + Encoding.UTF8.GetString(titles);
                    }
                    tray.notifyIcon.Text = Console.Title;
                    a2stimeout = 0;
                }

                if (a2stimeout >= 10)
                {
                    Logger.Log("Srcds crashed -> A2STimeout");
                    srcdsError = "Srcds crashed -> A2STimeout";
                    Logger.Push("Server unexpectedly crashed", "A2S service timeout.");
                    goto crashed;
                }
            }

            // shrot
            crashed:

            // clr
            Global.crash = true;
            Global.tupdate.Abort();
            Global.tcrash = null;

            // notify icon
            tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";
            tray.notifyIcon.BalloonTipText = srcdsError ?? "SRCDS crashed!";
            tray.notifyIcon.ShowBalloonTip(5000);

            // check?
            if (!Global.srcds.HasExited)
            {
                Global.srcds.EnableRaisingEvents = false;
                Global.srcds.Exited -= Srcds_OnExited;
                Global.srcds.Kill();
            }

            // new tread
            Thread.Sleep(1500);
            Global.tcrash = new Thread(Thread_CheckCrashs)
            {
                IsBackground = true,
                Name = "Crash Thread"
            };
            Global.tcrash.Start();
        }

        public static void Srcds_OnExited(object sender, EventArgs e)
        {
            tray.notifyIcon.BalloonTipTitle = "CSGO Server Manager";
            tray.notifyIcon.BalloonTipText = "Srcds crashed!";
            tray.notifyIcon.ShowBalloonTip(5000);
            Logger.Log("Srcds unexpectedly crashed!");
            Logger.Push("Server unexpectedly crashed", "Crashing by close.");

            Global.crash = true;

            if (Global.tcrash != null)
            {
                Global.tcrash.Abort();
                Global.tcrash = null;
            }

            if (Global.tupdate != null)
            {
                Global.tupdate.Abort();
                Global.tupdate = null;
            }

            if (Global.srcds != null)
            {
                Global.srcds.Close();
                Global.srcds.Dispose();
                Global.srcds = null;
            }

            Thread.Sleep(1500);
            Global.tcrash = new Thread(Thread_CheckCrashs)
            {
                IsBackground = true,
                Name = "Crash Thread"
            };
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
                    for (int cd = 60; cd > 0; cd--)
                    {
                        Console.WriteLine("Server restart in " + cd + " seconds");
                        Win32Api.Message.Write(Global.srcds.MainWindowHandle, "say Server restart in " + cd + " seconds");
                        Win32Api.Message.Send(Global.srcds.MainWindowHandle);
                        Thread.Sleep(1000);

                        if (Global.crash)
                            goto done;
                    }

                    goto done;
                }

                Thread.Sleep(300000);
            }
            while (true);

            done:
            Global.update = true;
            Global.tupdate = null;
            new Thread(Thread_UpdateCSGO).Start();
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
                    Console.WriteLine(Environment.NewLine);

                    process.EnableRaisingEvents = true;
                    process.StartInfo.FileName = Configs.steam;
                    process.StartInfo.Arguments = "+login anonymous +force_install_dir \"" + AppDomain.CurrentDomain.BaseDirectory + "\" " + "+app_update 740 +exit";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();

                    process.OutputDataReceived += (sender, e) => { Console.WriteLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { Console.WriteLine(e.Data); };

                    process.WaitForExit();

                    Console.WriteLine(Environment.NewLine);
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
                Global.tcrash = new Thread(Thread_CheckCrashs)
                {
                    IsBackground = true,
                    Name = "Crash Thread"
                };
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

                    Global.tcrash = new Thread(Thread_CheckCrashs)
                    {
                        IsBackground = true,
                        Name = "Crash Thread"
                    };
                    Global.tcrash.Start();
                }
            }
        }

        static string SrcdsError(out string err)
        {
            string ret = null;

            ret = Helper.FindError("Engine Error");
            if (ret != null)
            {
                err = "Engine Error";
                return ret;
            }

            ret = Helper.FindError("Host_Error");
            if (ret != null)
            {
                err = "Host_Error";
                return ret;
            }

            err = "Running";
            return ret;
        }
    }
}
