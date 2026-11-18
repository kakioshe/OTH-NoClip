using System;
using System.IO;

internal sealed class VehiclePreferenceStore
{
    private readonly string path;
    internal VehiclePreferenceStore(string path) { this.path = Path.GetFullPath(path); }
    internal VehiclePreferences Load(out int rejected)
    {
        rejected = 0;
        return File.Exists(path) ? VehiclePreferences.Parse(File.ReadAllText(path), out rejected) : new VehiclePreferences();
    }
    internal void Save(VehiclePreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, preferences.Serialize());
        File.Move(temporary, path, true);
    }
}
