using System.Net;

namespace PlayTogetherMod.Utils
{
    internal class IPRetriever
    {
        private const string service = "https://ifconfig.me/ip"; //Known service that echoes back your internet-facing IP.
        internal static string GetPublicIPAddress() {
            string ipAddress = "";
            using (WebClient client = new WebClient())
            {
                string response = client.DownloadString(service);
                ipAddress = response.Trim();
            }
            return ipAddress;
        }
    }

    //Later I should probably provide a hash rather than the IP for security reasons. ;)
}
