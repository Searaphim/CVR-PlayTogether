using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PlayTogetherMod.Utils
{
    public class IniParserHelper
    {
        public string ExtractAppName(string iniFilePath)
        {
            string pattern = @".*?\\apps\\.*?\\name=(.*)";
            using (FileStream fileStream = new FileStream(iniFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Match match = Regex.Match(line, pattern);
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            return null;
        }
    }
}
