# Port's Trainer

A Windows single-player trainer for **over the hill**, built for MelonLoader. Version **0.8.1** adds experimental High-range overdrive alongside repair, flip, vehicle tuning and presets, God mode, Water immunity and Spider mode. It includes the earlier menu, persistent keyboard/Xbox bindings, camera-relative flight and map-pin teleport.

## Install

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) x64 for the game. This project uses version 0.7.3. Launch the game once, then close it.
2. Download the trainer ZIP from [Releases](https://github.com/kakioshe/oth-trainer/releases) and extract it into the game folder. The DLL should be at `Mods/oth-trainer.dll`.
3. Launch the game and enter a single-player session. Press **Insert** to open Port's Trainer.

Close the game before installing or updating. Keep only one trainer DLL in `Mods`. The package includes the trainer and instructions; install MelonLoader separately. Back up your saves before using the trainer.

To uninstall, close the game and remove `Mods/oth-trainer.dll`. Optional saved bindings are at `UserData/PortsTrainer.bindings.cfg`. Startup and diagnostic messages appear in `MelonLoader/Latest.log`.

Vehicle slider preferences are saved separately in `UserData/PortsTrainer.vehicle.cfg`. Performance modifiers, God mode, Water immunity and Spider mode always start off; activation is not saved.

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

Press Insert and open **Movement**, then click a keyboard or controller field to rebind it. The Movement tab groups flight, map-pin teleport and their bindings. Release held controls before entering the new binding. Keyboard shortcuts support Ctrl, Shift and Alt with a main key, such as **Shift + Q**; modifiers cannot be assigned alone.

- Click **×** to clear a binding. Each active shortcut or controller input belongs to one action.
- Press **Escape** to cancel capture or close the menu. **Restore defaults** resets both columns.
- Insert is fixed and cannot be cleared or rebound. F10 and controller buttons do not open the menu.
- Changes save automatically to `UserData/PortsTrainer.bindings.cfg`. Existing custom action bindings are retained; older menu bindings become Insert, and modifier-only bindings are cleared.

A configured combination takes precedence over the same plain movement key. Action shortcuts require exact modifiers; plain movement remains usable while other modifiers are held. Escape and F10 are unavailable as trainer bindings. System and overlay shortcuts may intercept some combinations.

Opening the menu holds active flight in place and blocks gameplay input until the menu closes and held controls release. **The world keeps running.** Losing focus closes the menu and restores the cursor; release held controls before resuming trainer actions.

Outside the menu, the trainer reserves only controls needed by available actions. Unused map and game controls remain available during flight. Controller reservations can cover a whole stick or axis when a trainer action uses one direction. Temporary game binding overrides are restored without saving game settings.

## Vehicle controls

The Vehicle tab includes **Repair car**. Click **Repair** to request a full repair through the game's healing operation without consuming an item. The command requires the same initialized local single-player car with state authority when clicked and when executed. Closing the menu before execution cancels it. Hover over **i** for help or click to pin it. The result appears beneath the controls. Repair does not enable damage protection or change performance modifiers. Repair and the existing vehicle settings were reported working in development testing. Visual effects, all damage paths and transitions have not been exhaustively tested.

**Flip car** requests an upright reposition through the vehicle and network physics teleport operations, retaining horizontal heading, lifting the car 0.75 metres and stopping its motion. Requires flight off and the same initialized, authoritative local car at click and execution. Use open space; terrain or overhead obstacles can obstruct recovery. The command logs body, transform and physics pose before, immediately after, and at delayed readbacks. Successful flips leave no confirmation text on screen; failures still show feedback. The revised physics teleport path was reported working during development testing; this does not establish recovery on every terrain or vehicle.

**God mode** prevents new damage through the current local car's damage multiplier and damage handler. It leaves existing damage in place and restores the previous multiplier when disabled. Flooding and other environmental failures are outside this protection.

**Vehicle modifiers** groups five sliders under an **Active / Inactive** checkbox. Each number is editable: press Enter or click outside the field to apply it. Values must fall within the listed range; invalid entries restore the previous value. Typed values retain their precision, while sliders move in 0.05 steps. Preferences can be adjusted while inactive. The modifiers apply relative to the current car's legitimate baseline:

| Setting | Range | Meaning |
| --- | --- | --- |
| Power | 0.5–10x | Engine torque and existing torque/power cap values |
| Speed limit | 0.5–10x | Existing engine speed-limit setting; preserves whether the limiter is enabled |
| Grip | 0.5–2x | Per-wheel tire-friction multiplier |
| Steering angle | 0.5–1.5x | Maximum wheel steering angle |
| Braking strength | 0.5–2x | Service-brake torque |

Unchecking Active restores owned baseline values while keeping slider preferences. **Restore stock** unchecks Active, resets the five preferences to 1x and immediately releases High-range overdrive. Both actions leave God mode and repairs independent. Performance modifiers pause during flight and resume afterward. Losing the car/session switches both controls off; enable them explicitly for the next car. Shared asset settings are refused, and unknown external writes stop the controls rather than overwriting those values.

The existing vehicle settings were reported working in development testing. Protection coverage, upgrade rebasing and cleanup across all car/session transitions still require further runtime validation. A higher speed-limit multiplier does not guarantee that the engine/gearing can reach it or enforce a hard downhill cap. This report does not establish every grip/terrain interaction.

**Tuning presets** replace the five saved values, retaining current activation. If modifiers are active, the new tuning applies immediately. Presets never enable any switch. Stock uses all 1x; Trail uses 2/1.5/1.5/1.1/1.5; Climb uses 3/1/2/1/2; Fast uses 5/5/1.5/0.75/2, in power/speed/grip/steering/brakes order. The Climb preset is tuning only; **Spider mode** has its own switch. Power and speed values up to 10x are experimental and need in-game validation.

**Water immunity** is a separate experimental switch. It prevents new engine flooding and suppresses the local wheels' terrain drag/deceleration force while the car touches water. Tire grip, service brakes, water detection and normal drying stay active. This does not provide buoyancy, dry an already flooded engine or promise protection from every environmental effect. It also removes underlying terrain resistance while touching water; other causes of slow movement may remain. Disabling restores normal resistance on the next simulation substep and resumes normal flooding. Accumulated immersion progress is not restored. Water resistance interception was observed for all four local wheels, and the combined driving/water revision was reported working during development testing.

**Spider mode** replaces the earlier Climb assist prototype. It supplies a surface-relative motor, inward adhesion, gravity compensation and alignment torque for slopes, walls and ceilings. Use the game's normal keyboard/controller throttle, reverse and steering controls. The motor targets 36 km/h forward and 18 km/h reverse, independently of tuning sliders; these are motor targets, not hard speed caps. Release throttle or engage the handbrake to hold against the surface. The game's drivetrain still runs, so begin with performance modifiers off. The Climb preset remains ordinary tuning and does not enable this switch.

Spider mode uses wheel contacts plus short probes below and in the travel direction to attach and pitch onto nearby surfaces. It excludes trigger/water volumes and vehicle colliders. It releases when no surface is found, pauses during flight and refuses overturned roof contact. Sharp corners, narrow ledges, terrain collision geometry and approach speed can interrupt attachment. The combined revision was reported working during development testing. Every wall/ceiling transition and controller/manual reverse path has not been exhaustively verified. No persistent physics settings or shared assets are changed.

Both assist switches start off, reset on vehicle/session loss and remain independent of God mode, performance modifiers and Restore stock. The independent motor policies and lifecycle have automated checks; those checks do not establish game physics or native-hook interception.

**High-range overdrive** is a separate experimental control, with **1x (stock), 1.5x, 2x and 3x** buttons. Stop the car before selecting a value. It extends forward High-range gearing while retaining Low, reverse, the existing gears and normal engine RPM limits. For example, 2x divides the effective final reduction by two, allowing approximately twice the road speed per engine revolution while halving wheel torque for a given engine torque. This does not guarantee twice the attainable speed. High first gear also becomes taller; use Low for starting or climbing.

Overdrive starts at 1x, is not saved and is independent of performance modifiers, Power, Speed limit and tuning presets. It releases during flight, disabled input, neutral/reverse or shifting, and resets on vehicle/session loss or a fault. **Restore stock** releases it immediately even while moving. Unknown differential writes are preserved and stop overdrive. No shared High/Low settings or gearbox arrays are modified.

For a first comparison, leave Spider mode off and use the same Power and Speed limit values on the same level road. Select 1x while stopped and record High/1st and High/2nd RPM at a repeatable road speed. Stop and repeat at 1.5x, then 2x. Compare RPM at the same speed rather than assuming a top-speed gain. Check Low, reverse, manual shifts and restoration separately. Each selection records up to 90 input-hook samples, at most one per second, in `MelonLoader/Latest.log`; logs include range, requested/engaged gear, RPM, road speed, wheel angular velocity/slip and effective reduction. Native interception, physical effects and transition behavior remain runtime-unverified for 0.8.1.

## Compatibility and testing

Development uses MelonLoader 0.7.3 x64, Unity 6000.0.75f1 and game version 0.0.1. The trainer is restricted to the local vehicle in single-player. Compatibility with later game or loader versions is not established.

The compact menu received user approval during development; earlier flight and map-pin teleport were user tested. Camera-relative flight, all controller combinations, focus/session transitions and game-input override restoration have not received exhaustive runtime testing. Input suppression attempts an early update hook and logs a warning if it must fall back to per-frame updates; first-frame conflicts may then occur.

The user reported repair/settings, revised flip and the combined driving/water revision working successfully on development builds. The final name/version changes and standard release build have not been separately gameplay-tested. Broader protection, upgrade/session transitions and extreme tuning remain incompletely tested.

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

Vehicle-development checks cover preference parsing/storage, scalar ownership and group rollback, protection ownership, repair/flip commands, numeric input validation and control lifecycles. Overdrive checks cover inverse ratios, range/gear gating, stopped selection, known-writer rebasing, non-compounding updates, external-write refusal, restoration and fault cleanup. Scoped coverage includes these game-independent classes, and excludes game adapters, hooks and menus. Coverage does not establish gameplay correctness.

The loader entry point delegates to `TrainerRuntime`, which wires dependencies and orders frame updates. Flight and map-pin teleport are separate features using `LocalVehicleProvider` for single-player eligibility and vehicle identity. Input polling and menu presentation stay outside those features. `TrainerBindings` owns validation and serialization; `BindingStore` owns file access. Dependencies receive their logger explicitly, and `TrainerLifetime` releases resources in reverse acquisition order, including after partial initialization. Harmony callbacks use a small static bridge to their current owner; the owner removes its hooks during disposal.

Source folders group runtime, features, game integration, input, configuration, UI and diagnostics. See [Source organization](docs/architecture.md) for ownership boundaries and guidance on adding vehicle features.

Submit changes through a pull request with the behavior change and verification described. Main requires a pull request and a successful **Unit tests** check. Keep generated assemblies, binaries, saves and local tooling out of source control.
