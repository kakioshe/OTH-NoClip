# oth-trainer

A Windows single-player Over the Hill mod for flying your vehicle and requesting a teleport to a map pin. This development version requires MelonLoader and needs in-game testing. Back up your save before testing.

## Installation

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) for the game. Development uses MelonLoader 0.7.3 x64. Launch the game once and close it to generate the required assemblies.
2. Copy `oth-trainer.dll` to the game's `Mods` folder. Remove older copies of the trainer so only one is loaded.
3. Launch the game and enter a single-player session with a vehicle.

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

Place a pin on the map with right-click and disable flight before pressing F9. Teleportation is experimental and has not been verified in-game. The first connected XInput controller is used; sticks and triggers are proportional after a dead zone.

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
