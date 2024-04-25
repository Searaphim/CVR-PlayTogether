
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
using System.Runtime.InteropServices;

/*[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]

public class OpenFileName
{
    public int structSize = 0;
    public IntPtr dlgOwner = IntPtr.Zero;
    public IntPtr instance = IntPtr.Zero;
    public String filter = null;
    public String customFilter = null;
    public int maxCustFilter = 0;
    public int filterIndex = 0;
    public String file = null;
    public int maxFile = 0;
    public String fileTitle = null;
    public int maxFileTitle = 0;
    public String initialDir = null;
    public String title = null;
    public int flags = 0;
    public short fileOffset = 0;
    public short fileExtension = 0;
    public String defExt = null;
    public IntPtr custData = IntPtr.Zero;
    public IntPtr hook = IntPtr.Zero;
    public String templateName = null;
    public IntPtr reservedPtr = IntPtr.Zero;
    public int reservedInt = 0;
    public int flagsEx = 0;
}

public class DllTest
{
    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    public static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
    public static bool GetOpenFileName1([In, Out] OpenFileName ofn)
    {
        return GetOpenFileName(ofn);
    }
}*/


namespace PlayTogetherMod
{
    public class SharedVars
    {
        public const string RESOURCE_FOLDER = "Mods/CVRPlayTogether_Data";
        public const string MOONLIGHT_PATH = RESOURCE_FOLDER + "/Moonlight/Moonlight.exe";
    }



    public class Sunshine : Process
    {
        public const string EXE_PATH = SharedVars.RESOURCE_FOLDER + "/Sunshine/sunshine.exe";
        public const string SETTINGS_PATH = SharedVars.RESOURCE_FOLDER + "/Sunshine/config/sunshine.conf"; //Multicast may or may not be necessary for multiple clients, idk.
        public const string APPSCONF_PATH = SharedVars.RESOURCE_FOLDER + "/Sunshine/config/apps.json"; //Multicast may or may not be necessary for multiple clients, idk.
        //Apps file is only read on launch so sunshine needs to restart on edit. Probably the same for other confs.
        //channels=3 (3 distinct streams) //3 for 4 players
        //Configuration variables can be overwritten on the command line: "name=value" --> it can be usefull to set min_log_level=debug without modifying the configuration file
        public Sunshine() : base()
        {
            StartInfo.FileName = EXE_PATH;
            StartInfo.Arguments = "-p -0 -1"; //PINs can be written to stdin
            //stdout will read "Please insert pin: " when ready to receive a pin through stdin
            //Make a warning for users to make sure apps they add are always launched fullscreen for privacy reasons. Atl-tabbing risky, theres probably a way to prevent desktop from streaming. (apps.json?)
        }

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
            public List<string>? env;
            public List<AppInfo>? apps;
        }

        public AppsConf ReadAppsConf()
        {
            return JSON.Load(File.ReadAllText(APPSCONF_PATH)).Make<AppsConf>();
        }

        public void WriteAppsConf(AppsConf appsConf)
        {
            File.WriteAllText(APPSCONF_PATH, JSON.Dump(appsConf, EncodeOptions.PrettyPrint));
        }

        public void WritePin(string pin)
        {
            Console.WriteLine(pin);
        }

        public void Run(bool FirstRun = false)
        {
            if (FirstRun)
            {
                WriteAppsConf(new AppsConf());
                Start(StartInfo.FileName, "--creds defaultusr defaultpwd"); //Not a concern since we dont expose the interface to the internet
                WaitForExit();
            }
            Start();
            //Console.ReadLine(); 
        }

        public void Restart()
        {
            Close();
            WaitForExit();
            Run();
        }
    }

    public class PlayTogether : MelonMod
    {
        private Page _rootPage;
        public const string PROP_SCENE = "AdditiveContentScene";
        //Root working dir at runtime is 'ChilloutVR'
        public const string MOONLIGHT_RESOURCE = "CVRPlayTogether.resources.MoonlightPortable-x64-5.0.1.zip";
        public const string SUNSHINE_RESOURCE = "CVRPlayTogether.resources.sunshine-windows-portable.zip";
        //private Sunshine _sunshine = new Sunshine();


        private void UnpackResources()
        {
            ZipArchive zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(MOONLIGHT_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER + "/Moonlight", true);
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

            /*var hostCat = _rootPage.AddCategory("Game Hosting");
            var buttonBrowse = hostCat.AddButton("Browse", "", "Select the game you wish to play");
            buttonBrowse.OnPress += () =>
            {
                OpenFileName ofn = new OpenFileName();
                ofn.structSize = Marshal.SizeOf(ofn);
                ofn.filter = "All Files\0*.*\0\0";
                ofn.file = new string(new char[256]);
                ofn.maxFile = ofn.file.Length;
                ofn.fileTitle = new string(new char[64]);
                ofn.maxFileTitle = ofn.fileTitle.Length;
                ofn.initialDir = UnityEngine.Application.dataPath;
                ofn.title = "Upload Image";
                ofn.defExt = "PNG";
                ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008;//OFN_EXPLORER|OFN_FILEMUSTEXIST|OFN_PATHMUSTEXIST| OFN_ALLOWMULTISELECT|OFN_NOCHANGEDIR
                if (DllTest.GetOpenFileName(ofn))
                {
                    LoggerInstance.Msg($"Selected file: {ofn.file}");
                    //FileDialogResult("file:///" + ofn.file);
                }
            };*/
        }

        public override void OnInitializeMelon()
        {
            UnpackResources();
            //Our CCK Prop contains a custom MonoBehavior script component. We force-allow it here.
            var propWhitelist = SharedFilter._spawnableWhitelist;
            propWhitelist.Add(typeof(uWindowCapture.UwcWindowTexture));
            //Assembly assembly = Assembly.GetAssembly(typeof(uWindowCapture.UwcWindowTexture));
            //propWhitelist.Add(assembly.GetType("uWindowCapture.UwcWindowTexture"));

            //UI
            MakeUI();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene {sceneName} with build index {buildIndex} has been loaded!");
        }

    }
}