using System;
using Il2Cpp;

// Composition and frame ordering live here; features do not own the menu or input polling.
internal sealed class TrainerRuntime : IDisposable
{
    private readonly TrainerLifetime lifetime;
    private readonly TrainerInput input;
    private readonly TrainerMenu menu;
    private readonly Flight flight;
    private readonly MapPinTeleport teleport;
    private readonly GameInputBlocker gameInput;
    private readonly GameBindingSuppression gameBindings;
    private bool disposed;

    internal TrainerRuntime(TrainerLogger logger, string bindingsPath)
    {
        lifetime = new TrainerLifetime(error => logger.LogWarning("Trainer cleanup failed: " + error));
        try
        {
            var vehicles = new LocalVehicleProvider();
            var pose = new PoseSuppression(logger);
            lifetime.Add(pose.Dispose);
            input = new TrainerInput();
            menu = new TrainerMenu(input, new BindingStore(bindingsPath), logger);
            lifetime.Add(menu.Close);
            flight = new Flight(vehicles, pose, logger);
            lifetime.Add(flight.Stop);
            teleport = new MapPinTeleport(vehicles, logger);
            gameInput = new GameInputBlocker(() => vehicles.SessionReady, () => AcquireGameInputBlock(logger), logger.LogWarning);
            lifetime.Add(gameInput.Release);
            gameBindings = new GameBindingSuppression(() => GameInput.IsReady && vehicles.SessionReady,
                () => flight.Active, () => menu.Bindings, logger);
            lifetime.Add(gameBindings.Dispose);
            logger.LogInfo("Port's Trainer: Insert opens the controls menu. Camera-relative flight. Default F8 flight / F9 map-pin teleport. v0.6.1.");
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
        flight.Update();
        if (!menu.BlocksActions)
        {
            if (input.Pressed(menu.Bindings, TrainerAction.Flight)) flight.Toggle();
            if (input.Pressed(menu.Bindings, TrainerAction.Teleport)) teleport.Execute(flight.Active);
        }
        gameInput.Update(menu.Visible, input.Released);
        gameBindings.Update();
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
        lifetime.Dispose();
    }

    private static Action AcquireGameInputBlock(TrainerLogger logger)
    {
        const int trainerMapPriority = 10000;
        var token = GameInput.PushMap(GameInput.Map.GameHasNoFocus, trainerMapPriority);
        logger.LogInfo("Trainer acquired gameplay input block.");
        return () => { GameInput.PopMap(token); logger.LogInfo("Trainer released gameplay input block."); };
    }
}
