
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
using BTKUILib.UIObjects.Components;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using System.Text;
using static ABI_RC.Systems.Safety.BundleVerifier.RestrictedProcessRunner.Interop.InteropMethods;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.ComponentModel;
using RTG;

namespace PlayTogetherMod
{
    public class SharedVars
    {
        public const string RESOURCE_FOLDER = @"Mods\CVRPlayTogether_Data";
        public const string UWINDOWCAPTURE_DLL_PATH = @"ChilloutVR_Data\Plugins\x86_64\uWindowCapture.dll";
    }

    public class Sunshine
    {
        private const string EXE_PATH = SharedVars.RESOURCE_FOLDER + @"\Sunshine\sunshine.exe";
        private const string SETTINGS_PATH = SharedVars.RESOURCE_FOLDER + @"\Sunshine\config\sunshine.conf"; // Multicast may or may not be necessary for multiple clients, idk yet.
        private const string APPSDEFCONF_PATH = SharedVars.RESOURCE_FOLDER + @"\Sunshine\assets\apps.json";
        private const string APPSCONF_DIR = SharedVars.RESOURCE_FOLDER + @"\Sunshine\config";

        private const string APPSCONF_PATH = APPSCONF_DIR + @"\apps.json";
        private Process? normalprocess;
        private string _url = "https://localhost:47990/api/pin";
        private string _usr = "defaultusr";
        private string _pwd = "defaultpwd";
        private string _HostedAppName = "";
        public SunshineConf Config = new SunshineConf();

        public string HostedAppName
        {
            get { return _HostedAppName; }
            set { _HostedAppName = value; } 
        }

        public class SunshineConf 
        {
            public string virtual_sink {  get; set; }
            public bool keyboard {  get; set; }
            public bool mouse { get; set; }
            public string origin_web_ui_allowed { get; set; }
            public bool upnp { get; set; }

            public SunshineConf() 
            {
                this.virtual_sink = "VB-Audio Virtual Cable";
                this.keyboard = false;
                this.mouse = false;
                this.origin_web_ui_allowed = "pc";
                this.upnp = true;
            }
        }

        ~Sunshine() {
            Stop();
        }

        // Apps file is only read on launch so sunshine needs to restart on edit. Probably the same for other confs.
        // channels=3 (3 distinct streams) //3 for 4 players
        // Configuration variables can be overwritten on the command line: "name=value" --> it can be usefull to set min_log_level=debug without modifying the configuration file
        ProcessStartInfo normalstartinfo = new ProcessStartInfo
        {
            FileName = EXE_PATH,
            WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Sunshine",
            Arguments = "-p",
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false
        };
        // Make a warning for users to make sure apps they add are always launched fullscreen for privacy reasons. Atl-tabbing risky, theres probably a way to prevent desktop from streaming. (apps.json?)

        public void CreateUser()
        {
            ProcessStartInfo credsProc = new ProcessStartInfo
            {
                FileName = EXE_PATH,
                WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Sunshine",
                Arguments = $"--creds {_usr} {_pwd}",
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false
            };
            Process shortProc = Process.Start(credsProc); //Not a concern since we dont expose the interface to the internet
            shortProc.WaitForExit();
            shortProc.Dispose();
        }

