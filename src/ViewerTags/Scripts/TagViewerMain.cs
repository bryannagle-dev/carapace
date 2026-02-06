using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using VoxelCore;
using VoxelCore.Generation;
using VoxelCore.IO;
using VoxelCore.Meshing;

public partial class TagViewerMain : Node3D
{
    private const int MenuOpenVxm = 1;
    private const int MenuImportVox = 2;
    private const int MenuSaveVxm = 3;
    private const int MenuExportVox = 4;
    private const int MenuGenerateHumanoid = 10;
    private const int MenuGenerateTable = 11;
    private const int MenuGenerateWallTorch = 12;
    private const int MenuGenerateTreasureChest = 13;
    private const int MenuGenerateIronBarWall = 14;
    private const int MenuGenerateTree = 15;

    private Label? _fileLabel;
    private Label? _selectionLabel;
    private Label? _tagsLabel;
    private Label? _generateTitle;
    private LineEdit? _tagName;
    private OptionButton? _direction;
    private MenuButton? _generateMenu;
    private Button? _applyTag;
    private Button? _removeTag;
    private Button? _clearSelection;
    private LineEdit? _multiplierInput;
    private Button? _applyMultiplier;
    private Button? _resetDefaults;
    private FileDialog? _openDialog;
    private FileDialog? _saveDialog;
    private AcceptDialog? _generateDialog;
    private GridContainer? _paramsGrid;
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
    private readonly Dictionary<string, LineEdit> _paramInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _paramDefaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastParams = new(StringComparer.OrdinalIgnoreCase);
    private GenerateKind _pendingGenerate = GenerateKind.None;

    private PendingDialog _pendingDialog = PendingDialog.None;

