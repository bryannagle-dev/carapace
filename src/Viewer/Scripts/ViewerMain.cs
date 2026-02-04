using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using VoxelCore;
using VoxelCore.IO;
using VoxelCore.Meshing;

public partial class ViewerMain : Node3D
{
    [Export] public NodePath? MeshTargetPath;
    [Export] public NodePath? DimsLabelPath;
    [Export] public NodePath? VoxelCountLabelPath;
    [Export] public NodePath? MetadataLabelPath;
    [Export] public NodePath? CameraRigPath;

    private Label? _dimsLabel;
    private Label? _voxelCountLabel;
    private Label? _metadataLabel;
    private OrbitCamera? _cameraRig;
    private string _screenshotDir = string.Empty;

    public override void _Ready()
    {
        _dimsLabel = ResolveLabel(DimsLabelPath, "CanvasLayer/PanelContainer/VBoxContainer/LabelDims");
        _voxelCountLabel = ResolveLabel(VoxelCountLabelPath, "CanvasLayer/PanelContainer/VBoxContainer/LabelVoxelCount");
        _metadataLabel = ResolveLabel(MetadataLabelPath, "CanvasLayer/PanelContainer/VBoxContainer/LabelMetadata");
        _cameraRig = ResolveRig(CameraRigPath, "CameraRig");
        _screenshotDir = ResolveScreenshotDir();

        string[] args = OS.GetCmdlineUserArgs();
        string? filePath = GetArgValue(args, "--file");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            GD.Print("No --file argument provided.");
            return;
        }

        if (!File.Exists(filePath))
        {
            GD.Print($"File not found: {filePath}");
            return;
        }

        VxmData data = VxmCodec.Load(filePath);
        GD.Print($"Loaded {filePath} ({data.Grid.SizeX}x{data.Grid.SizeY}x{data.Grid.SizeZ}).");

        MeshInstance3D? target = GetMeshTarget();
        if (target == null)
        {
            GD.Print("No MeshInstance3D target found.");
            return;
        }

        ArrayMesh mesh = GreedyMesher.BuildMesh(data.Grid, data.PaletteRgba);
        target.Mesh = mesh;
        ApplyVertexColorMaterial(target);

        UpdateDebugUi(data);
        FocusCamera(data, target);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.F3)
        {
            _ = CaptureScreenshotAsync();
        }
    }

    private MeshInstance3D? GetMeshTarget()
    {
        if (MeshTargetPath != null && !MeshTargetPath.IsEmpty)
        {
            return GetNode<MeshInstance3D>(MeshTargetPath);
        }

        return GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
    }

    private Label? ResolveLabel(NodePath? path, string fallbackPath)
    {
        if (path != null && !path.IsEmpty)
        {
            return GetNodeOrNull<Label>(path);
        }

        return GetNodeOrNull<Label>(fallbackPath);
    }

    private OrbitCamera? ResolveRig(NodePath? path, string fallbackPath)
    {
        if (path != null && !path.IsEmpty)
        {
            return GetNodeOrNull<OrbitCamera>(path);
        }

        return GetNodeOrNull<OrbitCamera>(fallbackPath);
    }

    private void UpdateDebugUi(VxmData data)
    {
        int filled = CountFilled(data.Grid);
        _dimsLabel?.SetText($"Dims: {data.Grid.SizeX} x {data.Grid.SizeY} x {data.Grid.SizeZ}");
        _voxelCountLabel?.SetText($"Filled Voxels: {filled}");
        _metadataLabel?.SetText($"Metadata: {data.MetadataJson}");
    }

    private static void ApplyVertexColorMaterial(MeshInstance3D target)
    {
        if (target.Mesh == null || target.Mesh.GetSurfaceCount() == 0)
        {
            return;
        }

        StandardMaterial3D material = new()
        {
            VertexColorUseAsAlbedo = true,
            AlbedoColor = new Color(1f, 1f, 1f),
            Metallic = 0f,
            Roughness = 0.9f,
        };

        target.Mesh.SurfaceSetMaterial(0, material);
    }

    private static int CountFilled(VoxelGrid grid)
    {
        int count = 0;
        foreach (byte value in grid.Voxels)
        {
            if (value != 0)
            {
                count++;
            }
        }

        return count;
    }

    private string ResolveScreenshotDir()
    {
        string path = ProjectSettings.GlobalizePath("res://../../screenshots");
        Directory.CreateDirectory(path);
        return path;
    }

    private async Task CaptureScreenshotAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Image image = GetViewport().GetTexture().GetImage();
        string filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = Path.Combine(_screenshotDir, filename);

        Error result = image.SavePng(path);
        if (result != Error.Ok)
        {
            GD.PrintErr($"Failed to save screenshot: {result} ({path})");
        }
        else
        {
            GD.Print($"Saved screenshot: {path}");
        }
    }

    private void FocusCamera(VxmData data, MeshInstance3D target)
    {
        if (_cameraRig == null)
        {
            return;
        }

        if (!TryComputeBounds(data.Grid, out Vector3 min, out Vector3 max))
        {
            Vector3 centerFallback = new(data.Grid.SizeX / 2f, data.Grid.SizeY / 2f, data.Grid.SizeZ / 2f);
            _cameraRig.Focus(centerFallback, Mathf.Max(data.Grid.SizeX, Mathf.Max(data.Grid.SizeY, data.Grid.SizeZ)) * 0.5f);
            return;
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        float radius = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z)) * 0.5f;
        _cameraRig.Focus(center, radius);
    }

    private static bool TryComputeBounds(VoxelGrid grid, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;

        int sizeX = grid.SizeX;
        int sizeY = grid.SizeY;
        int sizeZ = grid.SizeZ;

        int index = 0;
        for (int z = 0; z < sizeZ; z++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++, index++)
                {
                    if (grid.Voxels[index] == 0)
                    {
                        continue;
                    }

                    any = true;
                    Vector3 voxelMin = new(x, y, z);
                    Vector3 voxelMax = new(x + 1, y + 1, z + 1);
                    min = new Vector3(Mathf.Min(min.X, voxelMin.X), Mathf.Min(min.Y, voxelMin.Y), Mathf.Min(min.Z, voxelMin.Z));
                    max = new Vector3(Mathf.Max(max.X, voxelMax.X), Mathf.Max(max.Y, voxelMax.Y), Mathf.Max(max.Z, voxelMax.Z));
                }
            }
        }

        return any;
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