        public async Task<bool> SendPin(string pin)
        {
            //Disables certificates. For now Im not sure how to add the server's certificate to the unity runtime's trust store or if I even should.
            RemoteCertificateValidationCallback cbCertValidation = (sender, certificate, chain, sslPolicyErrors) => true;
            ServicePointManager.ServerCertificateValidationCallback += cbCertValidation;
            try
            {
                var handler = new HttpClientHandler { Credentials = new NetworkCredential(_usr, _pwd) };
                var client = new HttpClient(handler);
                var request = new HttpRequestMessage(HttpMethod.Post, _url);
                var payload = @"{""pin"":""" + pin + @"""}";
                var content = new StringContent(payload, Encoding.UTF8, "text/plain");
                request.Content = content;
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                // Parse the response using regular expressions
                var regex = new Regex(@"\{\s*""status""\s*:\s*""(true|false)""\s*\}");
                var match = regex.Match(responseContent);
                if (match.Success)
                {
                    var status = match.Groups[1].Value;
                    return status == "true";
                }
                else
                {
                    return false;
                }
            }
            catch(Exception ex)
            {
                return false;
            }
            finally {
                //Restore default certification validation
                ServicePointManager.ServerCertificateValidationCallback -= cbCertValidation;
            }
        }

        private void FlushConfigs()
        {
            if (!Directory.Exists(APPSCONF_DIR)) return;
            string[] files = Directory.GetFiles(APPSCONF_DIR);
            foreach (string file in files)
            {
                File.Delete(file);
            }
        }

        private void GenerateConfigs()
        {
            if(!Directory.Exists(APPSCONF_DIR))
                Directory.CreateDirectory(APPSCONF_DIR);

            using (var writer = File.CreateText(SETTINGS_PATH))
            {
                foreach (var property in typeof(SunshineConf).GetProperties())
                {
                    var value = property.GetValue(Config);
                    if (value != null)
                    {
                        switch (property.PropertyType.Name)
                        {
                            case "Boolean":
                                writer.WriteLine($"{property.Name} = {((bool)value ? "enabled" : "disabled")}");
                                break;
                            default:
                                writer.WriteLine($"{property.Name} = {value}");
                                break;
                        }
                    }
                }
            }
        }

        public void Run(string appPath)
        {
            FlushConfigs();
            GenerateConfigs();
            CreateUser();
            _HostedAppName = Path.GetFileNameWithoutExtension(appPath);
            string appstr = @"{""name"":""" + _HostedAppName + @""",""cmd"":""" + Regex.Replace(appPath, @"\\|/", @"\\") + @""",""auto-detach"":""true"",""wait-all"":""true"",""image-path"":""steam.png""}";
            File.WriteAllText(APPSDEFCONF_PATH, @"{""env"":{},""apps"":[" + appstr + @"]}"); //Temporary lazy fix for json issues
            File.WriteAllText(APPSCONF_PATH, @"{""env"":{},""apps"":[" + appstr + @"]}"); //Temporary lazy fix for json issues
            normalprocess = Process.Start(normalstartinfo);
        }

        public void Stop()
        {
            if (normalprocess != null)
            {
                if (!normalprocess.HasExited)
                {
                    normalprocess.Kill();
                    normalprocess.WaitForExit();
                }
                normalprocess.Dispose();
                normalprocess = null;
            }
        }
    }

    public class Moonlight
    {
        private const string EXE_PATH = SharedVars.RESOURCE_FOLDER + @"\Moonlight\Moonlight.exe";
        private const string INI_PATH = SharedVars.RESOURCE_FOLDER + @"\Moonlight\Moonlight Game Streaming Project\Moonlight.ini";
        private Process? _sessionProc = null;
        private Process? _pairprocess = null;
        private string _lobbyCode = "";
        private string _lobbyDestination = "";

        public Process? SessionProcess { get { return _sessionProc; } }

        ~Moonlight() {
            StopPairing();
            StopSession();
        }

        public string LobbyCode
        {
            get { return _lobbyCode; }
            set { _lobbyCode = value; }
        }

        private string? GetHostAppTitle()
        {
            return new IniParserHelper().ExtractAppName(INI_PATH);
        }

        private void FlushConfigs()
        {
            if (File.Exists(INI_PATH))
            {
                File.Delete(INI_PATH);
            }
        }

        public void PairWithHost(string pairPin)
        {
            StopPairing();
            FlushConfigs();
            _lobbyDestination = LobbyCodeHandler.LobbyCodeToIP(_lobbyCode);
            _pairprocess = Process.Start(new ProcessStartInfo()
            {
                FileName = EXE_PATH,
                WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Moonlight",
                Arguments = $"pair {_lobbyDestination} --pin {pairPin}",
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false
            });
        }

        public void ClearInitFile() //Intended to prevent endless accumulation of expired clients
        {
            if(File.Exists(INI_PATH))
                File.Delete(INI_PATH); //Not sure about the implications of deleting the [gcmapping] field yet. May need granular control over this file depending on results.
        }

        public bool IsAppBlacklisted(string appTitle)
        {
            if(appTitle == "Desktop")
                return true;
            return false;
        }

        private void StartSession(string host, string? hostAppTitle)
        {
            if ((hostAppTitle == null) || (hostAppTitle == "") || (host == "") || (host == null))
            {
                return;
            }
            if (IsAppBlacklisted(hostAppTitle))
                return;
            _sessionProc = Process.Start(new ProcessStartInfo()
            {
                FileName = EXE_PATH,
                WorkingDirectory = SharedVars.RESOURCE_FOLDER + @"\Moonlight",
                Arguments = @$"stream {host} ""{hostAppTitle}""",
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false
            });
        }

