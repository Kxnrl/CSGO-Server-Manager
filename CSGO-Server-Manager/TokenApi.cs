using System;
using System.Net;

namespace Kxnrl.CSM
{
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
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    buffer = http.DownloadString("https://csgotokens.com/token-api.php?ip=" + Configs.ip + ":" + Configs.port + "&key=" + Configs.TokenApi);

                    if (consoleLog)
                    {
                        Console.WriteLine("{0} >>> TokenApi -> Init {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), result);
                    }

                    if (buffer.Equals(Configs.token))
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
                            Logger.Log("Token was banned -> old token [" + Configs.token + "] -> new token [" + buffer + "]");
                            Configs.token = buffer;
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
}
