# Changelog

## 0.8.1

- Add experimental High-range overdrive at 1x (stock), 1.5x, 2x and 3x, with stopped-only selection and anchored help. Taller forward High gearing trades wheel torque for road speed per engine revolution; attainable speed still depends on power and resistance.
- Preserve Low, reverse, existing gear count/spacing and engine RPM settings. Keep overdrive session-only and independent of saved presets and performance activation. Restore stock releases it immediately.
- Release the owned effective reduction around the local native input writer; reapply only for enabled High-forward input with a forward engaged gear and no shift. Pause during flight, reset on car/session loss or faults, validate drivetrain identity/isolation, and preserve unknown external writes.
- Add bounded input-hook telemetry for comparing range, selected/requested/engaged gear, RPM/limiter, speed, wheel angular velocity/slip and baseline/effective reduction. Extend independent policy tests and scoped coverage.

Runtime validation of the new gearing, native interception, Low/reverse behavior and restoration is pending. Automated checks and compilation do not establish gameplay behavior.

## 0.8.0

This release combines all earlier menu and movement features, the 0.6.1 source refactor, and the new vehicle controls. Existing release tags are preserved.

- Add Movement and Vehicle tabs with matching section styling, compact controls and anchored help popups. Switching tabs cancels binding capture.
- Add Repair car through the game's healing operation and Flip car through vehicle repositioning and network physics teleport. Flip retains heading, lifts the car slightly and stops motion; successful recovery leaves no persistent confirmation text.
- Add independent God mode for new vehicle damage. Existing damage and engine flooding are handled separately.
- Add power (0.5–10x), speed limit (0.5–10x), grip (0.5–2x), steering angle (0.5–1.5x) and braking strength (0.5–2x), with sliders and validated numeric entry.
- Group tuning under an Active/Inactive switch, save preferences separately from bindings, and add Stock, Trail, Climb and Fast presets. Presets change values without enabling controls; Restore stock disables performance modifiers and resets tuning to 1x.
- Apply tuning relative to captured legitimate vehicle values. Restore owned values when disabled, pause performance modifiers during flight, rebase around known game writers and stop on unknown external writes. Refuse shared settings and reset activation on vehicle/session loss.
- Add separate Water immunity to prevent new engine flooding and suppress local wheel terrain resistance during water contact. Existing flooded state, normal drying, grip and brakes remain independent; this does not provide buoyancy.
- Add Spider mode with a surface-relative motor, contact probes, adhesion, gravity compensation and alignment for driving on slopes, walls and ceilings. Use normal game driving controls; release throttle or use the handbrake to hold. Pause during flight and release when usable contact is lost.
- Preserve Insert menu access, F8 flight, F9 map-pin teleport, saved keyboard/Xbox bindings, camera-relative flight and selective game-input handling.
- Retain the 0.6.1 separation of runtime, features, game adapters, input, configuration and UI, including cleanup after shutdown or partial initialization.
- Expand game-independent tests and scoped coverage for tuning, presets, storage, ownership/rollback, recovery commands, protection, numeric input, assists and the Spider mode motor policy.

Development testing: the user reported repair/settings, revised flip and the combined driving/water revision working successfully. Logs captured four-wheel water-resistance suppression and owned-value restoration. The final rename/version metadata and standard release build have not been separately gameplay-tested. Exhaustive protection, upgrade/session transitions, wall/ceiling transitions, controller/manual reverse and extreme tuning coverage remain unverified. Automated checks cover independent policies, not the full game integration.

## 0.6.1

This maintenance release follows the previously published 0.7.0 menu build. It retains those features while assigning the source organization and refactor to 0.6.1. The earlier 0.7.0 release remains part of the published history.

- Group source by feature and responsibility, with documented ownership boundaries and guidance for future vehicle controls.
- Separate loader lifecycle, frame orchestration, flight, map-pin teleport, vehicle lookup and binding storage into focused classes with explicit dependencies.
- Release owned hooks and input resources during shutdown and partial initialization; cover resource-lifetime and storage behavior with automated checks.

## 0.7.0

- Add Port's Trainer, a compact menu opened with Insert.
- Add persistent keyboard and Xbox controller bindings, modifier shortcuts, conflict validation and restore defaults.
- Make flight movement and vehicle facing follow the camera's horizontal heading. Keep height controls separate and release the old turn controls for camera input.
- Reserve only controls used by available trainer actions so unused game controls, including the map, remain accessible during flight.
- Hold active flight while the controls menu is open and restore cursor/input state when it closes.
- Use arrow keys for height and B for boost by default. Migrate older bindings while retaining custom action settings.
- Add automated checks and scoped coverage for bindings, camera headings and input policies.

## 0.6.0

- Add map-pin teleport for the local single-player vehicle.
- Use MelonLoader for plugin loading.
