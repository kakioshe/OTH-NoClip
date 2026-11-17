using System;
using System.IO;

// Filesystem access stays outside binding validation and menu presentation.
internal sealed class BindingStore
{
    private readonly string path;

    internal BindingStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A bindings path is required.", nameof(path));
        this.path = Path.GetFullPath(path);
    }

    internal TrainerBindings Load(out int rejected)
    {
        rejected = 0;
        return File.Exists(path) ? TrainerBindings.Parse(File.ReadAllText(path), out rejected) : new TrainerBindings();
    }

    internal void Save(TrainerBindings bindings)
    {
        if (bindings == null) throw new ArgumentNullException(nameof(bindings));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, bindings.Serialize());
        File.Move(temporary, path, true);
    }
}