        public void Run()
        {
            StartSession(_lobbyDestination, GetHostAppTitle());
        }

        private bool StopProcess(Process? proc)
        {
            if (proc != null)
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit();
                }
                return true;
            }
            return false;
        }

        public void StopPairing()
        {
            if (StopProcess(_pairprocess))
            {
                _pairprocess.Dispose();
                _pairprocess = null;
            }

        }

        public void StopSession()
        {
            if (StopProcess(_sessionProc))
            {
                _sessionProc.Dispose();
                _sessionProc = null;
            }
        }
    }

    public class PlayTogether : MelonMod
    {
        private Page _rootPage;
        private const string PROP_SCENE = "AdditiveContentScene";
        private const string MANAGER_SCENE = "DontDestroyOnLoad";
        private const string MOONLIGHT_RESOURCE = "CVRPlayTogether.resources.MoonlightPortable-x64-5.0.1.zip";
        private const string SUNSHINE_RESOURCE = "CVRPlayTogether.resources.sunshine-windows-portable.zip";
        private const string UWINDOWCAPTURE_RESOURCE = "CVRPlayTogether.resources.uWindowCapture.dll";
        private Sunshine _sunshine;
        private Moonlight _moonlight;
        private string _pinInputs = "";
        private string _tempTargetLobbyCode = "";
        private IEnumerable<Process> _processList;
        private IEnumerator<Process> _processEnum;
        private MonitorIterator _monitorIterator = new MonitorIterator();

        private bool CheckGamepadDriver()
        {
            ProcessStartInfo credsProc = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-c Exit $(if ((Get-Item \"$env:SystemRoot\\System32\\drivers\\ViGEmBus.sys\").VersionInfo.FileVersion -ge [System.Version]\"1.17\") { 2 } Else { 1 })",
                CreateNoWindow = true
            };
            Process shortProc = Process.Start(credsProc); //Not a concern since we dont expose the interface to the internet
            shortProc.WaitForExit();
            if (shortProc.ExitCode == 2)
                return true;
            return false;
        }

        private void UIHandleGPDriver()
        {
            if (!CheckGamepadDriver())
            {
                QuickMenuAPI.ShowConfirm
                (
                    "Gamepad Support Info",
                    "[!] Your system has a missing or outdated critical component for gamepad support. Clicking 'Proceed' will open a link on your browser for you to download and install 'ViGEmBus_1.22.0_x64_x86_arm64.exe'",
                    () => { Misc.WindowsRun("https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0"); },
                    null,
                    "Proceed",
                    "Ignore"
                );
            }
        }

        private void DLLResourceLoader(string sourcePath, string destPath)
        {
            byte[] dllBytes = null;
            using (Stream stm = Assembly.GetExecutingAssembly().GetManifestResourceStream(sourcePath))
            {
                dllBytes = new byte[(int)stm.Length];
                stm.Read(dllBytes, 0, (int)stm.Length);
            }
            File.WriteAllBytes(destPath, dllBytes);
            NativeLibrary.LoadLib(destPath);
        }

        private void DeleteAllFolderContents(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                return;
            }
            foreach (string filePath in Directory.GetFiles(folderPath))
            {
                File.Delete(filePath);
            }
            foreach (string subFolderPath in Directory.GetDirectories(folderPath))
            {
                DeleteAllFolderContents(subFolderPath);
                Directory.Delete(subFolderPath);
            }
        }

        private void UnpackResources()
        {
            DeleteAllFolderContents(SharedVars.RESOURCE_FOLDER);
            ZipArchive zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(MOONLIGHT_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER + @"\Moonlight");
            zip.Dispose();
            zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(SUNSHINE_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER);
            DLLResourceLoader(UWINDOWCAPTURE_RESOURCE, SharedVars.UWINDOWCAPTURE_DLL_PATH);
        }

        private string GeneratePairingPin()
        {
            var random = new System.Random();
            return random.Next(10000).ToString("D4");
        }

        private int GetLobbyCodeMaxDigits(string codeStr)
        {
            if (codeStr.Contains("."))
                return 15;
            else
                return 10;
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
            var monitorCycleBtn = globalCat.AddButton("Change monitor", "", "Click to change to next monitor");
            monitorCycleBtn.OnPress += () => 
            {
                _monitorIterator.SetMonitorCount(UwcManager.desktopCount);
                Dictionary<string, object> changes = new Dictionary<string, object>
                {
                    {"desktopIndex", _monitorIterator.Next()}
                };
                Scene sceneInstance = SceneManager.GetSceneByName(PROP_SCENE);
                if (!sceneInstance.IsValid()) return;
                EditUwcWindowTextures(sceneInstance, changes, false);
            };
            var desktopModeToggle = globalCat.AddToggle("Desktop Mode", "Toggle Mode", true);
            desktopModeToggle.OnValueUpdated += b =>
            {
                if (b == true)
                {
                    Dictionary<string, object> changes = new Dictionary<string, object>
                    {
                        {"type", WindowTextureType.Desktop}
                    };
                    Scene sceneInstance = SceneManager.GetSceneByName(PROP_SCENE);
                    if (!sceneInstance.IsValid()) return;
                    EditUwcWindowTextures(sceneInstance, changes, false);
                    MelonLogger.Msg($"Desktop mode applied.");
                }
                else
                {
                    string windowTitle = "";
                    Process[] processes = Process.GetProcessesByName(_sunshine.HostedAppName);
                    _processList = processes;
                    if(processes.Length != 0) //Means we're currently hosting an app.
                    {
                        _processList = processes;
                        _processEnum = _processList.GetEnumerator();
                        _processEnum.MoveNext();
                        windowTitle = _processEnum.Current.MainWindowTitle;
                        MelonLogger.Msg($"Title1: {windowTitle}");
                    }
                    else { //Means we're not hosting an app. //We stay on Desktop Mode if both moonlight and sunshine arent doing anything
                        if (_moonlight.SessionProcess == null) return;
                        windowTitle = _moonlight.SessionProcess.MainWindowTitle;
                        MelonLogger.Msg($"Title2: {windowTitle}");
                    }
                    Dictionary<string, object> changes = new Dictionary<string, object>
                    {
                        {"type", WindowTextureType.Window},
                        {"partialWindowTitle", windowTitle}
                    };
                    Scene sceneInstance = SceneManager.GetSceneByName(PROP_SCENE);
                    if (!sceneInstance.IsValid()) return;
                    EditUwcWindowTextures(sceneInstance, changes, false);
                    MelonLogger.Msg($"Window mode applied.");
                }
            };
            var buttonApply = globalCat.AddButton("Apply FPS", "", "Apply setting to active screens");
            buttonApply.OnPress += () =>
            {
                int framerate = (int)Math.Round(sliderFPS.SliderValue, 0);
                Dictionary<string, object> changes = new Dictionary<string, object>
                {
                    {"captureFrameRate", framerate}
                };
                Scene sceneInstance = SceneManager.GetSceneByName(PROP_SCENE);
                if (!sceneInstance.IsValid()) return;
                EditUwcWindowTextures(sceneInstance, changes, true);
                MelonLogger.Msg($"Applied fps: {framerate}");
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
            var hostConfPage = hostCat.AddPage("Config", "dummy.png","Host configurations", "CVRPlayTogether");
            var hostAudioCat = hostConfPage.AddCategory("Audio");
            var hostNetworkCat = hostConfPage.AddCategory("Network");
            var hostControllersCat = hostConfPage.AddCategory("Controllers");
            var hostControllersTestBtn = hostControllersCat.AddButton("Game Controllers","dummy.png","Pop the game controllers desktop window");
            hostControllersTestBtn.OnPress += () => { Misc.WindowsRun("rundll32.exe shell32.dll,Control_RunDLL joy.cpl"); };
            var hostControllersHelpBtn = hostControllersCat.AddButton("Web Help", "", "Opens up the controllers help page on your web browser.");
            hostControllersHelpBtn.OnPress += () => { Misc.WindowsRun("https://github.com/Searaphim/CVR-PlayTogether/wiki/Controllers"); };
            var upnpBtn = hostNetworkCat.AddToggle("UPNP", "Automatic port forwarding. Not all routers support it. Restart Host to Apply", true);
            upnpBtn.OnValueUpdated += b =>
            {
                _sunshine.Config.upnp = b;
            };
            var hostNwkHelpBtn = hostNetworkCat.AddButton("Web Help", "", "Opens up the networking help page on your web browser.");
            hostNwkHelpBtn.OnPress += () => { Misc.WindowsRun("https://github.com/Searaphim/CVR-PlayTogether/wiki/Networking"); };
            var aMixerBtn = hostAudioCat.AddButton("Audio Mixer", "dummy.png", "Opens on your Desktop");
            aMixerBtn.OnPress += () => { AudioHelper.PopW10SoundMixer(); };
            var aListen = hostAudioCat.AddButton("Audio Devices", "dummy.png", "Opens on your Desktop");
            aListen.OnPress += () => { AudioHelper.PopSoundRecorders(); };
            var aHelpBtn = hostAudioCat.AddButton("Web Help", "", "Opens up the audio help page on your web browser.");
            aHelpBtn.OnPress += () => { AudioHelper.PopSoundGuide(); };
            var pairPage = clientCat.AddPage("Connect to Lobby", "", "Connect & Pair yourself to a Host", "CVRPlayTogether");
            var joinToggle = clientCat.AddToggle("Join Game", "Attempt to join game", false);
            hostToggle.OnValueUpdated += async b =>
            {
                if (b == true)
                {
                    UIHandleGPDriver();
                    if (!AudioHelper.CheckAudioConfig()) //Adding a post-launch check as well is worth considering
                    {
                        QuickMenuAPI.ShowConfirm("Audio Config Notice",
                            "*PLEASE READ*: Hosting requires special audio configuration and improper configuration has been detected. You only need to do this once. Click 'Quick Guide' to pop a Web guide on your desktop.",
                            () => { AudioHelper.PopSoundGuide(); },
                            () => { },
                            "Quick Guide",
                            "Cancel"
                        );
                        hostToggle.ToggleValue = false;
                        return;
                    }
                    QuickMenuAPI.ShowNotice("Select App.", "A new File Browser window appeared on your Desktop. Use it to select the application you want to Host.");
                    hostToggle.Disabled = true;
                    var filePath = await FileBrowser.BrowseForFile();
                    hostToggle.Disabled = false;
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
            var clearPinButton = sendPinCat.AddButton("Clear", "", "Clear PIN");
            sendPinButton.OnPress += async () =>
            {
                sendPinButton.Disabled = true;
                clearPinButton.Disabled = true;
                Action actionContinue = () =>
                {
                    pinNumpad.Disabled = false;
                    _pinInputs = "";
                    sendPinButton.ButtonText = _pinInputs;
                    sendPinButton.Disabled = false;
                    clearPinButton.Disabled = false;
                };
                var response = await _sunshine.SendPin(_pinInputs);
                if (response)
                    QuickMenuAPI.ShowNotice("Pairing Result", "Pairing Success! The application will launch once a client has joined.", actionContinue);
                else QuickMenuAPI.ShowNotice("Pairing Result", "Pairing Failed.", actionContinue);
            };
            clearPinButton.OnPress += () =>
            {
                pinNumpad.Disabled = false;
                _pinInputs = "";
                sendPinButton.ButtonText = _pinInputs;
            };
            List<Button> pinButtons = new List<Button>();
            for(int i =0; i < 10; i++)
            {
                int buttonNumber = i;
                pinButtons.Add(pinNumpad.AddButton(buttonNumber.ToString(), "", "dummy.png"));
                pinButtons[buttonNumber].OnPress += () =>
                {
                    if (_pinInputs.Length < 4)
                    {
                        _pinInputs = _pinInputs + buttonNumber.ToString();
                        sendPinButton.ButtonText = _pinInputs;
                    }
                    if (_pinInputs.Length == 4)
                    {
                        sendPinButton.ButtonText = _pinInputs;
                        pinNumpad.Disabled = true;
                    }
                };
            }

            joinToggle.OnValueUpdated += b =>
            {
                if(b == true)
                {
                    UIHandleGPDriver();
                    _moonlight.Run();
                }
                else
                {
                    _moonlight.StopSession();
                }
            };
            var lobbyNumpad = pairPage.AddCategory("Enter lobby code..");
            var confirmCat = pairPage.AddCategory("Connect to lobby");
            var connectButton = confirmCat.AddButton("", "", "Attempt connection with Host");
            connectButton.OnPress += () =>
            {
                if (connectButton.ButtonText == "")
                    return;
                var pairingPin = GeneratePairingPin();
                QuickMenuAPI.ShowNotice("Pairing Pin", 
                    $"Please only press OK after Pairing succeeded or failed. The host will tell you. -> They must enter this pin to approve your connection: {pairingPin}",
                    () => { 
                            _moonlight.StopPairing();
                        _rootPage.OpenPage();
                          });
                _moonlight.LobbyCode = connectButton.ButtonText;
                _moonlight.PairWithHost(pairingPin);
            };
            var clearLobbyCodeButton = confirmCat.AddButton("Clear", "", "Clear Lobby code");
            clearLobbyCodeButton.OnPress += () =>
            {
                lobbyNumpad.Disabled = false;
                _tempTargetLobbyCode = "";
                connectButton.ButtonText = "";
            };
            List<Button> lobbyCodeButtons = new List<Button>();
            for (int i = 0; i < 10; i++)
            {
                int buttonNumber = i;
                lobbyCodeButtons.Add(lobbyNumpad.AddButton(buttonNumber.ToString(), "dummy.png", ""));
                lobbyCodeButtons[buttonNumber].OnPress += () =>
                {
                    var maxDigits = GetLobbyCodeMaxDigits(_tempTargetLobbyCode);
                    if (_tempTargetLobbyCode.Length < maxDigits)
                    {
                        _tempTargetLobbyCode = _tempTargetLobbyCode + buttonNumber.ToString();
                        connectButton.ButtonText = _tempTargetLobbyCode;
                    }
                    if (_tempTargetLobbyCode.Length == maxDigits)
                    {
                        connectButton.ButtonText = _tempTargetLobbyCode;
                        lobbyNumpad.Disabled = true;
                    }
                };
            }
            var dotButton = lobbyNumpad.AddButton(".", "dummy.png", "");
            dotButton.OnPress += () =>
            {
                var maxDigits = GetLobbyCodeMaxDigits(_tempTargetLobbyCode);
                if (_tempTargetLobbyCode.Length < maxDigits)
                {
                    _tempTargetLobbyCode = _tempTargetLobbyCode + ".";
                    connectButton.ButtonText = _tempTargetLobbyCode;
                }
                if (_tempTargetLobbyCode.Length == maxDigits)
                {
                    connectButton.ButtonText = _tempTargetLobbyCode;
                    lobbyNumpad.Disabled = true;
                }
            };
        }

        public void EditUwcWindowTextures(Scene sceneInstance, Dictionary<string, object> propertyChanges, bool isField)
        {
            if (!sceneInstance.IsValid()) return;
            GameObject[] gObjs = sceneInstance.GetRootGameObjects();

            foreach (var item in gObjs)
            {
                FindAndEditUwcWindowTexture(item, propertyChanges, isField);
            }
        }

        void FindAndEditUwcWindowTexture(GameObject go, Dictionary<string, object> propertyChanges, bool isField)
        {
            uWindowCapture.UwcWindowTexture[] comps = go.GetComponentsInChildren<uWindowCapture.UwcWindowTexture>();
            if(isField)
            {
                foreach (uWindowCapture.UwcWindowTexture comp in comps)
                {
                    EditComponentField(comp, propertyChanges);
                }
            }
            else
            {
                foreach (uWindowCapture.UwcWindowTexture comp in comps)
                {
                    EditComponent(comp, propertyChanges);
                }
            }
        }

        void EditComponentField(uWindowCapture.UwcWindowTexture component, Dictionary<string, object> propertyChanges)
        {
            foreach (var pair in propertyChanges)
            {
                FieldInfo field = typeof(uWindowCapture.UwcWindowTexture).GetField(pair.Key);

                if (field != null)
                {
                    try { field.SetValue(component, pair.Value); }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"Error setting private property '{pair.Key}': {ex.Message}");
                    }
                }
            }
        }

        void EditComponent(uWindowCapture.UwcWindowTexture component, Dictionary<string, object> propertyChanges)
        {
            foreach (var pair in propertyChanges)
            {
                PropertyInfo property = typeof(uWindowCapture.UwcWindowTexture).GetProperty(pair.Key);

                if (property != null && property.CanWrite)
                {
                    try { property.SetValue(component, pair.Value); }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"Error setting private property '{pair.Key}': {ex.Message}");
                    }
                }
            }
        }

        public override void OnInitializeMelon()
        {
            UnpackResources();
            //AudioHelper audioHelper = new AudioHelper();
            //audioHelper.SetAudioDeviceAlt(audioHelper.GetCurrentProcessId(), "ROOT\\MEDIA\\0000"); //VBAudioVACWDM 

            //Our CCK Prop contains a custom MonoBehavior script component. We force-allow it here.
            var propWhitelist = SharedFilter._spawnableWhitelist;
            propWhitelist.Add(typeof(uWindowCapture.UwcWindowTexture));

            _sunshine = new Sunshine();
            _moonlight = new Moonlight();
            _moonlight.ClearInitFile();
            MakeUI();
        }
    }
}