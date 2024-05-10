
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
using System.Net.Http;
using System.Net;
using System.Text;
using static ABI_RC.Systems.Safety.BundleVerifier.RestrictedProcessRunner.Interop.InteropMethods;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

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
        private Process normalprocess;
        private string _url = "https://localhost:47990/api/pin"; //Security could be further increased by changing LAN restriction to localhost only.
        private string _usr = "defaultusr";
        private string _pwd = "defaultpwd";

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
            string[] files = Directory.GetFiles(APPSCONF_DIR);
            foreach (string file in files)
            {
                File.Delete(file);
            }
        }

        public void Run(string appPath)
        {
            FlushConfigs();
            CreateUser();
            string appstr = @"{""name"":""" + Path.GetFileNameWithoutExtension(appPath) + @""",""cmd"":""" + Regex.Replace(appPath, @"\\|/", @"\\") + @""",""auto-detach"":""true"",""wait-all"":""true"",""image-path"":""steam.png""}";
            File.WriteAllText(APPSDEFCONF_PATH, @"{""env"":{},""apps"":[" + appstr + @"]}"); //Temporary lazy fix for json issues
            normalprocess = Process.Start(normalstartinfo);
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
        private const string EXE_PATH = SharedVars.RESOURCE_FOLDER + @"\Moonlight\Moonlight.exe";
        private const string INI_PATH = SharedVars.RESOURCE_FOLDER + @"\Moonlight\Moonlight Game Streaming Project\Moonlight.ini";
        private Process? _sessionProc = null;
        private Process? _pairprocess = null;
        private string _lobbyCode = "";
        private string _lobbyDestination = "";

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
        private const string MOONLIGHT_RESOURCE = "CVRPlayTogether.resources.MoonlightPortable-x64-5.0.1.zip";
        private const string SUNSHINE_RESOURCE = "CVRPlayTogether.resources.sunshine-windows-portable.zip";
        private const string UWINDOWCAPTURE_RESOURCE = "CVRPlayTogether.resources.uWindowCapture.dll";
        private Sunshine _sunshine;
        private Moonlight _moonlight;
        private string _pinInputs = "";
        private string _tempTargetLobbyCode = "";

        private void UnpackResources()
        {
            ZipArchive zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(MOONLIGHT_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER + @"\Moonlight", true);
            zip.Dispose();
            zip = new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream(SUNSHINE_RESOURCE));
            zip.ExtractToDirectory(SharedVars.RESOURCE_FOLDER, true);
            byte[] dllBytes = null;
            using (Stream stm = Assembly.GetExecutingAssembly().GetManifestResourceStream(UWINDOWCAPTURE_RESOURCE))
            {
                dllBytes = new byte[(int)stm.Length];
                stm.Read(dllBytes, 0, (int)stm.Length);
            }
            File.WriteAllBytes(SharedVars.UWINDOWCAPTURE_DLL_PATH, dllBytes);
            NativeLibrary.LoadLib(SharedVars.UWINDOWCAPTURE_DLL_PATH);
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
                    QuickMenuAPI.ShowNotice("Pairing Result", "Pairing Success!", actionContinue);
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
                pinButtons.Add(pinNumpad.AddButton(buttonNumber.ToString(), "dummy.png", ""));
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

            var pairPage = clientCat.AddPage("Connect to Lobby", "", "Pair yourself with a Host", "CVRPlayTogether");
            var joinToggle = clientCat.AddToggle("Join Game", "Attempt to join game", false);
            joinToggle.OnValueUpdated += b =>
            {
                if(b == true)
                {
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
                    $"Please only press OK after Pairing succeeded or failed. The host will tell you -> They must enter this pin to approve your connection: {pairingPin}",
                    () => { _moonlight.StopPairing(); });
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
                    if (_tempTargetLobbyCode.Length < 10)
                    {
                        _tempTargetLobbyCode = _tempTargetLobbyCode + buttonNumber.ToString();
                        connectButton.ButtonText = _tempTargetLobbyCode;
                    }
                    if (_tempTargetLobbyCode.Length == 10)
                    {
                        connectButton.ButtonText = _tempTargetLobbyCode;
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

            _sunshine = new Sunshine();
            _moonlight = new Moonlight();
            _moonlight.ClearInitFile();
            MakeUI();
        }
    }
}