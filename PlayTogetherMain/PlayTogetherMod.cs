
using MelonLoader;
using ABI_RC.Core.Util.AssetFiltering;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using BTKUILib.UIObjects;
using BTKUILib;
using UnityEngine.SceneManagement;
using uWindowCapture;
using System.IO.Compression;
using System.IO;
using System.Diagnostics;
using MelonLoader.TinyJSON;
using PlayTogetherMod.Utils;
using System.Linq;
using BTKUILib.UIObjects.Components;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace PlayTogetherMod
{
    public class SharedVars
    {
        public const string RESOURCE_FOLDER = @"Mods\CVRPlayTogether_Data";
    }

    public class Sunshine
    {
        public const string EXE_PATH = SharedVars.RESOURCE_FOLDER + @"\Sunshine\sunshine.exe";
        public const string SETTINGS_PATH = SharedVars.RESOURCE_FOLDER + @"\Sunshine\config\sunshine.conf"; //Multicast may or may not be necessary for multiple clients, idk.
        public const string APPSCONF_PATH = SharedVars.RESOURCE_FOLDER + @"\Sunshine\assets\apps.json";
        public const string APPSCONF_PATH2 = SharedVars.RESOURCE_FOLDER + @"\Sunshine\config\apps.json";
        bool firstLaunch = true;
        public Process firststartprocess;
        public Process normalprocess;
        StreamWriter streamWriter;
        //Apps file is only read on launch so sunshine needs to restart on edit. Probably the same for other confs.
        //channels=3 (3 distinct streams) //3 for 4 players
        //Configuration variables can be overwritten on the command line: "name=value" --> it can be usefull to set min_log_level=debug without modifying the configuration file
        ProcessStartInfo firststartinfo = new ProcessStartInfo {
            FileName = EXE_PATH,
            WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Sunshine",
            Arguments = "--creds defaultusr defaultpwd", //Not a concern since we dont expose the interface to the internet
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };
        ProcessStartInfo normalstartinfo = new ProcessStartInfo
        {
            FileName = EXE_PATH,
            WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Sunshine",
            Arguments = "-p -0 -1",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = false //True somehow makes the process unable to receive our inputs
        };
        //Make a warning for users to make sure apps they add are always launched fullscreen for privacy reasons. Atl-tabbing risky, theres probably a way to prevent desktop from streaming. (apps.json?)

        public void WritePin(string pin)
        {
            streamWriter.WriteLine(pin);
            //streamWriter.Close();
        }

        public void Run(string appPath)
        {
            if (firstLaunch)
            {
                firstLaunch = false;
                firststartprocess = Process.Start(firststartinfo);
                if (!firststartprocess.HasExited)
                {
                    firststartprocess.WaitForExit();
                }
            }
            string appstr = @"{""name"":""" + Path.GetFileNameWithoutExtension(appPath) + @""",""cmd"":""" + Regex.Replace(appPath, @"\\|/", @"\\") + @""",""auto-detach"":""true"",""wait-all"":""true"",""image-path"":""steam.png""}";
            File.WriteAllText(APPSCONF_PATH, @"{""env"":{},""apps"":[" + appstr + @"]}"); //Temporary lazy fix for json issues
            File.WriteAllText(APPSCONF_PATH2, @"{""env"":{},""apps"":[" + appstr + @"]}"); //Temporary lazy fix for json issues
            normalprocess = Process.Start(normalstartinfo);
            streamWriter = normalprocess.StandardInput;
        }

        public void Stop()
        {
            if (!normalprocess.HasExited)
            {
                normalprocess.Kill();
                normalprocess.WaitForExit();
            }
        }
    }

    public class Moonlight
    {
        public const string EXE_PATH = SharedVars.RESOURCE_FOLDER + @"\Moonlight\Moonlight.exe";
        public const string INI_PATH = SharedVars.RESOURCE_FOLDER + @"\Moonlight\Moonlight Game Streaming Project\Moonlight.ini";
        public Process normalprocess;

        public void PairWithHost(string lobbyCode, string pairPin)
        {
            Process pairprocess = Process.Start(new ProcessStartInfo()
            {
                FileName = EXE_PATH,
                WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Moonlight",
                Arguments = $"pair {LobbyCodeHandler.LobbyCodeToIP(lobbyCode)} --pin {pairPin}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            });
        }

        public void ClearInitFile() //Intended to prevent endless accumulation of expired clients
        {
            File.Delete(INI_PATH); //Not sure about the implications of deleting the [gcmapping] field yet. May need granular control over this file depending on results.
        }

        public void Run()
        {
            //Would normally use the list command but it doesn't return anything when using the portable version of the apps. (could be either or both)
            normalprocess = Process.Start(new ProcessStartInfo()
            {
                FileName = EXE_PATH,
                WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Moonlight",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            });
        }
    }

    public class PlayTogether : MelonMod
    {
        Page _rootPage;
        public const string PROP_SCENE = "AdditiveContentScene";
        public const string MOONLIGHT_RESOURCE = "CVRPlayTogether.resources.MoonlightPortable-x64-5.0.1.zip";
        public const string SUNSHINE_RESOURCE = "CVRPlayTogether.resources.sunshine-windows-portable.zip";
        Sunshine _sunshine;
        Moonlight _moonlight;
        string pinInputs = "";
        string targetLobbyCode = "";

        private void UnpackResources()
        {
            ZipArchive zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(MOONLIGHT_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER + @"\Moonlight", true);
            zip.Dispose();
            zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(SUNSHINE_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER, true);
        }

        private string GeneratePairingPin()
        {
            var random = new System.Random();
            return random.Next(10000).ToString("D4");
        }

        private void MakeUI()
        {
            //!! Page() cannot have special characters for modName
            _rootPage = new Page("CVRPlayTogether", "Root Page", true)
            {
                MenuTitle = "CVR-PlayTogether Settings",
                MenuSubtitle = "Settings are applied to EVERY spawned screen"
            };

            var globalCat = _rootPage.AddCategory("Global Settings");

            var sliderFPS = globalCat.AddSlider("FPS", "Adjust the framerate.", 30f, 0f, 144f);
            var buttonApply = globalCat.AddButton("Apply", "", "Apply settings to active screens");
            buttonApply.OnPress += () =>
            {
                Scene sceneInstance = SceneManager.GetSceneByName(PROP_SCENE);
                if (!sceneInstance.IsValid()) return;
                GameObject[] gObjs = sceneInstance.GetRootGameObjects();
                foreach (var item in gObjs)
                {
                    uWindowCapture.UwcWindowTexture comp = item.gameObject.GetComponentInChildren<uWindowCapture.UwcWindowTexture>();
                    if (comp == null) return;
                    comp.captureFrameRate = (int)Math.Round(sliderFPS.SliderValue, 0);
                    //TODO: Add the rest of the settings here
                }
            };

            var hostCat = _rootPage.AddCategory("Game Hosting");
            var clientCat = _rootPage.AddCategory("Join Hosted Game");
            var viewCodeButton = hostCat.AddButton("My Lobby Code", "", "View your unique Host Lobby Code");
            viewCodeButton.Disabled = true;
            viewCodeButton.OnPress += () =>
            {
                string lobbyCode = LobbyCodeHandler.GenLobbyCode();
                QuickMenuAPI.ShowNotice("Your Lobby Code", "This is the code players must use to join your lobby if you are Hosting. *The code expires every 1-2 hours!*", null, lobbyCode);
            };
            var pinPage = hostCat.AddPage("Friend pairing", "", "Enter pairing PIN", "CVRPlayTogether");
            pinPage.Disabled = true;
            var hostToggle = hostCat.AddToggle("Host", "Select the game and start hosting", false);
            hostToggle.OnValueUpdated += b =>
            {
                if (b == true)
                {
                    var filePath = FileBrowser.BrowseForFile();
                    LoggerInstance.Msg($"Selected file: {filePath}");
                    if (filePath != "")
                    {
                        _sunshine.Run(filePath);
                        pinPage.Disabled = false;
                        viewCodeButton.Disabled = false;
                    }
                    else
                    {
                        hostToggle.ToggleValue = false;
                    }
                }
                else
                {
                    _sunshine.Stop();
                    LoggerInstance.Msg($"Hosting terminated.");
                    pinPage.Disabled = true;
                    viewCodeButton.Disabled = true;
                }
            };
            var pinNumpad = pinPage.AddCategory("PIN KEYBOARD");
            Category sendPinCat;
            sendPinCat = pinPage.AddCategory("Enter Friend's pairing PIN");
            var sendPinButton = sendPinCat.AddButton("", "", "Validate a friend's PIN");
            sendPinButton.OnPress += () =>
            {
                _sunshine.WritePin(pinInputs);
                pinNumpad.Disabled = false;
                pinInputs = "";
                sendPinButton.ButtonText = pinInputs;
            };
            var clearPinButton = sendPinCat.AddButton("Clear", "", "Clear PIN");
            clearPinButton.OnPress += () =>
            {
                pinNumpad.Disabled = false;
                pinInputs = "";
                sendPinButton.ButtonText = pinInputs;
            };
            List<Button> pinButtons = new List<Button>();
            for(int i =0; i < 10; i++)
            {
                int buttonNumber = i;
                pinButtons.Add(pinNumpad.AddButton(buttonNumber.ToString(), "", ""));
                pinButtons[buttonNumber].OnPress += () =>
                {
                    if (pinInputs.Length < 4)
                    {
                        pinInputs = pinInputs + buttonNumber.ToString();
                        sendPinButton.ButtonText = pinInputs;
                    }
                    if (pinInputs.Length == 4)
                    {
                        sendPinButton.ButtonText = pinInputs;
                        pinNumpad.Disabled = true;
                    }
                };
            }

            var pairPage = clientCat.AddPage("Connect to Lobby", "", "Pair yourself with a Host", "CVRPlayTogether");
            var joinButton = clientCat.AddButton("Join Game", "", "Attempt to join game");
            joinButton.OnPress += () => {
                _moonlight.Run();
            };
            var lobbyNumpad = pairPage.AddCategory("Enter lobby code..");
            var confirmCat = pairPage.AddCategory("Connect to lobby");
            var connectButton = confirmCat.AddButton("", "", "Attempt connection with Host");
            connectButton.OnPress += () =>
            {
                var pairingPin = GeneratePairingPin();
                QuickMenuAPI.ShowNotice("Pairing Pin", $"The host must enter this pin to approve your connection: {pairingPin}", null);
                _moonlight.PairWithHost(targetLobbyCode, pairingPin);
                lobbyNumpad.Disabled = false;
                targetLobbyCode = "";
                connectButton.ButtonText = targetLobbyCode;
            };
            var clearLobbyCodeButton = confirmCat.AddButton("Clear", "", "Clear Lobby code");
            clearLobbyCodeButton.OnPress += () =>
            {
                lobbyNumpad.Disabled = false;
                targetLobbyCode = "";
                connectButton.ButtonText = targetLobbyCode;
            };
            List<Button> lobbyCodeButtons = new List<Button>();
            for (int i = 0; i < 10; i++)
            {
                int buttonNumber = i;
                lobbyCodeButtons.Add(lobbyNumpad.AddButton(buttonNumber.ToString(), "", ""));
                lobbyCodeButtons[buttonNumber].OnPress += () =>
                {
                    if (targetLobbyCode.Length < 10)
                    {
                        targetLobbyCode = targetLobbyCode + buttonNumber.ToString();
                        connectButton.ButtonText = targetLobbyCode;
                    }
                    if (targetLobbyCode.Length == 10)
                    {
                        connectButton.ButtonText = targetLobbyCode;
                        lobbyNumpad.Disabled = true;
                    }
                };
            }
        }

        public override void OnInitializeMelon()
        {
            UnpackResources();
            //Our CCK Prop contains a custom MonoBehavior script component. We force-allow it here.
            var propWhitelist = SharedFilter._spawnableWhitelist;
            propWhitelist.Add(typeof(uWindowCapture.UwcWindowTexture));
            //Assembly assembly = Assembly.GetAssembly(typeof(uWindowCapture.UwcWindowTexture));
            //propWhitelist.Add(assembly.GetType("uWindowCapture.UwcWindowTexture"));

            _sunshine = new Sunshine();
            _moonlight = new Moonlight();
            _moonlight.ClearInitFile();
            MakeUI();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene {sceneName} with build index {buildIndex} has been loaded!");
        }

    }
}