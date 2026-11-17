using System;
using System.Collections.Generic;

internal static class LifetimeChecks
{
    internal static void Run(Action<string, bool> check)
    {
        var released = new List<int>();
        var failures = new List<Exception>();
        var scope = new TrainerLifetime(failures.Add);
        scope.Add(() => released.Add(1));
        scope.Add(() => throw new InvalidOperationException("cleanup failure"));
        scope.Add(() => released.Add(3));
        scope.Dispose();
        check("cleanup releases resources in reverse acquisition order", string.Join(",", released) == "3,1");
        check("cleanup reports failure while releasing remaining resources", failures.Count == 1 && failures[0].Message == "cleanup failure");
        scope.Dispose();
        check("cleanup is idempotent after a failure", released.Count == 2 && failures.Count == 1);
        bool rejected = false;
        try { scope.Add(() => released.Add(4)); }
        catch (ObjectDisposedException) { rejected = true; }
        check("disposed lifetime rejects new ownership", rejected);

        var empty = new TrainerLifetime(failures.Add);
        empty.Dispose();
        check("empty cleanup succeeds", failures.Count == 1);
        var reentrant = new TrainerLifetime(failures.Add);
        reentrant.Add(() => released.Add(5));
        reentrant.Add(reentrant.Dispose);
        reentrant.Dispose();
        check("reentrant cleanup releases each resource once", string.Join(",", released) == "3,1,5");

        rejected = false;
        try { new TrainerLifetime(null); }
        catch (ArgumentNullException) { rejected = true; }
        check("lifetime requires a failure reporter", rejected);
        using var valid = new TrainerLifetime(failures.Add);
        rejected = false;
        try { valid.Add(null); }
        catch (ArgumentNullException) { rejected = true; }
        check("lifetime rejects missing release operations", rejected);

        // Model a constructor failing after acquiring its first resource.
        bool restored = false;
        try
        {
            using var partial = new TrainerLifetime(failures.Add);
            partial.Add(() => restored = true);
            throw new InvalidOperationException("initialization failure");
        }
        catch (InvalidOperationException) { }
        check("partial initialization releases previously acquired resources", restored);
    }
}
