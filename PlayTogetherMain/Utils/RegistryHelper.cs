using Microsoft.Win32;
using System;

namespace PlayTogetherMod.Utils
{
    internal static class RegistryHelper
    {
        public static string ReadRegistryValue(RegistryHive regHive, string subkeyPath, string valueName)
        {
            try
            {
                RegistryKey key = RegistryKey.OpenBaseKey(regHive, RegistryView.Registry64).OpenSubKey(subkeyPath);
                if (key == null) throw new Exception($"Error opening registry key '{regHive}\\{subkeyPath}'");

                object value = key.GetValue(valueName);
                return value != null ? value.ToString() : string.Empty;
            }
            catch
            {
                throw; // rethrow the exception to propagate it up the call stack
            }
        }

        public static object ReadRegistryBIN(RegistryHive regHive, string subkeyPath, string valueName)
        {
            try
            {
                RegistryKey key = RegistryKey.OpenBaseKey(regHive, RegistryView.Registry64).OpenSubKey(subkeyPath);
                if (key == null) throw new Exception($"Error opening registry key '{regHive}\\{subkeyPath}'");

                return key.GetValue(valueName);

            }
            catch
            {
                throw; // rethrow the exception to propagate it up the call stack
            }
        }

        public static void WriteRegistryValue(RegistryHive regHive, string subkeyPath, string valueName, object value)
        {
            try
            {
                var baseKey = RegistryKey.OpenBaseKey(regHive, RegistryView.Registry64);
                RegistryKey sKey = baseKey.OpenSubKey(subkeyPath, true);
                if (sKey == null)
                    sKey = baseKey.CreateSubKey(subkeyPath);

                sKey.SetValue(valueName, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing to registry: " + ex.Message);
            }
        }
    }
}

