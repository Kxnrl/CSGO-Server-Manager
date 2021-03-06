﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Kxnrl.CSM
{
    class Helper
    {
        public static string GetLocalIpAddress()
        {
            string addr = null;
            Dns.GetHostEntry(Dns.GetHostName()).AddressList.ToList().ForEach(address =>
            {
                if (address.AddressFamily != AddressFamily.InterNetwork)
                    return;

                var ip = address.ToString();
                if (ip.StartsWith("10.") || ip.StartsWith("172.") || ip.StartsWith("192."))
                    return;

                addr = ip;
            });

            return addr ?? "Invalid Local Ip Adress (伺服器沒有公網IP)";
        }

        public static bool PortAvailable(int port)
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Where(p => p.Port == port).LongCount() == 1;
            //return !((from p in IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners() where p.Port == port select p).Count() == 1);
        }

        public static Process GetAppByPort(int checkPort)
        {
            using (var netstats = new Process())
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

                using (var sr = netstats.StandardOutput)
                {
                    var output = sr.ReadToEnd();
                    if (netstats.ExitCode != 0)
                        throw new Exception("netstats ExitCode = " + netstats.ExitCode);

                    var lines = Regex.Split(output, "\r\n");
                    foreach (var line in lines)
                    {
                        // first line 嘻嘻
                        if (line.Trim().StartsWith("Proto"))
                            continue;

                        var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length < 2)
                            continue;

                        if (!int.TryParse(parts[parts.Length - 1], out var pid))
                            continue;

                        if (!int.TryParse(parts[1].Split(':').Last(), out var port))
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
            if (Global.srcds == null || Global.srcds.HasExited)
                return;

            if (kickPlayer)
            {
                Win32Api.Message.Write(Global.srcds.MainWindowHandle, "sm_kick @all \"Server Restart\"");
                Win32Api.Message.Send(Global.srcds.MainWindowHandle);
                Thread.Sleep(2000);
            }

            Global.srcds.EnableRaisingEvents = false;
            Global.srcds.Exited -= Program.Srcds_OnExited;

            Win32Api.Message.Write(Global.srcds.MainWindowHandle, "quit");
            Win32Api.Message.Send(Global.srcds.MainWindowHandle);

            uint sec = 0;
            while (!Global.srcds.HasExited)
            {
                Thread.Sleep(1000);
                if (++sec >= 5)
                {
                    Console.WriteLine("{0} >>> Timeout -> Force Kill SRCDS! pid[{1}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Global.srcds.Id);
                    Global.srcds.Kill();
                    break;
                }
                Global.srcds.Refresh();
            }

            Global.srcds.Close();
            Global.srcds.Dispose();
            Global.srcds = null;
            if (Global.tcrash != null)
            {
                Global.tcrash.Abort();
                Global.tcrash = null;
            }
            Thread.Sleep(500);
        }

        public static void ForceQuit(Process exe)
        {
            Win32Api.Message.Write(exe.MainWindowHandle, "quit");
            Win32Api.Message.Send(exe.MainWindowHandle);

            Thread.Sleep(1500);

            var sec = 0u;
            while (!exe.HasExited)
            {
                Thread.Sleep(1000);
                if (++sec >= 5)
                {
                    Console.WriteLine("{0} >>> Timeout -> Force Kill SRCDS! pid[{1}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), exe.Id);
                    exe.Kill();
                    break;
                }
                exe.Refresh();
            }

            exe.Close();
            exe.Dispose();
        }

        public static void KillSRCDS(Process srcds)
        {
            Win32Api.Message.Write(srcds.MainWindowHandle, "quit");
            Win32Api.Message.Send(srcds.MainWindowHandle);

            Global.srcds.EnableRaisingEvents = false;
            Global.srcds.Exited -= Program.Srcds_OnExited;

            var sec = 0u;
            while (!srcds.HasExited)
            {
                Thread.Sleep(1000);
                if (++sec >= 5)
                {
                    Console.WriteLine("{0} >>> Timeout -> Force Kill SRCDS! pid[{1}]", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), srcds.Id);
                    srcds.Kill();
                    break;
                }
                srcds.Refresh();
            }

            srcds.Close();
            srcds.Dispose();
            Thread.Sleep(500);
        }

        public static void WatchFile()
        {
            Global.watcher = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory, "server_config.ini");
            Global.watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName;
            Global.watcher.Changed += ConfigFile_OnChanged;
            Global.watcher.Created += ConfigFile_OnChanged;
            Global.watcher.Deleted += ConfigFile_OnChanged;
            Global.watcher.Renamed += ConfigFile_OnRenamed;
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

        public static string FindError(string name)
        {
            string message = null;

            var handle = Win32Api.Window.FindWindow(null, name);
            if (handle == IntPtr.Zero)
            {
                // Not Found
                return message;
            }

            var tid_cl = Win32Api.Window.GetWindowThreadProcessId(handle, out var pid);
            if (Global.srcds.Id != pid)
            {
                // Not casuse by current srcds.
                return message;
            }

            var sb = new StringBuilder(256);
            Win32Api.Window.EnumChildWindows
            (
                handle,
                (hwnd, lparma) =>
                {
                    var length = Win32Api.Window.GetWindowTextLength(hwnd);
                    Win32Api.Window.GetWindowText(hwnd, sb, length + 1);

                    if (sb.ToString().Equals("确定") || sb.ToString().Equals("OK"))
                    {
                        //Ignore this.
                        return true;
                    }

                    // save
                    message = sb.ToString();
                    return false;
                },
                IntPtr.Zero
            );

            return message;
        }
    }
}
