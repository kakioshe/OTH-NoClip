# oth-trainer

A Windows single-player Over the Hill mod for flying your vehicle and teleporting to a map pin. Version 0.6 requires MelonLoader. Back up your save before using the mod.

## Installation

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) for the game. Development uses MelonLoader 0.7.3 x64. Launch the game once and close it to generate the required assemblies.
2. Copy `oth-trainer.dll` to the game's `Mods` folder. Remove older copies of the trainer so only one is loaded.
3. Launch the game and enter a single-player session with a vehicle.

### Upgrading from BepInEx (v0.5 and earlier)

Close the game and back up your saves and existing loader files. Disable the previous BepInEx installation before installing MelonLoader: move its `BepInEx` folder, `winhttp.dll`, `doorstop_config.ini`, and `.doorstop_version` outside the game folder, if present and belonging to that installation. Keep the backup until the new setup works. Do not run both loaders or retain an older trainer in an active plugin folder. The v0.6 ZIP contains a `Mods` folder; extract it into the game folder after installing MelonLoader. The loader itself is not included.

To uninstall, close the game and remove `Mods/oth-trainer.dll`. Check `MelonLoader/Latest.log` for startup and diagnostic messages.

## Controls

Keep the game focused. Disable flight in a clear area above solid ground; the vehicle may fall when gravity returns.

| Action | Keyboard | Xbox controller |
| --- | --- | --- |
| Toggle flight | F8 | — |
| Move forward/back | W / S | Left stick up/down |
| Move sideways | A / D | Left stick left/right |
| Turn | — | Right stick left/right |
| Rise / descend | Space / Ctrl | Right trigger / left trigger |
| Boost | Shift | Left bumper |
| Request teleport to map pin | F9 | — |

Place a pin on the map with right-click and disable flight before pressing F9. Choose clear ground and check your surroundings after arrival. The first connected XInput controller is used; sticks and triggers are proportional after a dead zone.

Flight and map-pin teleport were reported working in single-player with MelonLoader 0.7.3, Unity 6000.0.75f1 and game version 0.0.1. The startup log confirms trainer loading, pose patch installation, flight toggles and teleport requests. Controller, focus changes, respawn and every teleport refusal case have not been individually verified. This report used the development assembly labelled 0.5.0; the v0.6.0 candidate changes only the version label and documentation.

## Building from source

Install the .NET 10 SDK and .NET 6 runtime. Install MelonLoader, start the game once, and close it so `MelonLoader/Il2CppAssemblies` is generated. The game assemblies and loader are not included in this repository.

From the source folder, set paths for your machine and build:

```powershell
$gameDir = 'path to your game folder'
$runtimeDir = 'path to your Microsoft.NETCore.App 6.0.x folder'
dotnet build "-p:GameDir=$gameDir" "-p:DotNet6RuntimeDir=$runtimeDir"
```

Use `dotnet --list-runtimes` to find the .NET 6 runtime directory; append the version to the parent path shown in brackets. Alternatively set `MelonLoaderDir` to a copied `MelonLoader` folder containing `net6` and `Il2CppAssemblies`. The build may warn about native files included by wildcard references; check for zero errors. The output is `bin/Debug/net10.0/oth-trainer.dll`. Optionally set `DevelopmentPluginDir` to stage the output there after a successful build; close the game first.

## Tests

Run `dotnet run --project tests/ControllerInput.Tests.csproj` with the .NET 10 SDK. These 13 checks cover controller stick and trigger scaling. GitHub Actions runs them as `Unit tests` and reports scoped coverage of `ControllerInput.cs`. The checks do not cover Unity, Fusion, loader integration, flight or teleportation; those require in-game testing.
