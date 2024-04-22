
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

namespace PlayTogetherMod
{
    public class PlayTogether : MelonMod
    {
        private Page _rootPage;
        public const string PROP_SCENE = "AdditiveContentScene";
        //Root working dir at runtime is 'ChilloutVR'
        public const string RESOURCE_FOLDER = "Mods/CVRPlayTogether_Data";
        public const string MOONLIGHT_PATH = RESOURCE_FOLDER + "/Moonlight/Moonlight.exe";
        public const string SUNSHINE_PATH = RESOURCE_FOLDER + "/Sunshine/sunshine.exe";
        public const string MOONLIGHT_RESOURCE = "CVRPlayTogether.resources.MoonlightPortable-x64-5.0.1.zip";
        public const string SUNSHINE_RESOURCE = "CVRPlayTogether.resources.sunshine-windows-portable.zip";


        private void UnpackResources()
        {
            ZipArchive zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(MOONLIGHT_RESOURCE));
            zip.ExtractToDirectory(RESOURCE_FOLDER + "/Moonlight", true);
            zip.Dispose();
            zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(SUNSHINE_RESOURCE));
            zip.ExtractToDirectory(RESOURCE_FOLDER, true);
        }

        private void MakeUI()
        {
            _rootPage = new Page("CVR-PlayTogether", "Root Page", true);
            _rootPage.MenuTitle = "CVR-PlayTogether Settings";
            _rootPage.MenuSubtitle = "Settings are applied to EVERY spawned screen";

            var category = _rootPage.AddCategory("Global Settings");

            var sliderFPS = category.AddSlider("FPS", "Adjust the framerate.", 30f, 0f, 144f);
            var buttonApply = category.AddButton("Apply", "", "Apply settings to active screens");
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