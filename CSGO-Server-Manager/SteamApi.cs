using System;
using System.IO;
using System.Net;

//https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=730&version={version}&format=vdf

namespace Kxnrl.CSM
{
    class SteamApi
    {
        private static string buffer = null;
        private static string result = null;

        private static string GetCurrentVersion()
        {
            using (StreamReader sr = new StreamReader(Path.Combine(Path.GetDirectoryName(Configs.srcds), Configs.game, "steam.inf")))
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

        private static string GetCurrentAppId()
        {
            using (StreamReader sr = new StreamReader(Path.Combine(Path.GetDirectoryName(Configs.srcds), Configs.game, "steam.inf")))
            {
                buffer = null;
                while ((buffer = sr.ReadLine()) != null)
                {
                    if (!buffer.StartsWith("appID"))
                        continue;

                    return buffer.Replace("appID=", "");
                }
            }
            return "0";
        }

        public static bool GetLatestVersion()
        {
            try
            {
                result = null;
                using (WebClient http = new WebClient())
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    result = http.DownloadString("https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=" + GetCurrentAppId() + "&version=" + GetCurrentVersion() + "&format=json");
                    if (!result.Contains("\"success\":true"))
                    {
                        Console.WriteLine("{0} >>> Failed to check SteamApi: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), result);
                        return true;
                    }
                }
                return result.Equals("{\"response\":{\"success\":true,\"up_to_date\":true,\"version_is_listable\":true}}");
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} >>> Failed to check SteamApi: {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), e.Message);
            }

            return true;
        }
    }
}
