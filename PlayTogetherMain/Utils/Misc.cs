using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayTogetherMod.Utils
{
    public static class Misc
    {
        public static void WindowsRun(string cmd)
        {
            var pInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c start " + cmd,
                CreateNoWindow = true
            };
            Process.Start(pInfo);
        }
    }
}
