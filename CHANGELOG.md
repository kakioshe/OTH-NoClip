# Changelog

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
