CVR-PlayTogether
===================

**CVR-PlayTogether** is a [ChilloutVR](https://documentation.abinteractive.net/chilloutvr/) Mod that aims to reproduce a Couch Gaming / Lan Party experience in VR.

It does so by Hosting/Streaming one's game (and inputs) with friends of your choosing and displaying that on a shared "physical" Screen spawned in-game.

It depends on ChilloutVR's prop system and needs a custom [CCK-PlayTogether](https://github.com/Searaphim/CCK-PlayTogether) prop to function.

It uses [MelonLoader](https://melonwiki.xyz/#/) as it's injection method.

Requirements
===================

- Windows 10 or above
- [ChilloutVR](https://store.steampowered.com/app/661130/ChilloutVR/) with [MelonLoader](https://melonwiki.xyz/#/?id=requirements) properly set up
- [BTKUILib](https://github.com/BTK-Development/BTKUILib) (You can install from [CVR Melon Assistant](https://github.com/knah/CVRMelonAssistant))
- A [CCK-PlayTogether](https://github.com/Searaphim/CCK-PlayTogether) prop
- Friends (you can still play solo if you want)

Installation
----------
TODO

How to use
----------
TODO

To build CVR-PlayTogether (for devs)
===================

Prerequesites
-------------------------
- Visual Studio 2022 with .NET Standard 2.1 framework

Steps
-----------

- Clone the repo with git (ex: git clone https://github.com/Searaphim/CVR-PlayTogether.git)
- From inside the cloned repo; get the submodules (ex: git submodule update --init --recursive)
- Build NStrip (it's inside your CVR-PlayTogether folder)
- We need to fetch and force public ChilloutVR libraries we depend on. Run the follow command from inside the same directory as the new NStrip executable (replace fields as needed):

		.\NStrip.exe -p -n "<Path to your ChilloutVR folder>\ChilloutVR_Data\Managed" "<Path to your CVR-PlayTogether folder>\.ManagedLibs"
		
- build PlayTogetherLib (which we need for both CVR-PlayTogether and CCK-PlayTogether)
- Open the VS project in PlayTogetherMain and install ILMerge in the VS editor via 
`Tools->NuGet Package Manager->Package Manager Console`:

		NuGet\Install-Package ilmerge -Version 3.0.41

- get the latest moonlight x64 portable zip from 'https://github.com/Searaphim/moonlight-qt/releases' and insert in 'PlayTogetherMain/resources' folder
- get the latest sunshine portable zip from 'https://github.com/Searaphim/Sunshine/releases' and insert in 'PlayTogetherMain/resources' folder 
- For the previous 2 instructions, you can build yourself as long as files are formatted and placed in the exact same way.
- copy and paste all the DLLs from `'<ChilloutVR path>/MelonLoader/net35/'` into `'PlayTogetherMain/libs'`
- get latest BTKUILib.dll from 'https://github.com/Searaphim/BTKUILib/releases' and paste it into `'PlayTogetherMain/libs'` (or build it yourself and do the same)
- delete everything in BTKUILib folder once done (to avoid compilation issues)
- If you build your own CVR-PlayTogether dll you also need to do the same for the prop and use your own DLL otherwise the mod won't work due to signature differences.
		See the [instructions](https://github.com/Searaphim/CCK-PlayTogether)

- Copy/Paste (from the CCK-PlayTogether project to the CVR-PlayTogether project)
  `CCK-PlayTogether\Assets\uWindowCapture\Plugins\x86_64\uWindowCapture.dll`
	  into `PlayTogetherMain\resources` 
- build the PlayTogetherMain project with Visual Studio
- Open a developer command prompt: `Tools->Command Line->Developer Command Line`
- Enter the following in it: 

		msbuild /t:ILMerge 

- You can find the resulting DLL in `'PlayTogetherMain/ILMergeOut'`. That is the mod to be loaded in the ChilloutVR client via MelonLoader. (Put it in `<ChilloutVR path>/Mods`)

