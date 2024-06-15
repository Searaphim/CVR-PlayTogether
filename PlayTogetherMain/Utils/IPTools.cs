using System.Linq;
using System;
using System.Net;
using System.Text;

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

        public static class Base36Formatter
        {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            public static string ToBase36(uint value)
            {
                StringBuilder sb = new StringBuilder();

                while (value > 0u)
                {
                    int remainder = unchecked((int)(value % 36));
                    sb.Insert(0, alphabet[remainder]);
                    value /= 36;
                }

                return sb.ToString();
            }

            public static uint FromBase36(string value)
            {
                uint result = 0;
                value = value.ToUpper();
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    int digitValue = Array.IndexOf(alphabet.ToCharArray(), c);
                    result += (uint)(digitValue * Math.Pow(36, value.Length - 1 - i));
                }

                return result;
            }
        }

        internal static string GenLobbyCode()
        {
            int roundedTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600) / 7200 * 7200; //Makes lobby code expire between 1-2 hours
            return Base36Formatter.ToBase36(ScrambleIp(GetPublicIPAddress(), roundedTime));
        }

        internal static string LobbyCodeToIP(string lobbyCode)
        {
            if (lobbyCode.Contains("."))
                return lobbyCode;
            int roundedTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600) / 7200 * 7200;
            return UnscrambleIp(Base36Formatter.FromBase36(lobbyCode), roundedTime);
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
