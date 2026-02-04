# Procedural Voxel Humanoid Generator + Viewer

This repo contains a deterministic procedural voxel humanoid generator and a separate viewer, both built in Godot 4 with C#.

## Projects

- `src/Generator` Headless generator project that writes `.vxm` files.
- `src/Viewer` Interactive viewer that loads `.vxm` files and renders them.
- `src/VoxelCore` Shared voxel logic, file formats, and meshing.

Note: `src/Generator/VoxelCore` and `src/Viewer/VoxelCore` are symlinks to the shared `src/VoxelCore`. If your environment does not support symlinks, copy the folder into each project instead.

## Requirements

- Godot 4 .NET (C#) editor build
- .NET SDK compatible with the chosen Godot 4 C# version

## Download Godot

Use the scripts at the repo root:

- `./get_godot.sh`
- `./get_godot.ps1`

These download the Godot .NET editor and export templates into `tools/godot/`.

## Quick Demo

```
./scripts/run_demo.sh
```

This runs the headless generator and then launches the viewer with the output file.

Windows:

```
./scripts/run_demo.ps1
```

Viewer-only (Windows, double-clickable):

- `scripts/run_viewer_windows.bat`
- `scripts/run_viewer_windows.ps1`

## Generator CLI

Example:

```
Godot_v4.6-stable_mono_linux_x86_64/Godot_v4.6-stable_mono_linux.x86_64 \
  --headless --path src/Generator -- \
  --seed 123 --out ../../output/torso_only.vxm --height 48 --torso_voxels 1000 --style chunky
```

Arguments:

- `--seed <int>`
- `--out <path>`
- `--height <int>` optional
- `--torso_voxels <int>` optional
- `--style <chunky|slender>` optional

## Viewer CLI

Example:

```
Godot_v4.6-stable_mono_linux_x86_64/Godot_v4.6-stable_mono_linux.x86_64 \
  --path src/Viewer -- \
  --file ../../output/torso_only.vxm
```

Controls:
- RMB drag to rotate (orbit)
- Mouse wheel to zoom
- WASD to move the camera target on the ground plane
- F3 to save a screenshot to `screenshots/`

## Specs

See `specs/00_readme.md` for the full specification set.
