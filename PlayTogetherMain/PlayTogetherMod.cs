
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

namespace PlayTogetherMod
{
    public class SharedVars
    {
        public const string RESOURCE_FOLDER = @"Mods\CVRPlayTogether_Data";
        public const string MOONLIGHT_PATH = RESOURCE_FOLDER + @"\Moonlight\Moonlight.exe";
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
            Arguments = "--creds defaultusr defaultpwd",
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
            RedirectStandardInput = true, //Makes the process stall if UseShellExecute isnt set to false
            RedirectStandardOutput = false //True somehow makes the process unable to receive our inputs
        };
        //stdout will read "Please insert pin: " when ready to receive a pin through stdin
        //Make a warning for users to make sure apps they add are always launched fullscreen for privacy reasons. Atl-tabbing risky, theres probably a way to prevent desktop from streaming. (apps.json?)

        public struct AppInfo
        {
            public string name;
            public string cmd;
            [DecodeAlias("auto-detach")]
            public bool auto_detach;
            [DecodeAlias("wait-all")]
            public bool wait_all;
            [DecodeAlias("image-path")]
            public string image_path;
        }

        public class AppsConf
        {
            //[Exclude]
            //public List<string> env;
            public List<AppInfo> apps;
        }

        public AppsConf ReadAppsConf()
        {
            return JSON.Load(File.ReadAllText(APPSCONF_PATH)).Make<AppsConf>();
        }

        public AppsConf BuildAppInfo(string appPath)
        {
            var conf = ReadAppsConf();
            //conf.env.Clear();
            conf.apps.Clear();
            conf.apps.Add(new AppInfo { name = Path.GetFileNameWithoutExtension(appPath), cmd = appPath });
            return conf;
        }

        public void WriteAppsConf(AppsConf appsConf)
        {
            File.WriteAllText(APPSCONF_PATH, JSON.Dump(appsConf, EncodeOptions.PrettyPrint));
        }

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
                firststartprocess = Process.Start(firststartinfo); //Not a concern since we dont expose the interface to the internet
                if (!firststartprocess.HasExited)
                {
                    firststartprocess.WaitForExit();
                }
            }
            //WriteAppsConf(BuildAppInfo(appPath)); //Broken
            File.WriteAllText(APPSCONF_PATH, @"{""env"":{},""apps"":[]}"); //Temporary lazy fix for json issues
            File.WriteAllText(APPSCONF_PATH2, @"{""env"":{},""apps"":[]}"); //Temporary lazy fix for json issues
            normalprocess = Process.Start(normalstartinfo);
            streamWriter = normalprocess.StandardInput;
            //Console.ReadLine(); 
        }

        public void Stop()
        {
            if (!normalprocess.HasExited)
            {
                normalprocess.Close();
                normalprocess.WaitForExit();
            }
        }
    }

    public class PlayTogether : MelonMod
    {
        Page _rootPage;
        public const string PROP_SCENE = "AdditiveContentScene";
        //Root working dir at runtime is 'ChilloutVR'
        public const string MOONLIGHT_RESOURCE = "CVRPlayTogether.resources.MoonlightPortable-x64-5.0.1.zip";
        public const string SUNSHINE_RESOURCE = "CVRPlayTogether.resources.sunshine-windows-portable.zip";
        Sunshine _sunshine;
        string pinInputs = "";


        private void UnpackResources()
        {
            ZipArchive zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(MOONLIGHT_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER + @"\Moonlight", true);
            zip.Dispose();
            zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(SUNSHINE_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER, true);
        }

        private void MakeUI()
        {
            //!! BTKUILib Bug here: Page() cannot have special characters for modName
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
            var pinPage = hostCat.AddPage("ENTER FRIEND PIN", "", "PIN", "CVRPlayTogether");
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
                        //try
                        //{
                            //AppsConf appsconf;
                            //JSON.MakeInto(JSON.Load(File.ReadAllText(APPSCONF_PATH)), out appsconf); //All of them throw an Exception about during invocation or whatevr
                            //JSON.Load(File.ReadAllText(APPSCONF_PATH)).Make(out appsconf); 
                            //appsconf = JSON.Load(File.ReadAllText(APPSCONF_PATH)).Make<AppsConf>(); //Make is the culprit
                            //If it still doesnt work I can just interact with the json object directly instead...
                        //}
                        //catch (Exception ex)
                        //{
                        //    LoggerInstance.Msg(ex.Message);
                        //}
                        //_sunshine.ReadAppsConf(); //this stalls
                        //LoggerInstance.Msg($"{_sunshine.BuildAppInfo(filePath)}");//Seems to stall
                        LoggerInstance.Msg($"PROCESS_UNSTUCK");
                        pinPage.Disabled = false;
                    }
                    else
                    {
                        hostToggle.ToggleValue = false;
                    }
                }
                else
                {
                    _sunshine.Stop();
                    pinPage.Disabled = true;
                }
            };
            var pinKeyboard = pinPage.AddCategory("PIN KEYBOARD");
            Category sendCat;
            sendCat = pinPage.AddCategory("Enter PIN");
            var sendButton = sendCat.AddButton("", "", "Validate a friend's PIN");
            sendButton.OnPress += () =>
            {
                _sunshine.WritePin(pinInputs);
                pinKeyboard.Disabled = false;
                pinInputs = "";
                sendButton.ButtonText = pinInputs;
            };
            List<Button> pinButtons = new List<Button>();
            for(int i =0; i < 10; i++)
            {
                int buttonNumber = i;
                pinButtons.Add(pinKeyboard.AddButton(buttonNumber.ToString(), "", ""));
                pinButtons[buttonNumber].OnPress += () =>
                {
                    if (pinInputs.Length < 4)
                    {
                        pinInputs = pinInputs + buttonNumber.ToString();
                        sendButton.ButtonText = pinInputs;
                    }
                    if (pinInputs.Length == 4)
                    {
                        sendButton.ButtonText = pinInputs;
                        pinKeyboard.Disabled = true;
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
            MakeUI();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene {sceneName} with build index {buildIndex} has been loaded!");
        }

    }
}