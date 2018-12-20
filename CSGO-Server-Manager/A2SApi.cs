﻿using System;
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
                        results = Encoding.UTF8.GetString(response).Trim();

                        if (results.Length <= 5)
                            return false;

                        string[] data = results.Split('|');

                        if (data.Length < 3)
                        {
                            Logger.Error("Recv string not match: [" + results + "]");
                            return false;
                        }

                        if (Global.currentMap == null)
                        {
                            Global.currentMap = data[0];
                            Console.WriteLine("{0} >>> Started srcds with map {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Global.currentMap);
                        }
                        else if (!data[0].Equals(Global.currentMap))
                        {
                            Logger.Map(data[0]);
                            Configs.startmap = data[0];
                            Global.currentMap = data[0];
                        }

                        Global.currentPlayers = Convert.ToUInt32(data[1]);
                        Global.maximumPlayers = Convert.ToUInt32(data[2]);

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
            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "csgo", "addons", "sourcemod", "extensions", "A2SFirewall.ext.dll")))
            {
                Global.A2SFireWall = true;
                CheckAutoLoad();
            }
            else
            {
                Console.WriteLine("{0} >>> A2SFirewall -> Extension has not installed ...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                return;
            }

            Console.WriteLine("{0} >>> A2SFirewall -> Initializing ...", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
        }

        private static void CheckAutoLoad()
        {
            if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "csgo", "addons", "sourcemod", "extensions", "A2SFirewall.autoload")))
            {
                try
                {
                    using (FileStream file = File.Create(Path.Combine(Environment.CurrentDirectory, "csgo", "addons", "sourcemod", "extensions", "A2SFirewall.autoload")))
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
