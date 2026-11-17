using System;

// Own one release callback. Never replace the game's stack or restore another screen's map.
internal sealed class GameInputBlocker
{
    private readonly Func<bool> eligible;
    private readonly Func<Action> acquire;
    private readonly Action<string> warning;
    private Action release;
    private bool failed;

    internal GameInputBlocker(Func<bool> eligible, Func<Action> acquire, Action<string> warning)
    {
        this.eligible = eligible; this.acquire = acquire; this.warning = warning;
    }

    internal void Update(bool requested, bool controlsReleased)
    {
        try
        {
            if (!eligible() || (!requested && controlsReleased))
            {
                Release();
                if (release == null) failed = false;
                return;
            }
            if (requested && release == null && !failed) release = acquire();
        }
        catch (Exception error)
        {
            if (!failed) warning("Gameplay input block unavailable; use arrow-key flight defaults. " + error.Message);
            failed = true;
        }
    }

    internal void Release()
    {
        if (release == null) return;
        try { release(); release = null; }
        catch (Exception error)
        {
            if (!failed) warning("Could not release gameplay input block: " + error.Message);
            failed = true; // Retain ownership and retry cleanup, without logging every frame.
        }
    }
}
