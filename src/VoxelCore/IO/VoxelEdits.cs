using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VoxelCore.IO;

public sealed class VoxelEdits
{
    public List<VoxelCoord> Added { get; set; } = new();
    public List<VoxelCoord> Removed { get; set; } = new();

    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0;

    public static string GetEditsPath(string vxmPath)
    {
        return Path.ChangeExtension(vxmPath, ".edits.json");
    }

    public static void Save(string vxmPath, VoxelEdits edits)
    {
        string path = GetEditsPath(vxmPath);
        SaveToPath(path, edits);
    }

    public static void SaveToPath(string path, VoxelEdits edits)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };
        string json = JsonSerializer.Serialize(edits, options);
        File.WriteAllText(path, json);
    }

    public static bool TryLoad(string vxmPath, out VoxelEdits edits)
    {
        string path = GetEditsPath(vxmPath);
        return TryLoadPath(path, out edits);
    }

    public static bool TryLoadPath(string path, out VoxelEdits edits)
    {
        edits = new VoxelEdits();
        if (!File.Exists(path))
        {
            return false;
        }

        string json = File.ReadAllText(path);
        edits = JsonSerializer.Deserialize<VoxelEdits>(json) ?? new VoxelEdits();
        return true;
    }

    public void Apply(VoxelGrid grid, byte material = 1)
    {
        foreach (VoxelCoord coord in Added)
        {
            grid.Set(coord.X, coord.Y, coord.Z, material);
        }

        foreach (VoxelCoord coord in Removed)
        {
            grid.Set(coord.X, coord.Y, coord.Z, 0);
        }
    }
}

public struct VoxelCoord
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public VoxelCoord(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
