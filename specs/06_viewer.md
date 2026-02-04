# Viewer Specification

## Overview

The viewer is a Godot 4 C# project that loads `.vxm` files, generates a mesh, and displays the model in 3D.

## Features

- Load `.vxm` from CLI argument.
- Load `.vxm` via file picker button.
- Drag-and-drop `.vxm` into the window.
- Build a greedy-meshed `ArrayMesh` and display it.

## Controls

- Orbit camera with right mouse drag.
- Mouse wheel zoom.

## Lighting

- Single directional light.
- Minimal ambient light.

## Debug UI

- Dimensions and voxel count.
- Seed and generator parameters.
- Toggle part bounds display.
- Toggle material colors.
