using System;
using System.Collections.Generic;
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
    private MeshInstance3D? _meshTarget;
    private VoxelGrid? _grid;
    private byte[]? _palette;
    private byte[]? _baseVoxels;
    private string _metadataJson = "{}";
    private string _filePath = string.Empty;
    private StaticBody3D? _pickBody;
    private CollisionShape3D? _pickShape;
    private MultiMeshInstance3D? _selectionMesh;
    private MeshInstance3D? _cursorMesh;
    private readonly HashSet<Vector3I> _selectedVoxels = new();
    private Vector3I _activeVoxel;
    private bool _hasActiveVoxel;
    private bool _editMode;
    private readonly Stack<List<VoxelChange>> _undoStack = new();
    private readonly Stack<List<VoxelChange>> _redoStack = new();

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

        VxmData data = LoadVoxelData(filePath);
        GD.Print($"Loaded {filePath} ({data.Grid.SizeX}x{data.Grid.SizeY}x{data.Grid.SizeZ}).");

        _filePath = filePath;
        _grid = data.Grid;
        _palette = data.PaletteRgba;
        _metadataJson = data.MetadataJson;
        _baseVoxels = (byte[])data.Grid.Voxels.Clone();

        _meshTarget = GetMeshTarget();
        if (_meshTarget == null)
        {
            GD.Print("No MeshInstance3D target found.");
            return;
        }

        RebuildMesh();
        EnsureSelectionMesh();
        EnsureCursorMesh();
        UpdateDebugUi();
        FocusCamera(data, _meshTarget);
    }

    public override void _Process(double delta)
    {
        if (!_editMode)
        {
            return;
        }

        UpdateCursorFromMouse();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F3)
            {
                _ = CaptureScreenshotAsync();
                return;
            }

            if (keyEvent.Keycode == Key.F4)
            {
                SaveEdits();
                return;
            }

            if (keyEvent.Keycode == Key.F2)
            {
                ToggleEditMode();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_editMode && HandleEditHotkeys(keyEvent))
            {
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (!_editMode)
            {
                return;
            }

            if (_grid == null)
            {
                return;
            }

            if (!mouseButton.ShiftPressed)
            {
                return;
            }

            if (mouseButton.ButtonIndex != MouseButton.Left && mouseButton.ButtonIndex != MouseButton.Right)
            {
                return;
            }

            if (TryGetVoxelFromClick(true, out Vector3I voxel) && _grid.GetSafe(voxel.X, voxel.Y, voxel.Z) != 0)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    AddToSelection(voxel);
                }
                else
                {
                    RemoveFromSelection(voxel);
                }

                GetViewport().SetInputAsHandled();
            }
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

    private void UpdateDebugUi()
    {
        if (_grid == null)
        {
            return;
        }

        int filled = CountFilled(_grid);
        _dimsLabel?.SetText($"Dims: {_grid.SizeX} x {_grid.SizeY} x {_grid.SizeZ}");
        _voxelCountLabel?.SetText($"Filled Voxels: {filled}");

        string meta = _metadataJson;
        if (_selectedVoxels.Count > 0)
        {
            meta += $"\nSelected: {_selectedVoxels.Count}";
        }

        if (_hasActiveVoxel)
        {
            meta += $"\nActive: {_activeVoxel.X}, {_activeVoxel.Y}, {_activeVoxel.Z}";
        }

        meta += $"\nEdit Mode: {(_editMode ? "ON" : "OFF")}";

        _metadataLabel?.SetText($"Metadata: {meta}");
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

    private void RebuildMesh()
    {
        if (_grid == null || _palette == null || _meshTarget == null)
        {
            return;
        }

        ArrayMesh mesh = GreedyMesher.BuildMesh(_grid, _palette);
        _meshTarget.Mesh = mesh;
        ApplyVertexColorMaterial(_meshTarget);
        UpdateCollision(mesh);
    }

    private void EnsureSelectionMesh()
    {
        if (_meshTarget == null || _selectionMesh != null)
        {
            return;
        }

        _selectionMesh = new MultiMeshInstance3D
        {
            Name = "SelectionVoxels",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };

        BoxMesh box = new()
        {
            Size = Vector3.One * 1.02f,
        };

        MultiMesh multi = new()
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = box,
            InstanceCount = 0,
        };

        _selectionMesh.Multimesh = multi;

        StandardMaterial3D mat = new()
        {
            AlbedoColor = new Color(1f, 1f, 0f),
            Metallic = 0f,
            Roughness = 0.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
        };
        _selectionMesh.MaterialOverride = mat;

        _meshTarget.AddChild(_selectionMesh);
    }

    private void EnsureCursorMesh()
    {
        if (_meshTarget == null || _cursorMesh != null)
        {
            return;
        }

        _cursorMesh = new MeshInstance3D
        {
            Name = "CursorVoxel",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };

        BoxMesh box = new()
        {
            Size = Vector3.One * 1.04f,
        };
        _cursorMesh.Mesh = box;

        StandardMaterial3D mat = new()
        {
            AlbedoColor = new Color(0.3f, 1f, 0.3f, 0.35f),
            Metallic = 0f,
            Roughness = 0.3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _cursorMesh.MaterialOverride = mat;

        _meshTarget.AddChild(_cursorMesh);
    }

    private void UpdateSelectionMesh()
    {
        if (_selectionMesh == null || _selectionMesh.Multimesh == null)
        {
            return;
        }

        int count = _selectedVoxels.Count;
        if (count == 0)
        {
            _selectionMesh.Visible = false;
            _selectionMesh.Multimesh.InstanceCount = 0;
            return;
        }

        _selectionMesh.Visible = true;
        _selectionMesh.Multimesh.InstanceCount = count;

        int index = 0;
        foreach (Vector3I voxel in _selectedVoxels)
        {
            Transform3D transform = new(Basis.Identity, new Vector3(voxel.X + 0.5f, voxel.Y + 0.5f, voxel.Z + 0.5f));
            _selectionMesh.Multimesh.SetInstanceTransform(index++, transform);
        }
    }

    private void UpdateCursorFromMouse()
    {
        if (_cursorMesh == null || _grid == null)
        {
            return;
        }

        if (TryGetVoxelFromClick(true, out Vector3I voxel))
        {
            _cursorMesh.Visible = true;
            _cursorMesh.Position = new Vector3(voxel.X + 0.5f, voxel.Y + 0.5f, voxel.Z + 0.5f);
        }
        else
        {
            _cursorMesh.Visible = false;
        }
    }

    private void SetActiveVoxel(Vector3I voxel)
    {
        _activeVoxel = voxel;
        _hasActiveVoxel = true;
    }

    private void AddToSelection(Vector3I voxel)
    {
        if (_selectedVoxels.Add(voxel))
        {
            UpdateSelectionMesh();
        }

        SetActiveVoxel(voxel);
        UpdateDebugUi();
    }

    private void RemoveFromSelection(Vector3I voxel)
    {
        if (_selectedVoxels.Remove(voxel))
        {
            if (_hasActiveVoxel && voxel == _activeVoxel)
            {
                if (TryGetAnySelected(out Vector3I next))
                {
                    SetActiveVoxel(next);
                }
                else
                {
                    _hasActiveVoxel = false;
                }
            }

            UpdateSelectionMesh();
            UpdateDebugUi();
        }
    }

    private void ClearSelection()
    {
        _selectedVoxels.Clear();
        _hasActiveVoxel = false;
        UpdateSelectionMesh();
        UpdateDebugUi();
    }

    private bool TryGetAnySelected(out Vector3I voxel)
    {
        foreach (Vector3I item in _selectedVoxels)
        {
            voxel = item;
            return true;
        }

        voxel = default;
        return false;
    }

    private void ToggleEditMode()
    {
        SetEditMode(!_editMode);
    }

    private void SetEditMode(bool enabled)
    {
        _editMode = enabled;
        if (_cameraRig != null)
        {
            _cameraRig.EnableMovement = !enabled;
        }

        if (!enabled)
        {
            ClearSelection();
            if (_cursorMesh != null)
            {
                _cursorMesh.Visible = false;
            }
        }

        UpdateDebugUi();
    }

    private void UpdateCollision(ArrayMesh mesh)
    {
        if (_meshTarget == null)
        {
            return;
        }

        if (_pickBody == null)
        {
            _pickBody = new StaticBody3D
            {
                CollisionLayer = 1,
                CollisionMask = 1,
                Name = "PickBody",
            };
            _pickShape = new CollisionShape3D();
            _pickBody.AddChild(_pickShape);
            _meshTarget.AddChild(_pickBody);
        }

        if (_pickShape == null)
        {
            return;
        }

        if (mesh.GetSurfaceCount() == 0)
        {
            _pickShape.Shape = null;
            return;
        }

        ConcavePolygonShape3D shape = new();
        shape.Data = mesh.GetFaces();
        _pickShape.Shape = shape;
    }

    private bool TryGetVoxelFromClick(bool selectMode, out Vector3I voxel)
    {
        voxel = default;

        if (_meshTarget == null || _grid == null)
        {
            return false;
        }

        if (!TryRaycast(out Vector3 hitPos, out Vector3 hitNormal))
        {
            return false;
        }

        Vector3 localHit = _meshTarget.ToLocal(hitPos);
        Vector3 localNormal = (_meshTarget.ToLocal(hitPos + hitNormal) - localHit).Normalized();
        Vector3 offset = localNormal * 0.01f * (selectMode ? -1f : 1f);
        Vector3 adjusted = localHit + offset;

        int x = Mathf.FloorToInt(adjusted.X);
        int y = Mathf.FloorToInt(adjusted.Y);
        int z = Mathf.FloorToInt(adjusted.Z);

        voxel = new Vector3I(x, y, z);
        return _grid.InBounds(voxel.X, voxel.Y, voxel.Z);
    }

    private bool TryRaycast(out Vector3 hitPosition, out Vector3 hitNormal)
    {
        hitPosition = default;
        hitNormal = default;

        Camera3D? camera = _cameraRig?.GetNodeOrNull<Camera3D>("Camera3D") ?? GetViewport().GetCamera3D();
        if (camera == null)
        {
            return false;
        }

        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector3 from = camera.ProjectRayOrigin(mousePos);
        Vector3 dir = camera.ProjectRayNormal(mousePos);

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, from + dir * 1000f);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.CollisionMask = 1;

        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
        {
            return false;
        }

        if (result.TryGetValue("position", out Variant positionValue) &&
            result.TryGetValue("normal", out Variant normalValue))
        {
            hitPosition = positionValue.As<Vector3>();
            hitNormal = normalValue.As<Vector3>();
            return true;
        }

        return false;
    }

    private void SaveEdits()
    {
        if (_grid == null || _palette == null)
        {
            GD.PrintErr("No voxel data to save.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_filePath))
        {
            GD.PrintErr("No file path available to save.");
            return;
        }

        try
        {
            if (IsVoxFile(_filePath))
            {
                VoxCodecOptional.Save(_grid, _filePath, _palette);
            }
            else
            {
                VxmCodec.Save(_grid, _filePath, _palette, _metadataJson);
            }
            VoxelEdits edits = BuildEdits();
            string editsPath = VoxelEdits.GetEditsPath(_filePath);

            if (edits.IsEmpty)
            {
                if (File.Exists(editsPath))
                {
                    File.Delete(editsPath);
                }
            }
            else
            {
                VoxelEdits.Save(_filePath, edits);
            }

            GD.Print($"Saved edits: {_filePath} (+{edits.Added.Count} / -{edits.Removed.Count})");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to save edits: {ex.Message}");
        }
    }

    private VoxelEdits BuildEdits()
    {
        VoxelEdits edits = new();

        if (_grid == null || _baseVoxels == null)
        {
            return edits;
        }

        if (_baseVoxels.Length != _grid.Voxels.Length)
        {
            return edits;
        }

        int sizeX = _grid.SizeX;
        int sizeY = _grid.SizeY;
        int sizeZ = _grid.SizeZ;

        int index = 0;
        for (int z = 0; z < sizeZ; z++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++, index++)
                {
                    byte before = _baseVoxels[index];
                    byte after = _grid.Voxels[index];

                    if (before == 0 && after != 0)
                    {
                        edits.Added.Add(new VoxelCoord(x, y, z));
                    }
                    else if (before != 0 && after == 0)
                    {
                        edits.Removed.Add(new VoxelCoord(x, y, z));
                    }
                }
            }
        }

        return edits;
    }

    private bool HandleEditHotkeys(InputEventKey keyEvent)
    {
        if (_grid == null)
        {
            return false;
        }

        if (keyEvent.CtrlPressed)
        {
            if (keyEvent.Keycode == Key.Z)
            {
                UndoLast();
                return true;
            }

            if (keyEvent.Keycode == Key.Y)
            {
                RedoLast();
                return true;
            }
        }

        if (keyEvent.Keycode == Key.Backspace || keyEvent.Keycode == Key.Delete)
        {
            RemoveSelectedVoxels();
            return true;
        }

        if (!_hasActiveVoxel)
        {
            return false;
        }

        Vector3I delta = keyEvent.Keycode switch
        {
            Key.W => new Vector3I(0, 0, 1),
            Key.S => new Vector3I(0, 0, -1),
            Key.A => new Vector3I(-1, 0, 0),
            Key.D => new Vector3I(1, 0, 0),
            Key.Q => new Vector3I(0, 1, 0),
            Key.Z => new Vector3I(0, -1, 0),
            _ => default,
        };

        if (delta == Vector3I.Zero)
        {
            return false;
        }

        Vector3I target = _activeVoxel + delta;
        if (_grid.InBounds(target.X, target.Y, target.Z))
        {
            if (_grid.GetSafe(target.X, target.Y, target.Z) == 0)
            {
                List<VoxelChange> changes = new()
                {
                    new VoxelChange(target, 0, 1),
                };

                ApplyChanges(changes, applyAfter: true);
                PushUndo(changes);
                AddToSelection(target);
                RebuildMesh();
            }
        }

        return true;
    }

    private void RemoveSelectedVoxels()
    {
        if (_grid == null)
        {
            return;
        }

        if (_selectedVoxels.Count == 0)
        {
            return;
        }

        List<VoxelChange> changes = new();
        foreach (Vector3I voxel in _selectedVoxels)
        {
            byte current = _grid.GetSafe(voxel.X, voxel.Y, voxel.Z);
            if (current == 0)
            {
                continue;
            }

            changes.Add(new VoxelChange(voxel, current, 0));
        }

        if (changes.Count == 0)
        {
            return;
        }

        ApplyChanges(changes, applyAfter: true);
        PushUndo(changes);
        ClearSelection();
        RebuildMesh();
    }

    private void PushUndo(List<VoxelChange> changes)
    {
        _undoStack.Push(changes);
        _redoStack.Clear();
    }

    private void UndoLast()
    {
        if (_grid == null || _undoStack.Count == 0)
        {
            return;
        }

        List<VoxelChange> changes = _undoStack.Pop();
        ApplyChanges(changes, applyAfter: false);
        _redoStack.Push(changes);
        ClearSelection();
        RebuildMesh();
    }

    private void RedoLast()
    {
        if (_grid == null || _redoStack.Count == 0)
        {
            return;
        }

        List<VoxelChange> changes = _redoStack.Pop();
        ApplyChanges(changes, applyAfter: true);
        _undoStack.Push(changes);
        ClearSelection();
        RebuildMesh();
    }

    private void ApplyChanges(List<VoxelChange> changes, bool applyAfter)
    {
        if (_grid == null)
        {
            return;
        }

        foreach (VoxelChange change in changes)
        {
            byte value = applyAfter ? change.After : change.Before;
            _grid.Set(change.Position.X, change.Position.Y, change.Position.Z, value);
        }
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

    private static VxmData LoadVoxelData(string path)
    {
        if (IsVoxFile(path))
        {
            return VoxCodecOptional.Load(path);
        }

        return VxmCodec.Load(path);
    }

    private static bool IsVoxFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".vox", StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct VoxelChange
    {
        public Vector3I Position { get; }
        public byte Before { get; }
        public byte After { get; }

        public VoxelChange(Vector3I position, byte before, byte after)
        {
            Position = position;
            Before = before;
            After = after;
        }
    }
}
