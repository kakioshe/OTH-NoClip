# Source organization

The trainer builds as one assembly. Folders separate responsibilities while keeping build and deployment simple. Add another project only when a component needs an independently enforced dependency boundary or reuse outside the trainer.

```text
src/
  Runtime/             Loader entry point, composition, frame order and cleanup
  Features/
    Flight/            Flight behavior, controls and heading policy
    Teleport/          Map-pin teleport behavior
    Vehicle/           Recovery, tuning, protection, assists and ownership policies
  Game/                Game-object access and scoped game hooks
  Input/               Device polling, action consumption and input-block policy
  Configuration/       Binding/tuning validation, presets, serialization and storage
  UI/                  Menu presentation, binding capture and cursor behavior
  Diagnostics/         Logging adapter
tests/                 Game-independent automated checks and coverage reporting
```

## Dependency boundaries

- `Runtime/Plugin.cs` adapts MelonLoader callbacks. `TrainerRuntime` constructs collaborators and defines update order; it should delegate feature behavior.
- Each feature owns its behavior and state restoration. Features receive input values or commands; they do not poll devices, render menus or access configuration files.
- `Game/` owns game-object lookup, eligibility and interception. Keep local-vehicle and single-player checks centralized. Hooks must have an explicit owner and be removed during cleanup.
- `Input/` translates devices and bindings into actions. Keep decision rules independent of Unity and MelonLoader wherever possible so tests can run without the game.
- `Configuration/` separates validation and serialization from file access. `UI/` presents state and requests actions; it does not tune vehicle physics directly.
- `TrainerMenu` owns the shared window, Movement tab, cursor and binding capture. The runtime supplies `VehicleMenu.Draw` for the Vehicle tab inside the same frame. Content receives a scaled drawing rectangle and does not own a second window or navigation. Changing tabs cancels binding capture. `VehicleMenu` presents repair, independent protection/performance switches and five sliders. Private tracing is not exposed as a product control.
- Pass dependencies explicitly. Introduce an interface when interchangeable implementations or a meaningful test boundary need it. Avoid a generic service registry or a shared miscellaneous folder.

Folder names group the existing internal types; this reorganization retains their type names and current visibility. Folders alone do not enforce dependencies. Review new references against these boundaries, and introduce focused namespaces or project boundaries when they solve an actual collision or dependency problem.

## Adding vehicle controls

Performance, recovery, damage protection and assist behavior live under `Features/Vehicle/`. Keep independently testable settings validation, baseline/restore policy and activation decisions there. Put game component access and hooks under `Game/`, and extend the menu with commands and display state rather than embedding mutations in drawing code.

Performance modifiers, one-shot repair and damage protection have distinct lifecycles. Give each a focused owner and explicit cleanup semantics. Applying performance modifiers must not implicitly toggle protection or repair damage. Reuse one verified local-vehicle provider and handle vehicle replacement, session loss and partial initialization. Preserve legitimate game upgrades when establishing restoration baselines.

`VehicleRepair` queues a target captured on click and executes once during the next runtime update while the menu is still visible and focused. It coalesces duplicate clicks, validates damage bounds and reports failure without retrying. Its `IVehicleRepairTarget` boundary permits independent lifecycle/failure tests. `Game/VehicleRepairTarget` resolves through `LocalVehicleProvider`, verifies controller/component identity and state authority before every access, and calls `CarDamageSystem.HealPlayer` for the local player with the game's maximum damage amount and self-healing reason. It does not directly assign damage, consume inventory, reset debuffs or modify tuning. Disposal cancels pending work; there is no repair rollback. Repair was user tested; visual/other healing effects and every damage path have not been exhaustively validated.

`VehiclePreferences` validates and serializes bounded multipliers relative to stock values. It does not serialize activation or damage-protection state. Speed is represented as a ratio to the car's stock engine limit; no physical unit conversion is assumed. The current ranges are development bounds pending handling tests. `VehiclePreferenceStore` persists only preferences through a temporary-file replacement. Runtime saves changed sliders after input release and on disposal, keeping filesystem access out of drawing code.

