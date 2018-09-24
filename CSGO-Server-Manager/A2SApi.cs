using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Kxnrl.CSM
{
    class A2S
    {
        private static readonly byte[] request_a2sping = new byte[9] { 0xFF, 0xFF, 0xFF, 0xFF, 0x69, 0xFF, 0xFF, 0xFF, 0xFF };
        private static readonly byte[] request_a2scsm = new byte[9] { 0xFF, 0xFF, 0xFF, 0xFF, 0x66, 0xFF, 0xFF, 0xFF, 0xFF };
        private static byte[] response = new byte[128];
        private static string results = null;
        public static string a2skey = null;

        public static bool Query(bool start)
        {
            Array.Clear(response, 0, response.Length);
            results = string.Empty;

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
                        results = Encoding.UTF8.GetString(response).TrimEnd('\t').TrimEnd('\n').TrimEnd('\0').Trim();

                        if (results.Length <= 5)
                            return false;

                        if (Global.currentMap == null)
                        {
                            Global.currentMap = results;
                            Console.WriteLine("{0} >>> Started srcds with map {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Global.currentMap);
                        }
                        else if (!results.Equals(Global.currentMap))
                        {
                            Logger.Map(results);
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
            if (File.Exists(Environment.CurrentDirectory + "\\csgo\\addons\\sourcemod\\extensions\\A2SFirewall.ext.dll"))
            {
                Global.A2SFireWall = true;
                CheckAutoLoad();
            }
            else
            {
                Console.WriteLine("{0} >>> A2SFirewall -> Extension has not installed ...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                return;
            }

            Console.WriteLine("{0} >>> A2SFirewall -> Checking A2Skey ...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));

            int left = 3;
            while(left >= 0)
            {
                try
                {
                    using (WebClient web = new WebClient())
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        Console.WriteLine("{0} >>> A2SFirewall -> Downloading A2Skey ...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                        a2skey = web.DownloadString("https://api.kxnrl.com/A2SFirewall/?ip=" + Configs.ip);
                        if (a2skey.Length >= 4)
                        {
                            Console.WriteLine("{0} >>> A2SFirewall -> A2Skey has been loaded ...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                            break;
                        }
                    }
                }
                catch (WebException webEx)
                {
                    Logger.Error("\n----------------------------------------\nThread: " + Thread.CurrentThread.Name + "\nException: " + webEx.GetType() + "\nMessage: " + webEx.Message + "\nStackTrace:\n" + webEx.StackTrace);
                }
                catch (Exception e)
                {
                    Logger.Error("\n----------------------------------------\nThread: " + Thread.CurrentThread.Name + "\nException: " + e.GetType() + "\nMessage: " + e.Message + "\nStackTrace:\n" + e.StackTrace);
                }
                finally
                {
                    
                    if(string.IsNullOrEmpty(a2skey))
                    {
                        left--;
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        private static void CheckAutoLoad()
        {
            if (!File.Exists(Environment.CurrentDirectory + "\\csgo\\addons\\sourcemod\\extensions\\A2SFirewall.autoload"))
            {
                try
                {
                    using (FileStream file = File.Create(Environment.CurrentDirectory + "\\csgo\\addons\\sourcemod\\extensions\\A2SFirewall.autoload"))
                    {
                        response = Encoding.UTF8.GetBytes("This file created by CSGO Server Manager.");
                        file.Write(response, 0, response.Length);
                    }
                }
                catch(Exception e)
                {
                    Console.Write("Failed to check A2SFirewall autoload");
                    Logger.Error("\n----------------------------------------\nThread: " + Thread.CurrentThread.Name + "\nException: " + e.GetType() + "\nMessage: " + e.Message + "\nStackTrace:\n" + e.StackTrace);
                }
            }
        }
    }
}
