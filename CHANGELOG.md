# Changelog

## 0.8.1

- Add three persistent saved preset slots per mode, including Detailed's high-gear speed boost. Save/Load never stores protection or assist activation. Preserve the previous preset file as a backup, validate stored snapshots and keep saved slots intact after failed writes.
- Add Custom recall before built-in preset previews, retain it across mode switches, and commit pending valid numeric edits before mode/preset/save actions.
- Automatically request tuning ON after changed valid values or preset choices/loads. Keep manual OFF, startup/mode-switch OFF and stopped-only speed/gearing application.
- Add Simple and Detailed tuning modes, with Simple selected by default. Simple exposes Top speed, Acceleration, Grip, Steering and Braking, calculating the speed ceiling, forward gearing and compensating power together.
- Keep independently adjustable original settings and presets in Detailed. Save both profiles independently without activation. Mode changes switch tuning off; Restore stock resets both profiles. Speed/gearing edits wait until stopped, retaining the complete previous tune until then.
- Add one coloured Tuning toggle: grey OFF, green ON, amber WAITING/PAUSED. Group engine/speed controls together and handling controls together, with Detailed presets above the groups. ON reflects successfully applied performance values, rather than merely a requested switch. Disabling releases performance and gearing changes in both modes.
- Expose experimental gearing in Detailed as a High-gear speed boost slider from 1x to 3x, with plain-language help. Taller forward High gearing trades wheel torque for road speed per engine revolution; attainable speed still depends on power and resistance.
- Preserve Low, reverse, existing gear count/spacing and engine RPM settings. The detailed gear preference is session-only unless explicitly saved in a user preset. Restore stock releases it immediately and retains saved presets.
- Release the owned effective reduction around the local native input writer; reapply only for enabled High-forward input with a forward engaged gear and no shift. Pause during flight, reset on car/session loss or faults, validate drivetrain identity/isolation, and preserve unknown external writes.
- Add bounded input-hook telemetry for comparing range, selected/requested/engaged gear, RPM/limiter, speed, wheel angular velocity/slip and baseline/effective reduction. Extend independent policy tests and scoped coverage.

Development logs confirm the earlier overdrive-only candidate loaded and intercepted the input writer, applied 8-to-4 High reduction at 2x and retained stock reduction during shifts. The same drive held near 27 km/h with a 7.5 m/s speed-control setting, consistent with the speed limiter holding it below redline. Simple now coordinates that ceiling with gearing. Combined Simple driving, new mode rendering, Low/reverse and full restoration remain runtime-unverified. Automated checks and compilation do not establish gameplay behavior.

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