    public override void _Ready()
    {
        _fileLabel = GetNodeOrNull<Label>("CanvasLayer/UIRoot/TopBar/LabelFile");
        _selectionLabel = GetNodeOrNull<Label>("CanvasLayer/UIRoot/TagPanel/TagVBox/LabelSelection");
        _tagsLabel = GetNodeOrNull<Label>("CanvasLayer/UIRoot/TagPanel/TagVBox/LabelTags");
        _generateTitle = GetNodeOrNull<Label>("CanvasLayer/UIRoot/GenerateDialog/GenerateVBox/GenerateTitle");
        _tagName = GetNodeOrNull<LineEdit>("CanvasLayer/UIRoot/TagPanel/TagVBox/TagName");
        _direction = GetNodeOrNull<OptionButton>("CanvasLayer/UIRoot/TagPanel/TagVBox/Direction");
        _generateMenu = GetNodeOrNull<MenuButton>("CanvasLayer/UIRoot/TopBar/GenerateMenu");
        _applyTag = GetNodeOrNull<Button>("CanvasLayer/UIRoot/TagPanel/TagVBox/ApplyTag");
        _removeTag = GetNodeOrNull<Button>("CanvasLayer/UIRoot/TagPanel/TagVBox/RemoveTag");
        _clearSelection = GetNodeOrNull<Button>("CanvasLayer/UIRoot/TagPanel/TagVBox/ClearSelection");
        _multiplierInput = GetNodeOrNull<LineEdit>("CanvasLayer/UIRoot/GenerateDialog/GenerateVBox/MultiplierRow/MultiplierValue");
        _applyMultiplier = GetNodeOrNull<Button>("CanvasLayer/UIRoot/GenerateDialog/GenerateVBox/MultiplierRow/ApplyMultiplier");
        _resetDefaults = GetNodeOrNull<Button>("CanvasLayer/UIRoot/GenerateDialog/GenerateVBox/ResetDefaults");
        _openDialog = GetNodeOrNull<FileDialog>("CanvasLayer/UIRoot/OpenDialog");
        _saveDialog = GetNodeOrNull<FileDialog>("CanvasLayer/UIRoot/SaveDialog");
        _generateDialog = GetNodeOrNull<AcceptDialog>("CanvasLayer/UIRoot/GenerateDialog");
        _paramsGrid = GetNodeOrNull<GridContainer>("CanvasLayer/UIRoot/GenerateDialog/GenerateVBox/ParamsGrid");
        _meshTarget = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        _cameraRig = GetNodeOrNull<OrbitCamera>("CameraRig");

        ConfigureFileMenu();
        ConfigureGenerateMenu();
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

    private void ConfigureGenerateMenu()
    {
        if (_generateMenu == null)
        {
            return;
        }

        PopupMenu popup = _generateMenu.GetPopup();
        popup.Clear();
        popup.AddItem("Humanoid", MenuGenerateHumanoid);
        popup.AddItem("Table", MenuGenerateTable);
        popup.AddItem("Wall Torch", MenuGenerateWallTorch);
        popup.AddItem("Treasure Chest", MenuGenerateTreasureChest);
        popup.AddItem("Iron Bar Wall", MenuGenerateIronBarWall);
        popup.AddItem("Tree", MenuGenerateTree);
        popup.IdPressed += OnGenerateMenuPressed;
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
        _direction.AddItem("Forward", 4);
        _direction.AddItem("Back", 5);
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

        if (_generateDialog != null)
        {
            _generateDialog.Confirmed += OnGenerateConfirmed;
        }

        if (_applyMultiplier != null)
        {
            _applyMultiplier.Pressed += ApplyMultiplier;
        }

        if (_resetDefaults != null)
        {
            _resetDefaults.Pressed += ResetDefaults;
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

    private void OnGenerateMenuPressed(long id)
    {
        switch (id)
        {
            case MenuGenerateHumanoid:
                ShowGenerateDialog(GenerateKind.Humanoid);
                break;
            case MenuGenerateTable:
                ShowGenerateDialog(GenerateKind.Table);
                break;
            case MenuGenerateWallTorch:
                ShowGenerateDialog(GenerateKind.WallTorch);
                break;
            case MenuGenerateTreasureChest:
                ShowGenerateDialog(GenerateKind.TreasureChest);
                break;
            case MenuGenerateIronBarWall:
                ShowGenerateDialog(GenerateKind.IronBarWall);
                break;
            case MenuGenerateTree:
                ShowGenerateDialog(GenerateKind.Tree);
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

    private void OnGenerateConfirmed()
    {
        if (_grid == null && _pendingGenerate == GenerateKind.None)
        {
            return;
        }

        SaveLastParams();

        switch (_pendingGenerate)
        {
            case GenerateKind.Humanoid:
                GenerateHumanoid();
                break;
            case GenerateKind.Table:
                GenerateTable();
                break;
            case GenerateKind.WallTorch:
                GenerateWallTorch();
                break;
            case GenerateKind.TreasureChest:
                GenerateTreasureChest();
                break;
            case GenerateKind.IronBarWall:
                GenerateIronBarWall();
                break;
            case GenerateKind.Tree:
                GenerateTree();
                break;
        }

        _pendingGenerate = GenerateKind.None;
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

    private void ShowGenerateDialog(GenerateKind kind)
    {
        if (_generateDialog == null || _paramsGrid == null)
        {
            return;
        }

        _pendingGenerate = kind;
        if (_paramInputs.Count > 0)
        {
            SaveLastParams();
        }

        _paramInputs.Clear();
        _paramDefaults.Clear();

        foreach (Node child in _paramsGrid.GetChildren())
        {
            child.QueueFree();
        }

        _generateTitle?.SetText(kind switch
        {
            GenerateKind.Humanoid => "Humanoid Parameters",
            GenerateKind.Table => "Table Parameters",
            GenerateKind.WallTorch => "Wall Torch Parameters",
            GenerateKind.TreasureChest => "Treasure Chest Parameters",
            GenerateKind.IronBarWall => "Iron Bar Wall Parameters",
            GenerateKind.Tree => "Tree Parameters",
            _ => "Parameters",
        });
        _generateDialog.OkButtonText = "Run";

        IEnumerable<GenerateParam> parameters = kind switch
        {
            GenerateKind.Humanoid => new[]
            {
                new GenerateParam("seed", "123"),
                new GenerateParam("height", "48"),
                new GenerateParam("torso_voxels", "1000"),
            },
            GenerateKind.Table => new[]
            {
                new GenerateParam("width", "16"),
                new GenerateParam("depth", "10"),
                new GenerateParam("height", "10"),
                new GenerateParam("leg_thickness", "2"),
                new GenerateParam("top_thickness", "2"),
            },
            GenerateKind.WallTorch => new[]
            {
                new GenerateParam("plate_width", "21"),
                new GenerateParam("plate_height", "30"),
                new GenerateParam("plate_thickness", "3"),
                new GenerateParam("bracket_length", "12"),
                new GenerateParam("torch_radius", "6"),
                new GenerateParam("torch_length", "12"),
                new GenerateParam("flame_height", "12"),
                new GenerateParam("flame_enabled", "1"),
                new GenerateParam("handle_enabled", "1"),
                new GenerateParam("handle_length", "12"),
                new GenerateParam("handle_radius", "3"),
            },
            GenerateKind.TreasureChest => new[]
            {
                new GenerateParam("width", "12"),
                new GenerateParam("depth", "8"),
                new GenerateParam("height", "6"),
                new GenerateParam("lid_height", "4"),
                new GenerateParam("wall_thickness", "1"),
                new GenerateParam("band_thickness", "1"),
            },
            GenerateKind.IronBarWall => new[]
            {
                new GenerateParam("width", "16"),
                new GenerateParam("height", "12"),
                new GenerateParam("depth", "2"),
                new GenerateParam("frame_thickness", "1"),
                new GenerateParam("bar_radius", "1"),
                new GenerateParam("bar_spacing", "2"),
                new GenerateParam("side_frames_enabled", "1"),
            },
            GenerateKind.Tree => new[]
            {
                new GenerateParam("seed", "123"),
                new GenerateParam("height", "24"),
                new GenerateParam("trunk_radius", "2"),
                new GenerateParam("branch_every", "4"),
                new GenerateParam("branch_length", "6"),
                new GenerateParam("canopy_radius", "6"),
                new GenerateParam("canopy_height", "6"),
                new GenerateParam("canopy_density", "70"),
                new GenerateParam("canopy_spheres", "3"),
                new GenerateParam("root_length", "4"),
            },
            _ => Array.Empty<GenerateParam>(),
        };

        foreach (GenerateParam param in parameters)
        {
            Label label = new() { Text = param.Name };
            string lastValue = GetLastParamValue(kind, param.Name) ?? param.DefaultValue;
            LineEdit input = new() { Text = lastValue };
            _paramsGrid.AddChild(label);
            _paramsGrid.AddChild(input);
            _paramInputs[param.Name] = input;
            _paramDefaults[param.Name] = param.DefaultValue;
        }

        _generateDialog.PopupCenteredRatio(0.4f);
    }

    private void GenerateHumanoid()
    {
        int seed = ReadParamInt("seed", 123);
        int height = ReadParamInt("height", 48);
        int torsoVoxels = ReadParamInt("torso_voxels", 1000);

        VoxelGrid grid = HumanoidGenerator.BuildHumanoid(height, torsoVoxels, seed);
        byte[] palette = BuildGeneratorPalette();
        string metadata = $"{{\"generator\":\"humanoid\",\"seed\":{seed},\"height_vox\":{height},\"torso_voxels\":{torsoVoxels}}}";

        UseGeneratedGrid(grid, palette, metadata);
    }

    private void GenerateTable()
    {
        int width = ReadParamInt("width", 16);
        int depth = ReadParamInt("depth", 10);
        int height = ReadParamInt("height", 10);
        int legThickness = ReadParamInt("leg_thickness", 2);
        int topThickness = ReadParamInt("top_thickness", 2);

        VoxelGrid grid = HumanoidGenerator.BuildTable(width, depth, height, legThickness, topThickness);
        byte[] palette = BuildGeneratorPalette();
        string metadata = $"{{\"generator\":\"table\",\"width\":{width},\"depth\":{depth},\"height\":{height}}}";

        UseGeneratedGrid(grid, palette, metadata);
    }

    private void GenerateWallTorch()
    {
        int plateWidth = ReadParamInt("plate_width", 7);
        int plateHeight = ReadParamInt("plate_height", 10);
        int plateThickness = ReadParamInt("plate_thickness", 1);
        int bracketLength = ReadParamInt("bracket_length", 4);
        int torchRadius = ReadParamInt("torch_radius", 2);
        int torchLength = ReadParamInt("torch_length", 4);
        int flameHeight = ReadParamInt("flame_height", 4);
        int flameEnabled = ReadParamInt("flame_enabled", 1);
        int handleEnabled = ReadParamInt("handle_enabled", 1);
        int handleLength = ReadParamInt("handle_length", 4);
        int handleRadius = ReadParamInt("handle_radius", 1);

        VoxelGrid grid = HumanoidGenerator.BuildWallTorch(
            plateWidth,
            plateHeight,
            plateThickness,
            bracketLength,
            torchRadius,
            torchLength,
            flameHeight,
            flameEnabled != 0,
            handleEnabled != 0,
            handleLength,
            handleRadius);
        byte[] palette = BuildGeneratorPalette();
        string metadata = $"{{\"generator\":\"wall_torch\",\"plate_width\":{plateWidth},\"plate_height\":{plateHeight},\"bracket_length\":{bracketLength}}}";

        UseGeneratedGrid(grid, palette, metadata);
        ApplyGeneratedTags(BuildAttachTag(grid, "attach", "back"), BuildTorchEmitterTag(grid, "emitter", "up"));
    }

    private void GenerateTreasureChest()
    {
        int width = ReadParamInt("width", 12);
        int depth = ReadParamInt("depth", 8);
        int height = ReadParamInt("height", 6);
        int lidHeight = ReadParamInt("lid_height", 4);
        int wallThickness = ReadParamInt("wall_thickness", 1);
        int bandThickness = ReadParamInt("band_thickness", 1);

        VoxelGrid grid = HumanoidGenerator.BuildTreasureChest(width, depth, height, lidHeight, wallThickness, bandThickness);
        byte[] palette = BuildGeneratorPalette();
        string metadata = $"{{\"generator\":\"treasure_chest\",\"width\":{width},\"depth\":{depth},\"height\":{height}}}";

        UseGeneratedGrid(grid, palette, metadata);
        ApplyGeneratedTags(BuildChestPivotTag(grid, "pivot", "up"));
    }

    private void GenerateIronBarWall()
    {
        int width = ReadParamInt("width", 16);
        int height = ReadParamInt("height", 12);
        int depth = ReadParamInt("depth", 2);
        int frameThickness = ReadParamInt("frame_thickness", 1);
        int barRadius = ReadParamInt("bar_radius", ReadParamInt("bar_thickness", 1));
        int barSpacing = ReadParamInt("bar_spacing", 2);
        int sideFramesEnabled = ReadParamInt("side_frames_enabled", 1);

        VoxelGrid grid = HumanoidGenerator.BuildIronBarWall(
            width,
            height,
            depth,
            frameThickness,
            barRadius,
            barSpacing,
            sideFramesEnabled != 0);
        byte[] palette = BuildGeneratorPalette();
        string metadata = $"{{\"generator\":\"iron_bar_wall\",\"width\":{width},\"height\":{height},\"depth\":{depth}}}";

        UseGeneratedGrid(grid, palette, metadata);
    }

    private void GenerateTree()
    {
        int seed = ReadParamInt("seed", 123);
        int height = ReadParamInt("height", 24);
        int trunkRadius = ReadParamInt("trunk_radius", 2);
        int branchEvery = ReadParamInt("branch_every", 4);
        int branchLength = ReadParamInt("branch_length", 6);
        int canopyRadius = ReadParamInt("canopy_radius", 6);
        int canopyHeight = ReadParamInt("canopy_height", 6);
        int canopyDensity = ReadParamInt("canopy_density", 70);
        int canopySpheres = ReadParamInt("canopy_spheres", 3);
        int rootLength = ReadParamInt("root_length", 4);

        VoxelGrid grid = HumanoidGenerator.BuildTree(height, trunkRadius, branchEvery, branchLength, canopyRadius, canopyHeight, canopyDensity, canopySpheres, rootLength, seed);
        byte[] palette = BuildGeneratorPalette();
        string metadata = $"{{\"generator\":\"tree\",\"seed\":{seed},\"height\":{height}}}";

        UseGeneratedGrid(grid, palette, metadata);
    }

    private int ReadParamInt(string key, int fallback)
    {
        if (!_paramInputs.TryGetValue(key, out LineEdit? input) || input == null)
        {
            return fallback;
        }

        return int.TryParse(input.Text, out int value) ? value : fallback;
    }

    private void UseGeneratedGrid(VoxelGrid grid, byte[] palette, string metadata)
    {
        _grid = grid;
        _palette = palette;
        _metadataJson = metadata;
        _currentPath = string.Empty;
        _selectedVoxels.Clear();
        _tags.Clear();
        UpdateSelectionMesh();

        RebuildMesh();
        FocusCamera();
        UpdateUi();
    }

    private void ApplyMultiplier()
    {
        if (_multiplierInput == null)
        {
            return;
        }

        if (!float.TryParse(_multiplierInput.Text, out float factor))
        {
            return;
        }

        if (Math.Abs(factor) < 0.001f)
        {
            return;
        }

        foreach ((string key, LineEdit input) in _paramInputs)
        {
            if (key.Equals("seed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (key.EndsWith("_enabled", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!float.TryParse(input.Text, out float value))
            {
                continue;
            }

            int scaled = (int)MathF.Round(value * factor);
            input.Text = scaled.ToString();
        }
    }

    private void ResetDefaults()
    {
        foreach ((string key, LineEdit input) in _paramInputs)
        {
            if (_paramDefaults.TryGetValue(key, out string? value))
            {
                input.Text = value;
            }
        }
    }

    private void SaveLastParams()
    {
        if (_pendingGenerate == GenerateKind.None)
        {
            return;
        }

        foreach ((string key, LineEdit input) in _paramInputs)
        {
            _lastParams[BuildParamKey(_pendingGenerate, key)] = input.Text;
        }
    }

    private string? GetLastParamValue(GenerateKind kind, string key)
    {
        return _lastParams.TryGetValue(BuildParamKey(kind, key), out string? value) ? value : null;
    }

    private static string BuildParamKey(GenerateKind kind, string key)
    {
        return $"{kind}:{key}".ToLowerInvariant();
    }

    private void ApplyGeneratedTags(params TagGroupRuntime?[] tags)
    {
        _tags.Clear();
        foreach (TagGroupRuntime? tag in tags)
        {
            if (tag == null)
            {
                continue;
            }

            _tags[tag.Name] = tag;
        }
        UpdateUi();
    }

    private TagGroupRuntime? BuildAttachTag(VoxelGrid grid, string name, string direction)
    {
        if (!TryComputeBounds(grid, out Vector3 min, out Vector3 max))
        {
            return null;
        }

        int centerX = (int)MathF.Round((min.X + max.X - 1) * 0.5f);
        int centerY = (int)MathF.Round((min.Y + max.Y - 1) * 0.5f);
        int z = (int)MathF.Floor(min.Z);

        Vector3I voxel = new(centerX, centerY, z);
        if (grid.GetSafe(voxel.X, voxel.Y, voxel.Z) == 0)
        {
            voxel = FindNearestFilled(grid, voxel);
        }

        TagGroupRuntime tag = new(name, direction);
        tag.Voxels.Add(voxel);
        return tag;
    }

    private TagGroupRuntime? BuildChestPivotTag(VoxelGrid grid, string name, string direction)
    {
        if (!TryComputeBounds(grid, out Vector3 min, out Vector3 max))
        {
            return null;
        }

        int centerX = (int)MathF.Round((min.X + max.X - 1) * 0.5f);
        int y = (int)MathF.Floor(max.Y) - 1;
        int z = (int)MathF.Floor(max.Z) - 1;

        TagGroupRuntime tag = new(name, direction);
        for (int dx = -1; dx <= 1; dx++)
        {
            Vector3I voxel = new(centerX + dx, y, z);
            if (grid.GetSafe(voxel.X, voxel.Y, voxel.Z) == 0)
            {
                voxel = FindNearestFilled(grid, voxel);
            }
            tag.Voxels.Add(voxel);
        }

        return tag;
    }

    private TagGroupRuntime? BuildTorchEmitterTag(VoxelGrid grid, string name, string direction)
    {
        if (!TryComputeBounds(grid, out Vector3 min, out Vector3 max))
        {
            return null;
        }

        int minX = (int)MathF.Floor(min.X);
        int maxX = (int)MathF.Ceiling(max.X);
        int minZ = (int)MathF.Floor(min.Z);
        int maxZ = (int)MathF.Ceiling(max.Z);
        int topY = (int)MathF.Ceiling(max.Y) - 1;

        TagGroupRuntime tag = new(name, direction);
        for (int z = minZ; z < maxZ; z++)
        {
            for (int x = minX; x < maxX; x++)
            {
                if (grid.GetSafe(x, topY, z) == 4)
                {
                    tag.Voxels.Add(new Vector3I(x, topY, z));
                }
            }
        }

        if (tag.Voxels.Count == 0)
        {
            return null;
        }

        return tag;
    }

    private Vector3I FindNearestFilled(VoxelGrid grid, Vector3I origin)
    {
        int bestDist = int.MaxValue;
        Vector3I best = origin;

        for (int z = 0; z < grid.SizeZ; z++)
        {
            for (int y = 0; y < grid.SizeY; y++)
            {
                for (int x = 0; x < grid.SizeX; x++)
                {
                    if (grid.GetSafe(x, y, z) == 0)
                    {
                        continue;
                    }

                    int dx = x - origin.X;
                    int dy = y - origin.Y;
                    int dz = z - origin.Z;
                    int dist = dx * dx + dy * dy + dz * dz;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = new Vector3I(x, y, z);
                    }
                }
            }
        }

        return best;
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
            if (string.IsNullOrWhiteSpace(_currentPath))
            {
                _fileLabel.Text = _grid == null ? "No file loaded" : "Generated (unsaved)";
            }
            else
            {
                _fileLabel.Text = Path.GetFileName(_currentPath);
            }
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
            4 => "forward",
            5 => "back",
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

    private static byte[] BuildGeneratorPalette()
    {
        byte[] palette = new byte[256 * 4];
        for (int i = 0; i < 256; i++)
        {
            int offset = i * 4;
            if (i == 0)
            {
                palette[offset] = 0;
                palette[offset + 1] = 0;
                palette[offset + 2] = 0;
                palette[offset + 3] = 0;
            }
            else
            {
                palette[offset] = 230;
                palette[offset + 1] = 230;
                palette[offset + 2] = 230;
                palette[offset + 3] = 255;
            }
        }

        // 2: metal
        palette[2 * 4] = 160;
        palette[2 * 4 + 1] = 160;
        palette[2 * 4 + 2] = 170;
        palette[2 * 4 + 3] = 255;

        // 3: gold
        palette[3 * 4] = 210;
        palette[3 * 4 + 1] = 180;
        palette[3 * 4 + 2] = 60;
        palette[3 * 4 + 3] = 255;

        // 4: wood brown
        palette[4 * 4] = 110;
        palette[4 * 4 + 1] = 70;
        palette[4 * 4 + 2] = 40;
        palette[4 * 4 + 3] = 255;

        // 5: leaf green
        palette[5 * 4] = 50;
        palette[5 * 4 + 1] = 140;
        palette[5 * 4 + 2] = 50;
        palette[5 * 4 + 3] = 255;

        // 6: dark wood stripe
        palette[6 * 4] = 90;
        palette[6 * 4 + 1] = 55;
        palette[6 * 4 + 2] = 30;
        palette[6 * 4 + 3] = 255;

        return palette;
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

    private readonly record struct GenerateParam(string Name, string DefaultValue);

    private enum GenerateKind
    {
        None,
        Humanoid,
        Table,
        WallTorch,
        TreasureChest,
        IronBarWall,
        Tree,
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
