using System.Linq;
using System;
using System.Net;

namespace PlayTogetherMod.Utils
{
    internal class LobbyCodeHandler
    {
        private const string service = "https://ifconfig.me/ip"; //Known service that echoes back your internet-facing IP.
        private static string GetPublicIPAddress() {
            string ipAddress = "";
            using (WebClient client = new WebClient())
            {
                string response = client.DownloadString(service);
                ipAddress = response.Trim();
            }
            return ipAddress;
        }

        internal static string GenLobbyCode()
        {
            int roundedTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600) / 7200 * 7200; //Makes lobby code expire between 1-2 hours
            return ScrambleIp(GetPublicIPAddress(), roundedTime).ToString();
        }

        internal static string LobbyCodeToIP(string lobbyCode)
        {
            if (lobbyCode.Contains("."))
                return lobbyCode;
            int roundedTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600) / 7200 * 7200;
            uint codeAsInt = 0;
            if(uint.TryParse(lobbyCode, out codeAsInt))
                return UnscrambleIp(codeAsInt, roundedTime);
            return "";
        }

        public static uint ScrambleIp(string ipAddress, int roundedTime)
        {
            byte[] ipBytes = ipAddress.Split('.').Select(byte.Parse).ToArray();
            byte[] timeBytes = BitConverter.GetBytes(roundedTime);
            uint scrambledDecimal = 0;

            for (int i = 0; i < ipBytes.Length; i++)
            {
                // Use addition instead of XOR
                int scrambledByte = ipBytes[i] + timeBytes[i % timeBytes.Length];
                // Use modulus to ensure byte values stay within range
                scrambledByte %= 256;
                // Combine scrambled bytes into a single uint
                scrambledDecimal += (uint)(scrambledByte << (i * 8));
            }

            return scrambledDecimal;
        }

        public static string UnscrambleIp(uint scrambledDecimal, int roundedTime)
        {
            byte[] timeBytes = BitConverter.GetBytes(roundedTime);
            byte[] unscrambledBytes = new byte[4];

            for (int i = 0; i < 4; i++)
            {
                // Extract each byte from the scrambledDecimal
                int scrambledByte = (int)((scrambledDecimal >> (i * 8)) & 0xFF);
                // Use subtraction instead of XOR
                int unscrambledByte = scrambledByte - timeBytes[i % timeBytes.Length];
                // Use modulus to ensure byte values stay within range
                unscrambledByte = (unscrambledByte + 256) % 256;
                unscrambledBytes[i] = (byte)unscrambledByte;
            }

            return string.Join(".", unscrambledBytes);
        }
    }
}
