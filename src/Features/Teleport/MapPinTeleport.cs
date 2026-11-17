using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using CommandTerminal = Il2CppCommandTerminal;

internal sealed class MapPinTeleport
{
    private readonly LocalVehicleProvider vehicles;
    private readonly TrainerLogger logger;

    internal MapPinTeleport(LocalVehicleProvider vehicles, TrainerLogger logger)
    {
        this.vehicles = vehicles;
        this.logger = logger;
    }

    internal void Execute(bool flightActive)
    {
        try
        {
            if (flightActive) { logger.LogWarning("Disable flight before teleporting to a map pin."); return; }
            if (!vehicles.TryGet(out _)) { logger.LogWarning("Teleport unavailable: local single-player vehicle not found."); return; }
            var map = MapNetworkManager.Instance;
            if (map == null || !map.PlayerRefToPinpointPosition.TryGet(OverlandNetworkRunner.LocalPlayerRef, out _))
            {
                logger.LogWarning("Teleport unavailable: place a map pin first.");
                return;
            }
            CheatsController.TeleportAtPin(new Il2CppReferenceArray<CommandTerminal.CommandArg>(0));
            logger.LogInfo("Requested teleport to the local player's map pin.");
        }
        catch (Exception error) { logger.LogError("Map-pin teleport failed: " + error); }
    }
}
