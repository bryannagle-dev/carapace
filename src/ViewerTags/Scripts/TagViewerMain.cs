using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using VoxelCore;
using VoxelCore.IO;
using VoxelCore.Meshing;

public partial class TagViewerMain : Node3D
{
    private const int MenuOpenVxm = 1;
    private const int MenuImportVox = 2;
    private const int MenuSaveVxm = 3;
    private const int MenuExportVox = 4;

    private Label? _fileLabel;
    private Label? _selectionLabel;
    private Label? _tagsLabel;
    private LineEdit? _tagName;
    private OptionButton? _direction;
    private Button? _applyTag;
    private Button? _removeTag;
    private Button? _clearSelection;
    private FileDialog? _openDialog;
    private FileDialog? _saveDialog;
    private MeshInstance3D? _meshTarget;
    private OrbitCamera? _cameraRig;
    private StaticBody3D? _pickBody;
    private CollisionShape3D? _pickShape;
    private MultiMeshInstance3D? _selectionMesh;

    private VoxelGrid? _grid;
    private byte[]? _palette;
    private string _metadataJson = "{}";
    private string _currentPath = string.Empty;

    private readonly HashSet<Vector3I> _selectedVoxels = new();
    private readonly Dictionary<string, TagGroupRuntime> _tags = new(StringComparer.OrdinalIgnoreCase);

    private PendingDialog _pendingDialog = PendingDialog.None;

    public override void _Ready()
    {
        _fileLabel = GetNodeOrNull<Label>("CanvasLayer/UIRoot/TopBar/LabelFile");
        _selectionLabel = GetNodeOrNull<Label>("CanvasLayer/UIRoot/TagPanel/TagVBox/LabelSelection");
        _tagsLabel = GetNodeOrNull<Label>("CanvasLayer/UIRoot/TagPanel/TagVBox/LabelTags");
        _tagName = GetNodeOrNull<LineEdit>("CanvasLayer/UIRoot/TagPanel/TagVBox/TagName");
        _direction = GetNodeOrNull<OptionButton>("CanvasLayer/UIRoot/TagPanel/TagVBox/Direction");
        _applyTag = GetNodeOrNull<Button>("CanvasLayer/UIRoot/TagPanel/TagVBox/ApplyTag");
        _removeTag = GetNodeOrNull<Button>("CanvasLayer/UIRoot/TagPanel/TagVBox/RemoveTag");
        _clearSelection = GetNodeOrNull<Button>("CanvasLayer/UIRoot/TagPanel/TagVBox/ClearSelection");
        _openDialog = GetNodeOrNull<FileDialog>("CanvasLayer/UIRoot/OpenDialog");
        _saveDialog = GetNodeOrNull<FileDialog>("CanvasLayer/UIRoot/SaveDialog");
        _meshTarget = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        _cameraRig = GetNodeOrNull<OrbitCamera>("CameraRig");

        ConfigureFileMenu();
        ConfigureDirectionOptions();
        HookUiSignals();
        EnsureSelectionMesh();

        string[] args = OS.GetCmdlineUserArgs();
        string? filePath = GetArgValue(args, "--file");
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            LoadFile(filePath);
        }

