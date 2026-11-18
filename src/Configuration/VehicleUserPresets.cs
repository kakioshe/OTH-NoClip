using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// Immutable snapshots contain tuning values only, never activation or assists.
internal sealed class VehicleTuneSnapshot
{
    private readonly VehiclePreferences preferences;
    internal bool Simple { get; }
    internal float Gearing { get; }
    internal VehiclePreferences CopyPreferences() => preferences.Copy();

    internal VehicleTuneSnapshot(bool simple, VehiclePreferences values, float gearing)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (!VehicleOverdrive.ValidMultiplier(gearing) || simple && gearing != 1)
            throw new ArgumentOutOfRangeException(nameof(gearing));
        foreach (VehicleSetting setting in Enum.GetValues(typeof(VehicleSetting)))
            if (simple && !SimpleVehicleTuning.Valid(setting, values.Get(setting)))
                throw new ArgumentOutOfRangeException(nameof(values));
        Simple = simple; Gearing = gearing; preferences = values.Copy();
    }
}

internal sealed class VehicleUserPresets
{
    internal const int SlotCount = 3;
    private readonly string path;
    private readonly VehicleTuneSnapshot[,] slots = new VehicleTuneSnapshot[2, SlotCount];
    internal VehicleUserPresets(string path) { this.path = Path.GetFullPath(path); }

    internal bool TryGet(bool simple, int slot, out VehicleTuneSnapshot snapshot)
    {
        CheckSlot(slot);
        snapshot = slots[simple ? 0 : 1, slot];
        return snapshot != null;
    }

    internal void Load(out int rejected)
    {
        rejected = 0;
        if (!File.Exists(path)) return;
        var document = JsonSerializer.Deserialize<PresetFile>(File.ReadAllText(path));
        if (document == null || document.Version != 1 || document.Entries == null)
            throw new InvalidDataException("Unsupported vehicle preset file.");
        var loaded = new VehicleTuneSnapshot[2, SlotCount];
        foreach (var entry in document.Entries)
        {
            if (entry == null || entry.Slot < 1 || entry.Slot > SlotCount || entry.Preferences == null)
            { rejected++; continue; }
            int mode = entry.Simple ? 0 : 1, slot = entry.Slot - 1;
            var values = VehiclePreferences.Parse(entry.Preferences, out int invalid);
            // Require a full canonical snapshot, not a partially defaulted tune.
            if (invalid != 0 || values.Serialize() != entry.Preferences || loaded[mode, slot] != null)
            { rejected++; continue; }
            try { loaded[mode, slot] = new VehicleTuneSnapshot(entry.Simple, values, entry.HighGearSpeedBoost); }
            catch (ArgumentOutOfRangeException) { rejected++; }
        }
        Array.Copy(loaded, slots, loaded.Length);
    }

    internal void Save(bool simple, int slot, VehicleTuneSnapshot snapshot)
    {
        CheckSlot(slot);
        if (snapshot == null || snapshot.Simple != simple) throw new ArgumentException("Preset mode does not match.", nameof(snapshot));
        var document = new PresetFile { Version = 1 };
        for (int mode = 0; mode < 2; mode++)
            for (int index = 0; index < SlotCount; index++)
            {
                var value = mode == (simple ? 0 : 1) && index == slot ? snapshot : slots[mode, index];
                if (value == null) continue;
                document.Entries.Add(new PresetEntry {
                    Simple = value.Simple, Slot = index + 1,
                    Preferences = value.CopyPreferences().Serialize(), HighGearSpeedBoost = value.Gearing
                });
            }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        File.Move(temporary, path, true);
        // A failed disk write must leave the previously saved in-memory slot intact.
        slots[simple ? 0 : 1, slot] = snapshot;
    }

    private static void CheckSlot(int slot)
    { if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot)); }

    internal sealed class PresetFile
    {
        public PresetFile() { }
        public int Version { get; set; }
        public List<PresetEntry> Entries { get; set; } = new();
    }

    internal sealed class PresetEntry
    {
        public PresetEntry() { }
        public bool Simple { get; set; }
        public int Slot { get; set; }
        public string Preferences { get; set; }
        public float HighGearSpeedBoost { get; set; } = 1;
    }
}
