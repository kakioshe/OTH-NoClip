# Port's Trainer

A Windows single-player trainer for **over the hill**, built for MelonLoader. Version **0.6.1** reorganizes the source and separates runtime responsibilities, retaining the compact controls menu, persistent keyboard and Xbox bindings, and camera-relative flight.

## Install

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) x64 for the game. This project uses version 0.7.3. Launch the game once, then close it.
2. Download the trainer ZIP from [Releases](https://github.com/kakioshe/oth-trainer/releases) and extract it into the game folder. The DLL should be at `Mods/oth-trainer.dll`.
3. Launch the game and enter a single-player session. Press **Insert** to open Port's Trainer.

Close the game before installing or updating. Keep only one trainer DLL in `Mods`. The package includes the trainer and instructions; install MelonLoader separately. Back up your saves before using the trainer.

To uninstall, close the game and remove `Mods/oth-trainer.dll`. Optional saved bindings are at `UserData/PortsTrainer.bindings.cfg`. Startup and diagnostic messages appear in `MelonLoader/Latest.log`.

## Default controls

| Action | Keyboard | Xbox controller |
| --- | --- | --- |
| Open / close menu | Insert (fixed) | - |
| Toggle flight | F8 | - |
| Forward / back | W / S | Left stick up / down |
| Strafe left / right | A / D | Left stick left / right |
| Ascend / descend | Up / Down arrow | Right / left trigger |
| Boost | B | Left bumper |
| Teleport to map pin | F9 | - |

Flight follows the camera's **horizontal direction**, and the car faces that direction while staying level. Use ascend and descend to change height. The right stick remains available for the game camera unless assigned to another trainer action. If the camera is unavailable or points straight up or down, flight keeps its previous heading.

Disable flight above clear, solid ground; the vehicle may fall when gravity returns. To teleport, place a map pin with right-click, disable flight, then press F9. Teleport requires a spawned local vehicle in single-player.

The first connected XInput controller is used. Sticks and triggers support proportional movement with dead zones.

## Customize controls

Press Insert, then click a keyboard or controller field to rebind it. Release held controls before entering the new binding. Keyboard shortcuts support Ctrl, Shift and Alt with a main key, such as **Shift + Q**; modifiers cannot be assigned alone.

- Click **×** to clear a binding. Each active shortcut or controller input belongs to one action.
- Press **Escape** to cancel capture or close the menu. **Restore defaults** resets both columns.
- Insert is fixed and cannot be cleared or rebound. F10 and controller buttons do not open the menu.
- Changes save automatically to `UserData/PortsTrainer.bindings.cfg`. Existing custom action bindings are retained; older menu bindings become Insert, and modifier-only bindings are cleared.

A configured combination takes precedence over the same plain movement key. Action shortcuts require exact modifiers; plain movement remains usable while other modifiers are held. Escape and F10 are unavailable as trainer bindings. System and overlay shortcuts may intercept some combinations.

Opening the menu holds active flight in place and blocks gameplay input until the menu closes and held controls release. **The world keeps running.** Losing focus closes the menu and restores the cursor; release held controls before resuming trainer actions.

Outside the menu, the trainer reserves only controls needed by available actions. Unused map and game controls remain available during flight. Controller reservations can cover a whole stick or axis when a trainer action uses one direction. Temporary game binding overrides are restored without saving game settings.

## Compatibility and limits

Development uses MelonLoader 0.7.3 x64, Unity 6000.0.75f1 and game version 0.0.1. The trainer is restricted to the local vehicle in single-player. Compatibility with later game or loader versions is not established.

The compact menu received user approval during development; earlier flight and map-pin teleport were user tested. Camera-relative flight, all controller combinations, focus/session transitions and game-input override restoration have not received exhaustive runtime testing. Input suppression attempts an early update hook and logs a warning if it must fall back to per-frame updates; first-frame conflicts may then occur.

## Build

Install the **.NET 10 SDK** and **.NET 6 runtime**. Initialize MelonLoader by launching the game once so `MelonLoader/Il2CppAssemblies` exists, then close the game. Loader and game assemblies are not included in this repository.

From the repository root:

```powershell
$gameDir = 'path to your game folder'
$runtimeDir = 'path to Microsoft.NETCore.App/6.0.x'
dotnet build --configuration Release "-p:GameDir=$gameDir" "-p:DotNet6RuntimeDir=$runtimeDir"
```

Use `dotnet --list-runtimes` to find the runtime directory; append the displayed 6.0.x version to its parent path. You can set `MelonLoaderDir` instead of `GameDir` when building against a separate initialized loader folder containing `net6` and `Il2CppAssemblies`.

Output: `bin/Release/net10.0/oth-trainer.dll`. Optional `DevelopmentPluginDir` copies a successful build into a staging folder; close the game before replacing an installed DLL. Wildcard assembly references may warn about native files; a successful build must finish with zero errors.

## Tests and contributions

```powershell
dotnet run --project tests/ControllerInput.Tests.csproj --configuration Release
```

The checks cover controller scaling, binding conflicts and persistence, legacy configuration migration, fixed menu controls, camera-heading normalization and fallback, input-block lifecycle, selective control consumption, and resource cleanup after partial initialization or failures. They run without game assemblies. The **Unit tests** GitHub Actions job also reports scoped line and branch coverage for the independent controller, binding, storage, heading, input-policy and resource-lifetime classes. Coverage does not measure the complete plugin or establish runtime integration behavior.

The loader entry point delegates to `TrainerRuntime`, which wires dependencies and orders frame updates. Flight and map-pin teleport are separate features using `LocalVehicleProvider` for single-player eligibility and vehicle identity. Input polling and menu presentation stay outside those features. `TrainerBindings` owns validation and serialization; `BindingStore` owns file access. Dependencies receive their logger explicitly, and `TrainerLifetime` releases resources in reverse acquisition order, including after partial initialization. Harmony callbacks use a small static bridge to their current owner; the owner removes its hooks during disposal.

Source folders group runtime, features, game integration, input, configuration, UI and diagnostics. See [Source organization](docs/architecture.md) for ownership boundaries and guidance on adding vehicle features.

Submit changes through a pull request with the behavior change and verification described. Main requires a pull request and a successful **Unit tests** check. Keep generated assemblies, binaries, saves and local tooling out of source control.
