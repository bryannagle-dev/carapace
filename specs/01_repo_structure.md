# Repository Structure

## Layout

```
voxel-humanoid/
README.md
.gitignore
tools/
scripts/
get_godot.ps1
get_godot.sh
formats/
vox.md
vxm.md
src/
  VoxelCore/
    VoxelGrid.cs
    Primitives.cs
    Rules.cs
    Meshing/
      GreedyMesher.cs
    IO/
      VxmCodec.cs
      VoxCodec_optional.cs
  Generator/
    project.godot
    Main.tscn
    Main.cs
    presets.cfg
  Viewer/
    project.godot
    Scenes/
      ViewerMain.tscn
    Scripts/
      ViewerMain.cs
    UI/
```

## Rationale

- Two Godot projects keep generator runtime minimal and viewer features flexible.
- A shared `VoxelCore` library avoids duplicate voxel logic.
- `formats/` contains format documentation.
- `tools/` and `scripts/` host helper scripts for downloading Godot and automation.

## Shared Code Strategy

Choose one of these options:

- Copy `VoxelCore` into both Godot projects and keep it identical.
- Use a shared folder referenced by both projects.
- Use a Godot addon-style module if that fits the project.
