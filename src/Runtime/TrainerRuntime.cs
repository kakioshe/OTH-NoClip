using System;
using System.IO;
using Il2Cpp;

// Composition and frame ordering live here; features do not own the menu or input polling.
internal sealed class TrainerRuntime : IDisposable
{
    private readonly TrainerLifetime lifetime;
    private readonly TrainerInput input;
    private readonly TrainerMenu menu;
    private readonly Flight flight;
    private readonly MapPinTeleport teleport;
    private readonly VehicleRepair repair;
    private readonly VehicleFlip flip;
    private readonly VehicleControls vehicleControls;
    private readonly VehicleAssists vehicleAssists;
    private readonly VehiclePreferenceStore vehicleStore;
    private bool vehiclePreferencesDirty;
    private readonly GameInputBlocker gameInput;
    private readonly GameBindingSuppression gameBindings;
    private bool disposed;

    internal TrainerRuntime(TrainerLogger logger, string bindingsPath)
    {
        vehicleSaveWarning = logger.LogWarning;
        lifetime = new TrainerLifetime(error => logger.LogWarning("Trainer cleanup failed: " + error));
        try
        {
            var vehicles = new LocalVehicleProvider();
            var pose = new PoseSuppression(logger);
            lifetime.Add(pose.Dispose);
            input = new TrainerInput();
            repair = new VehicleRepair(() => VehicleRepairTarget.Resolve(vehicles), logger.LogInfo);
            lifetime.Add(repair.Cancel);
            vehicleStore = new VehiclePreferenceStore(Path.Combine(Path.GetDirectoryName(bindingsPath), "PortsTrainer.vehicle.cfg"));
            var preferences = new VehiclePreferences();
            try
            {
                preferences = vehicleStore.Load(out int rejected);
                if (rejected > 0) logger.LogWarning("Ignored " + rejected + " invalid vehicle preferences.");
            }
            catch (Exception error) { logger.LogWarning("Vehicle preferences unavailable; using defaults: " + error.Message); }
            var vehicleHooks = new VehicleControlHooks(vehicles, logger);
            lifetime.Add(vehicleHooks.Dispose);
            vehicleControls = new VehicleControls(preferences, vehicleHooks.Resolve, logger.LogWarning);
            lifetime.Add(vehicleControls.Dispose);
            flight = new Flight(vehicles, pose, logger);
            lifetime.Add(flight.Stop);
            var assistHooks = new VehicleAssistHooks(vehicles, () => flight.Active, () => !input.Focused || menu == null || menu.BlocksActions, logger.LogWarning, logger.LogInfo);
            lifetime.Add(assistHooks.Dispose);
            vehicleAssists = new VehicleAssists(assistHooks.Resolve, logger.LogWarning);
            assistHooks.Failed = vehicleAssists.Fail;
            lifetime.Add(vehicleAssists.Dispose);
            flip = new VehicleFlip(() => VehicleFlipTarget.Resolve(vehicles, () => flight.Active), logger.LogInfo);
            lifetime.Add(flip.Stop);
            var vehicleMenu = new VehicleMenu(repair, flip, vehicleControls, vehicleAssists, () => vehiclePreferencesDirty = true);
            menu = new TrainerMenu(input, new BindingStore(bindingsPath), logger, vehicleMenu.Draw);
            lifetime.Add(menu.Close);
            teleport = new MapPinTeleport(vehicles, logger);
            gameInput = new GameInputBlocker(() => vehicles.SessionReady, () => AcquireGameInputBlock(logger), logger.LogWarning);
            lifetime.Add(gameInput.Release);
            gameBindings = new GameBindingSuppression(() => GameInput.IsReady && vehicles.SessionReady,
                () => flight.Active, () => menu.Bindings, logger);
            lifetime.Add(gameBindings.Dispose);
            logger.LogInfo("Port's Trainer: Insert opens Movement and Vehicle controls. Default F8 flight / F9 map-pin teleport. v0.8.0.");
        }
        catch
        {
            lifetime.Dispose();
            throw;
        }
    }

    internal void Update()
    {
        if (disposed) return;
        input.Poll();
        menu.Update();
        if (menu.Visible && input.Focused) { repair.Update(); }
        else { repair.Cancel(); flip.Cancel(); }
        flip.Update();
        flight.Update();
        if (!menu.BlocksActions)
        {
            if (input.Pressed(menu.Bindings, TrainerAction.Flight)) flight.Toggle();
            if (input.Pressed(menu.Bindings, TrainerAction.Teleport)) teleport.Execute(flight.Active);
        }
        gameInput.Update(menu.Visible, input.Released);
        gameBindings.Update();
        vehicleControls.Update(flight.Active);
        vehicleAssists.Update(flight.Active);
        if (vehiclePreferencesDirty && input.Released) SaveVehiclePreferences();
    }

    internal void LateUpdate()
    {
        if (disposed) return;
        bool hold = menu.BlocksActions;
        flight.LateUpdate(new FlightControls(
            hold ? 0 : Value(TrainerAction.Forward) - Value(TrainerAction.Back),
            hold ? 0 : Value(TrainerAction.Right) - Value(TrainerAction.Left),
            hold ? 0 : Value(TrainerAction.Rise) - Value(TrainerAction.Descend),
            !hold && Value(TrainerAction.Boost) > 0.5f, hold));
        menu.MaintainCursor();
    }

    private float Value(TrainerAction action) => input.Value(menu.Bindings, action);
    internal void Draw() { if (!disposed) menu.Draw(); }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (vehiclePreferencesDirty) SaveVehiclePreferences();
        lifetime.Dispose();
    }

    private void SaveVehiclePreferences()
    {
        vehiclePreferencesDirty = false;
        try { vehicleStore.Save(vehicleControls.Preferences); }
        catch (Exception error) { vehicleSaveWarning?.Invoke("Vehicle preferences work this session but could not be saved: " + error.Message); }
    }

    private readonly Action<string> vehicleSaveWarning;

    private static Action AcquireGameInputBlock(TrainerLogger logger)
    {
        const int trainerMapPriority = 10000;
        var token = GameInput.PushMap(GameInput.Map.GameHasNoFocus, trainerMapPriority);
        logger.LogInfo("Trainer acquired gameplay input block.");
        return () => { GameInput.PopMap(token); logger.LogInfo("Trainer released gameplay input block."); };
    }
}
