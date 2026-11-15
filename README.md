# OTH-NoClip

A single-player Over the Hill mod that lets you fly your vehicle through obstacles. F8 toggles flight and levels the vehicle.

## Prerequisites

- Over the Hill for Windows.
- BepInEx 6 IL2CPP for Windows x64 installed in the game folder. The development setup used build 788. Launch the game once after installing BepInEx to generate its files.
- An Xbox-compatible XInput controller for controller controls (optional).

This is a prototype. The controller controls and activation while moving need further in-game testing. Back up your save before use.

## Installation

1. Close the game.
2. Download `OTH-NoClip.dll` from the latest GitHub release.
3. Copy it into the game's `BepInEx/plugins` folder, replacing an older version if present.
4. Launch the game and enter a single-player session with a vehicle.

To uninstall, close the game and remove `OTH-NoClip.dll` from `BepInEx/plugins`.

## Controls

Keep the game window focused. Disable flight in a clear area above solid ground; the car may fall when gravity returns.

| Action | Keyboard | Xbox controller |
| --- | --- | --- |
| Toggle flight | F8 | — |
| Move forward/back | W / S | Left stick up/down |
| Move sideways | A / D | Left stick left/right |
| Turn | — | Right stick left/right |
| Rise / descend | Space / Ctrl | Right trigger / left trigger |
| Boost | Shift | Left bumper |

The first connected XInput controller is used. Stick and trigger movement is proportional after a small dead zone.

## Building from source

The project uses the .NET 10 SDK and references .NET 6 runtime assemblies, BepInEx core assemblies and game-generated interop assemblies. Dependencies and build outputs are not included in this repository. Set `SupportDir` to a folder containing `interop` and `BepInEx-Setup`, and `DotNet6RuntimeDir` to the .NET 6 runtime directory, then run:

```powershell
dotnet build -p:SupportDir="C:\path\to\support" -p:DotNet6RuntimeDir="C:\path\to\Microsoft.NETCore.App\6.0.x"
```

The DLL is generated in `bin/Debug/net10.0`. If the mod fails to load or move the vehicle, check `BepInEx/LogOutput.log` in the game folder.

