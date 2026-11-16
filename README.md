# OTH-NoClip

A single-player Over the Hill mod that lets you fly your vehicle through obstacles. F8 toggles flight and levels the vehicle.

Press F8 to enable flight and F8 again to return to normal driving. Flight automatically prevents the local vehicle's network pose updates from overriding movement.

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

You can build the DLL yourself on Windows after downloading the source ZIP from GitHub and extracting it. The source does not include the game-generated assemblies or BepInEx dependencies.

The plugin code is in `src/`, its project file is in the repository root, and the independent controller input checks are in `tests/`.

1. Install the **.NET 10 SDK** and the **.NET 6 runtime**. In PowerShell, run `dotnet --list-sdks` and `dotnet --list-runtimes` to check that both are installed. Note the directory of the installed `Microsoft.NETCore.App` 6.0.x runtime.
2. Install BepInEx 6 IL2CPP for Windows x64 in the game folder, launch the game once, and close it. This generates the `BepInEx\interop` folder. The development setup used BepInEx build 788.
3. Create a separate support folder with the following layout. Copy `interop` and `core` from the game's `BepInEx` folder; keep their DLLs intact:

   ```text
   support\
   ├── interop\                  (copy of BepInEx\interop)
   └── BepInEx-Setup\
       └── BepInEx\
           └── core\             (copy of BepInEx\core)
   ```

4. Open PowerShell in the extracted source folder containing `OTH-NoClip.csproj`. Set the following variables to your own support folder and installed .NET 6 runtime directory, then build:

```powershell
$supportDir = 'path to your support folder'
$runtimeDir = 'path to your Microsoft.NETCore.App 6.0.x folder'
dotnet build "-p:SupportDir=$supportDir" "-p:DotNet6RuntimeDir=$runtimeDir"
```

5. After a successful build, find `OTH-NoClip.dll` in `bin\Debug\net10.0`. Close the game, copy the DLL into the game's `BepInEx\plugins` folder, and launch the game.

For the runtime path, `dotnet --list-runtimes` displays the parent directory in brackets; append the listed 6.0.x version to that directory. `SupportDir` defaults to a `support` folder beside the source folder if omitted. The build may report reference warnings from native DLLs included by the project's wildcard references; check that it finishes with zero errors. If the mod fails to load or move the vehicle, check `BepInEx\LogOutput.log` in the game folder.

Optionally pass `-p:DevelopmentPluginDir=<your staging directory>` to copy the built DLL there after a successful build. Close the game first if that directory is linked into its plugin loader.

## Tests

Run `dotnet run --project tests/ControllerInput.Tests.csproj` with the .NET 10 SDK. This dependency-free test runner checks the production controller stick and trigger scaling without requiring the game or BepInEx. A failure returns a nonzero exit code.

GitHub Actions runs the same checks on pushes and pull requests as the `Unit tests` job. It reports line and branch coverage for `ControllerInput.cs` in the job summary; this is not coverage of the whole plugin. Coverage uses the free Coverlet tool and has no percentage threshold. The plugin build requires game-generated dependencies and is not included in this automated check.

These checks do not exercise XInput, Unity, Fusion, flight activation, collision restoration, or gameplay. Those require integration or in-game testing; continue to test activation while moving, controller input, and safe toggle-off in a single-player session.
