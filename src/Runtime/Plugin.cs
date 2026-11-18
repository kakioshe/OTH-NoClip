using System.IO;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(Plugin), "Port's Trainer", "0.8.1", "Port")]
[assembly: MelonGame("Funselektor", "over the hill")]

public sealed class Plugin : MelonMod
{
    private TrainerRuntime runtime;

    public override void OnInitializeMelon()
    {
        var logger = new TrainerLogger(LoggerInstance);
        var bindingsPath = Path.Combine(MelonEnvironment.UserDataDirectory, "PortsTrainer.bindings.cfg");
        runtime = new TrainerRuntime(logger, bindingsPath);
    }

    public override void OnUpdate() { runtime?.Update(); }
    public override void OnLateUpdate() { runtime?.LateUpdate(); }
    public override void OnGUI() { runtime?.Draw(); }
    public override void OnDeinitializeMelon() { runtime?.Dispose(); runtime = null; }
}