`VehicleModifierValue` tracks one scalar on one settings object. It derives every output from its captured baseline, avoiding compounded multipliers. An unexpected current value permanently relinquishes ownership; cleanup leaves that value alone. `VehicleValueGroup` applies the independent multipliers, verifies readback and attempts every original value during rollback, even if one object is unavailable. `VehicleProtection` separately owns the observed damage multiplier and restores its actual baseline, including nonunit or zero values. It never reads or changes existing damage.

`VehicleControls` coordinates those lifecycles through `IVehicleControlsTarget`: independent switches, flight suspension, vehicle-loss reset and failure cleanup. `Game/VehicleControlsTarget` checks local eligibility, active block references and isolation from category/engine-tier assets before capturing baselines. Each value validates its own original native settings/wheel identity, including during restoration. `Game/VehicleControlHooks` releases owners before known engine-tier, damage-debuff, damage-modifier and wheel-friction writers; the next update captures their resulting state. It also attempts cleanup before VehicleBase.OnDisable and skips local ApplyDamage only while the original eligible car is protected. Hook installation failure disables these controls while retaining the rest of the trainer. Equality checks cannot detect every writer, and hook registration/compilation do not establish runtime coverage or successful teardown cleanup.

Add automated checks for independent behavior, including invalid values and cleanup/rebasing transitions. Game ownership, damage/repair semantics, hooks and physics effects also require runtime evidence; unit tests do not establish those behaviors. Do not add empty placeholder directories or generalized feature frameworks before the implementation needs them.

## Build and tooling

The .NET SDK includes C# files recursively, so new source subfolders build automatically. The test project deliberately links only game-independent files; update its explicit paths when moving or adding tested code. Coverage selects those files by filename and remains scoped to the linked policies.

See the root README for build and test commands. Keep private diagnostics, generated outputs, game assemblies, saves and workspace archives outside this repository. A source reorganization should pass the existing tests and compile with the initialized loader; validate any separate diagnostic build against the new paths before using it.

This layout applies [Microsoft's separation-of-concerns guidance](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) to a small single-assembly trainer. Recursive compile behavior is documented in the [.NET SDK overview](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview).

Vehicle recovery uses VehicleFlip for queued command/identity eligibility and VehicleFlipTarget for the game reposition and network physics teleport operations. Flip observes the captured car after execution even when the menu closes, stops observing on eligibility loss, and clears requests/readback on disposal. VehicleValueInput validates numeric entries against VehiclePreferences; VehicleMenu retains edit drafts and groups performance settings independently of recovery/protection. Revised flip behavior was user tested; recovery on every terrain and vehicle remains unverified.

Movement and Vehicle share MenuTheme colors and section heading creation. Both tabs use matching section cards, row sizing and footer placement. Flip success clears visible status while retaining diagnostic readback logging.

`VehiclePresets` supplies complete tuning profiles without owning activation. Only their resulting five values are persisted; manual edits remain custom values. Power/speed limits accept up to 10x. `VehicleAssists` separately coordinates flooding and climb switches through `IVehicleAssistsTarget`, clearing activation on target loss/failure and pausing climb during flight. Neither assist is persisted or coupled to Restore stock.

`VehicleAssistHooks` scopes flooding-update/transition prefixes, the vehicle simulation postfix and wheel substep postfix to the captured original local car. `VehicleAssistsTarget` rechecks authority and car identity at each mutation. Flooding prevention resets immersion progress and refuses new flooded transitions while allowing the original update/drying behavior. Existing flooded state is left alone; disabled prevention does not restore historical immersion progress.

Spider mode uses the independent `SurfaceDrive` motor policy. `VehicleAssistsTarget` supplies fresh wheel/raycast contacts and applies force/torque through the current authoritative local body. The adapter reads normal game input and blocks propulsion while the menu/focus gate is active. It owns no persistent physics settings; disabling stops forces. Water resistance suppression runs after wheel substep interpolation, matches the current car's wheel pointers and changes only transient resistance input while touching water. The user reported the combined driving/water revision working and logs captured resistance suppression on four wheels. Every contact transition, input path and hook path still needs separate runtime evidence beyond policy tests.