        UpdateUi();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left || mouseButton.ButtonIndex == MouseButton.Right)
            {
                if (TrySelectVoxel(mouseButton))
                {
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    private void ConfigureFileMenu()
    {
        MenuButton? fileMenu = GetNodeOrNull<MenuButton>("CanvasLayer/UIRoot/TopBar/FileMenu");
        if (fileMenu == null)
        {
            return;
        }

        PopupMenu popup = fileMenu.GetPopup();
        popup.Clear();
        popup.AddItem("Open VXM...", MenuOpenVxm);
        popup.AddItem("Import VOX...", MenuImportVox);
        popup.AddSeparator();
        popup.AddItem("Save VXM", MenuSaveVxm);
        popup.AddItem("Export VOX...", MenuExportVox);
        popup.IdPressed += OnFileMenuPressed;
    }

    private void ConfigureDirectionOptions()
    {
        if (_direction == null)
        {
            return;
        }

        _direction.Clear();
        _direction.AddItem("Up", 0);
        _direction.AddItem("Down", 1);
        _direction.AddItem("Left", 2);
        _direction.AddItem("Right", 3);
        _direction.Selected = 0;
    }

    private void HookUiSignals()
    {
        if (_applyTag != null)
        {
            _applyTag.Pressed += ApplyTagToSelection;
        }

        if (_removeTag != null)
        {
            _removeTag.Pressed += RemoveTagFromSelection;
        }

        if (_clearSelection != null)
        {
            _clearSelection.Pressed += ClearSelection;
        }

        if (_openDialog != null)
        {
            _openDialog.FileSelected += OnOpenFileSelected;
        }

        if (_saveDialog != null)
        {
            _saveDialog.FileSelected += OnSaveFileSelected;
        }
    }

    private void OnFileMenuPressed(long id)
    {
        switch (id)
        {
            case MenuOpenVxm:
                OpenDialogFor(PendingDialog.OpenVxm, "*.vxm");
                break;
            case MenuImportVox:
                OpenDialogFor(PendingDialog.ImportVox, "*.vox");
                break;
            case MenuSaveVxm:
                SaveCurrentVxm();
                break;
            case MenuExportVox:
                OpenSaveDialogFor(PendingDialog.ExportVox, "*.vox");
                break;
        }
    }

    private void OpenDialogFor(PendingDialog mode, string filter)
    {
        if (_openDialog == null)
        {
            return;
        }

        _pendingDialog = mode;
        _openDialog.Filters = new[] { filter };
        _openDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _openDialog.PopupCenteredRatio();
    }

    private void OpenSaveDialogFor(PendingDialog mode, string filter)
    {
        if (_saveDialog == null)
        {
            return;
        }

        _pendingDialog = mode;
        _saveDialog.Filters = new[] { filter };
        _saveDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
        _saveDialog.PopupCenteredRatio();
    }

    private void OnOpenFileSelected(string path)
    {
        if (_pendingDialog == PendingDialog.OpenVxm)
        {
            LoadFile(path);
        }
        else if (_pendingDialog == PendingDialog.ImportVox)
        {
            ImportVox(path);
        }

        _pendingDialog = PendingDialog.None;
    }

    private void OnSaveFileSelected(string path)
    {
        if (_pendingDialog == PendingDialog.ExportVox)
        {
            ExportVox(path);
        }
        else if (_pendingDialog == PendingDialog.SaveVxm)
        {
            SaveVxmAs(path);
        }

        _pendingDialog = PendingDialog.None;
    }

    private void LoadFile(string path)
    {
        VxmData data = IsVoxFile(path) ? VoxCodecOptional.Load(path) : VxmCodec.Load(path);
        _grid = data.Grid;
        _palette = data.PaletteRgba;
        _metadataJson = data.MetadataJson;
        _currentPath = path;
        _selectedVoxels.Clear();
        LoadTagsFromMetadata();

        RebuildMesh();
        FocusCamera();
        UpdateUi();
    }

    private void ImportVox(string path)
    {
        VxmData data = VoxCodecOptional.Load(path);
        string targetPath = Path.ChangeExtension(path, ".vxm");
        VxmCodec.Save(data.Grid, targetPath, data.PaletteRgba, data.MetadataJson);
        LoadFile(targetPath);
    }

    private void SaveCurrentVxm()
    {
        if (_grid == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentPath) || IsVoxFile(_currentPath))
        {
            OpenSaveDialogFor(PendingDialog.SaveVxm, "*.vxm");
            return;
        }

        PersistMetadataWithTags();
        VxmCodec.Save(_grid, _currentPath, _palette, _metadataJson);
        UpdateUi();
    }

    private void SaveVxmAs(string path)
    {
        if (_grid == null)
        {
            return;
        }

        string savePath = EnsureExtension(path, ".vxm");
        PersistMetadataWithTags();
        VxmCodec.Save(_grid, savePath, _palette, _metadataJson);
        _currentPath = savePath;
        UpdateUi();
    }

    private void ExportVox(string path)
    {
        if (_grid == null)
        {
            return;
        }

        string exportPath = EnsureExtension(path, ".vox");
        VoxCodecOptional.Save(_grid, exportPath, _palette);
    }

    private void FocusCamera()
    {
        if (_cameraRig == null || _grid == null)
        {
            return;
        }

        if (!TryComputeBounds(_grid, out Vector3 min, out Vector3 max))
        {
            Vector3 centerFallback = new(_grid.SizeX / 2f, _grid.SizeY / 2f, _grid.SizeZ / 2f);
            _cameraRig.Focus(centerFallback, Mathf.Max(_grid.SizeX, Mathf.Max(_grid.SizeY, _grid.SizeZ)) * 0.5f);
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

    private bool TrySelectVoxel(InputEventMouseButton mouseButton)
    {
        if (_grid == null)
        {
            return false;
        }

        if (!TryGetVoxelFromClick(out Vector3I voxel))
        {
            return false;
        }

        if (_grid.GetSafe(voxel.X, voxel.Y, voxel.Z) == 0)
        {
            return false;
        }

        bool shift = mouseButton.ShiftPressed;
        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (!shift)
            {
                _selectedVoxels.Clear();
            }

            _selectedVoxels.Add(voxel);
            UpdateSelectionMesh();
            UpdateUi();
            return true;
        }

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            _selectedVoxels.Remove(voxel);
            UpdateSelectionMesh();
            UpdateUi();
            return true;
        }

        return false;
    }

    private bool TryGetVoxelFromClick(out Vector3I voxel)
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
        Vector3 offset = localNormal * -0.01f;
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

    private void ApplyTagToSelection()
    {
        if (_selectedVoxels.Count == 0)
        {
            return;
        }

        string tagName = _tagName?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        string direction = GetSelectedDirection();

        if (!_tags.TryGetValue(tagName, out TagGroupRuntime? group) || group == null)
        {
            group = new TagGroupRuntime(tagName, direction);
            _tags[tagName] = group;
        }

        group.Direction = direction;
        foreach (Vector3I voxel in _selectedVoxels)
        {
            group.Voxels.Add(voxel);
        }

        UpdateUi();
    }

    private void RemoveTagFromSelection()
    {
        if (_selectedVoxels.Count == 0)
        {
            return;
        }

        string tagName = _tagName?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        if (!_tags.TryGetValue(tagName, out TagGroupRuntime? group) || group == null)
        {
            return;
        }

        foreach (Vector3I voxel in _selectedVoxels)
        {
            group.Voxels.Remove(voxel);
        }

        if (group.Voxels.Count == 0)
        {
            _tags.Remove(tagName);
        }

        UpdateUi();
    }

    private void ClearSelection()
    {
        _selectedVoxels.Clear();
        UpdateSelectionMesh();
        UpdateUi();
    }

    private void UpdateUi()
    {
        _selectionLabel?.SetText($"Selected: {_selectedVoxels.Count}");
        _tagsLabel?.SetText($"Tags: {_tags.Count}");

        if (_fileLabel != null)
        {
            _fileLabel.Text = string.IsNullOrWhiteSpace(_currentPath) ? "No file loaded" : Path.GetFileName(_currentPath);
        }
    }

    private void LoadTagsFromMetadata()
    {
        _tags.Clear();

        if (string.IsNullOrWhiteSpace(_metadataJson))
        {
            return;
        }

        JsonNode? root = JsonNode.Parse(_metadataJson);
        if (root == null)
        {
            return;
        }

        JsonArray? tagsArray = root["tags"] as JsonArray;
        if (tagsArray == null)
        {
            return;
        }

        foreach (JsonNode? node in tagsArray)
        {
            if (node == null)
            {
                continue;
            }

            TagGroup? tag = node.Deserialize<TagGroup>();
            if (tag == null || string.IsNullOrWhiteSpace(tag.Name))
            {
                continue;
            }

            TagGroupRuntime runtime = new(tag.Name, tag.Direction ?? "up");
            foreach (VoxelCoord coord in tag.Voxels ?? new List<VoxelCoord>())
            {
                runtime.Voxels.Add(new Vector3I(coord.X, coord.Y, coord.Z));
            }

            _tags[tag.Name] = runtime;
        }
    }

    private void PersistMetadataWithTags()
    {
        JsonNode root = JsonNode.Parse(_metadataJson) ?? new JsonObject();
        JsonArray tagArray = new();

        foreach (TagGroupRuntime group in _tags.Values)
        {
            TagGroup export = new()
            {
                Name = group.Name,
                Direction = group.Direction,
                Voxels = new List<VoxelCoord>(),
            };

            foreach (Vector3I voxel in group.Voxels)
            {
                export.Voxels.Add(new VoxelCoord(voxel.X, voxel.Y, voxel.Z));
            }

            tagArray.Add(JsonSerializer.SerializeToNode(export));
        }

        root["tags"] = tagArray;
        _metadataJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private string GetSelectedDirection()
    {
        if (_direction == null)
        {
            return "up";
        }

        return _direction.Selected switch
        {
            0 => "up",
            1 => "down",
            2 => "left",
            3 => "right",
            _ => "up",
        };
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

    private static bool IsVoxFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".vox", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureExtension(string path, string ext)
    {
        if (Path.GetExtension(path).Equals(ext, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path + ext;
    }

    private sealed class TagGroupRuntime
    {
        public string Name { get; }
        public string Direction { get; set; }
        public HashSet<Vector3I> Voxels { get; } = new();

        public TagGroupRuntime(string name, string direction)
        {
            Name = name;
            Direction = direction;
        }
    }

    private sealed class TagGroup
    {
        public string? Name { get; set; }
        public string? Direction { get; set; }
        public List<VoxelCoord>? Voxels { get; set; }
    }

    private enum PendingDialog
    {
        None,
        OpenVxm,
        ImportVox,
        SaveVxm,
        ExportVox,
    }
}
