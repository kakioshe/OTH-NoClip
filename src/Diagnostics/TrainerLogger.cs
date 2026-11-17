using MelonLoader;

internal sealed class TrainerLogger
{
    private readonly MelonLogger.Instance logger;

    internal TrainerLogger(MelonLogger.Instance logger) { this.logger = logger; }
    internal void LogInfo(string message) => logger.Msg(message);
    internal void LogWarning(string message) => logger.Warning(message);
    internal void LogError(string message) => logger.Error(message);
}
