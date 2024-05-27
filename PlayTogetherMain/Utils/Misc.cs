using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayTogetherMod.Utils
{
    public class MonitorIterator
    {
        private int _count = 0;
        private int _index = 0;

        public void SetMonitorCount(int count)
        {
            _count = count;
        }
        public int Next() {
            _index++;
            if(_index >= _count)
                _index = 0;
            return _index;
        }
    }

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
