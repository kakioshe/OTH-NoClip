# Source organization

The trainer builds as one assembly. Folders separate responsibilities while keeping build and deployment simple. Add another project only when a component needs an independently enforced dependency boundary or reuse outside the trainer.

```text
src/
  Runtime/             Loader entry point, composition, frame order and cleanup
  Features/
    Flight/            Flight behavior, controls and heading policy
    Teleport/          Map-pin teleport behavior
  Game/                Game-object access and scoped game hooks
  Input/               Device polling, action consumption and input-block policy
  Configuration/       Binding validation, serialization and file storage
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
- Pass dependencies explicitly. Introduce an interface when interchangeable implementations or a meaningful test boundary need it. Avoid a generic service registry or a shared miscellaneous folder.

Folder names group the existing internal types; this reorganization retains their type names and current visibility. Folders alone do not enforce dependencies. Review new references against these boundaries, and introduce focused namespaces or project boundaries when they solve an actual collision or dependency problem.

## Adding vehicle controls

Place the planned performance, repair and damage-protection behavior under `Features/Vehicle/` when implemented. Keep independently testable settings validation, baseline/restore policy and activation decisions there. Put game component access and hooks under `Game/`, and extend the menu with commands and display state rather than embedding mutations in drawing code.

Performance modifiers, one-shot repair and damage protection have distinct lifecycles. Give each a focused owner and explicit cleanup semantics. Applying performance modifiers must not implicitly toggle protection or repair damage. Reuse one verified local-vehicle provider and handle vehicle replacement, session loss and partial initialization. Preserve legitimate game upgrades when establishing restoration baselines.

Add automated checks for independent behavior, including invalid values and cleanup/rebasing transitions. Game ownership, damage/repair semantics, hooks and physics effects also require runtime evidence; unit tests do not establish those behaviors. Do not add empty placeholder directories or generalized feature frameworks before the implementation needs them.

## Build and tooling

The .NET SDK includes C# files recursively, so new source subfolders build automatically. The test project deliberately links only game-independent files; update its explicit paths when moving or adding tested code. Coverage selects those files by filename and remains scoped to the linked policies.

See the root README for build and test commands. Keep private diagnostics, generated outputs, game assemblies, saves and workspace archives outside this repository. A source reorganization should pass the existing tests and compile with the initialized loader; validate any separate diagnostic build against the new paths before using it.

This layout applies [Microsoft's separation-of-concerns guidance](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) to a small single-assembly trainer. Recursive compile behavior is documented in the [.NET SDK overview](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview).
