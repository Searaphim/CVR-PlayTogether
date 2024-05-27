using System.Diagnostics;
using System;
using Microsoft.Win32;

namespace PlayTogetherMod.Utils
{
    public static class AudioHelper
    {
        private static string FindVBCableInputGUID()
        {
            try
            {
                RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render");
                if (key == null) throw new Exception($"Error opening registry key");
                string[] subkeys = key.GetSubKeyNames();
                foreach (var subkey in subkeys)
                {
                    var propkey = key.OpenSubKey(subkey + @"\Properties");
                    var res = propkey.GetValue("{a8b865dd-2e3d-4094-ad97-e593a70c75d6},8");
                    if (res == null) continue;
                    if (res.ToString() == "VBAudioVACWDM") return subkey;
                }
                return "";
            }
            catch
            {
                throw; // rethrow the exception to propagate it up the call stack
            }
        }

        private static string FindVBCableOutputGUID()
        {
            try
            {
                RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture");
                if (key == null) throw new Exception($"Error opening registry key");
                string[] subkeys = key.GetSubKeyNames();
                foreach (var subkey in subkeys)
                {
                    var propkey = key.OpenSubKey(subkey + @"\Properties");
                    var res = propkey.GetValue("{a8b865dd-2e3d-4094-ad97-e593a70c75d6},8");
                    if (res == null) continue;
                    if (res.ToString() == "VBAudioVACWDM") return subkey;
                }
                return "";
            }
            catch
            {
                throw; // rethrow the exception to propagate it up the call stack
            }
        }

        private static string FindChilloutVREndpointKey()
        {
            try
            {
                RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Multimedia\Audio\DefaultEndpoint");
                if (key == null) throw new Exception($"Error opening registry key");
                string[] subkeys = key.GetSubKeyNames();
                foreach (var subkey in subkeys)
                {
                    var endpointKey = key.OpenSubKey(subkey);
                    var res = endpointKey.GetValue("");
                    if (res == null) continue;
                    var resStr = res.ToString();
                    if (resStr.Contains("ChilloutVR.exe")) return subkey;
                }
                return "";
            }
            catch
            {
                throw; // rethrow the exception to propagate it up the call stack
            }
        }

        private static string GetAppAudioRef(string appEndpoint)
        {
            return RegistryHelper.ReadRegistryValue(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Multimedia\Audio\DefaultEndpoint\" + appEndpoint, "000_000");
        }

        public static bool isAudioMixerOK()
        {
            try
            {
                var guid = FindVBCableInputGUID();
                if (guid == "") return false;
                Int32 deviceState = (Int32)RegistryHelper.ReadRegistryBIN(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\" + guid, "DeviceState");
                if (deviceState != 1) return false; //Checks if vbcable input device is enabled
                var appEndpoint = FindChilloutVREndpointKey();
                if (appEndpoint == "") return false;
                var inputRef = RegistryHelper.ReadRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\" + guid + @"\Properties", "{9c119480-ddc2-4954-a150-5bd240d454ad},1");
                if (inputRef == "") return false;
                var appAudioRef = GetAppAudioRef(appEndpoint);
                if (appAudioRef == "") return false;
                if (inputRef == appAudioRef) return false; //Checks that ChilloutVR's audio isn't VBCable.

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static bool isListenModeOK()
        {
            try
            {
                var guidOut = FindVBCableOutputGUID();
                if (guidOut == "") return false;
                Int32 deviceState = (Int32)RegistryHelper.ReadRegistryBIN(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\" + guidOut, "DeviceState");
                if (deviceState != 1) return false; //Checks if vbcable output device is enabled
                                                    //Checks if listen mode ON
                byte[] outputBytes = (byte[])RegistryHelper.ReadRegistryBIN(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\" + guidOut + @"\Properties", "{24dbb0fc-9311-4b3d-9cf0-18ff155639d4},1");
                int relevantBytes = (outputBytes[8] << 8) | outputBytes[9];
                if (relevantBytes != 0xFFFF) return false; //Output device listen mode isn't marked ON.

                //Checks selected audio.
                var outputRef = RegistryHelper.ReadRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\" + guidOut + @"\Properties", "{24dbb0fc-9311-4b3d-9cf0-18ff155639d4},0");
                if (outputRef == "") return false;
                var guidIn = FindVBCableInputGUID();
                if (guidIn == "") return false;
                if (outputRef == "{0.0.0.00000000}." + guidIn) return false; //Checks that we don't transmit listen data to VBCable.
                var appAudioRef = GetAppAudioRef(FindChilloutVREndpointKey());
                if (appAudioRef == "") return false;
                if (!appAudioRef.Contains(outputRef)) return false; //Checks that the device we listen to outputs to the same device as the app.
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static void PopSoundGuide()
        {
            Misc.WindowsRun("https://github.com/Searaphim/CVR-PlayTogether/wiki/Audio-Setup-Quick-Guide");
        }

        public static void PopW10SoundMixer()
        {
            Misc.WindowsRun("ms-settings:apps-volume");
        }

        public static void PopSoundRecorders()
        {
            Misc.WindowsRun("rundll32.exe Shell32.dll,Control_RunDLL Mmsys.cpl,,1");
        }

        public static void PopVBCablePage()
        {
            Misc.WindowsRun("https://vb-audio.com/Cable/");
        }

        public static bool isVBCableInstalled()
        {
            try
            {
                var value = RegistryHelper.ReadRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\VB-Audio\Cable", "VBAudioCableWDM");
                if (value.Length > 0) return true;
            }
            catch (Exception e)
            {
                return false;
            }
            return false;
        }

        public static bool CheckAudioConfig()
        {
            return
            (
                isVBCableInstalled() &&
                isAudioMixerOK() &&
                isListenModeOK()
            );
        }
    }
}
