# Generator Specification

## Overview

The generator is a Godot 4 C# project intended to run in headless mode. It creates a voxel model from a seed, writes it to disk, prints a summary, and exits.

## Headless Execution

- Use `--headless` with the Godot editor binary.
- Provide a CLI entrypoint that parses arguments.
- Exit code `0` on success.

## CLI Arguments

Required:

- `--seed <int>`
- `--out <path>`

Optional:

- `--height <int>`
- `--style <chunky|slender>`
- `--pose <A|T>`

## Output

- `.vxm` file saved to `--out`.
- Optional metadata JSON saved inside the `.vxm` or as a sidecar.

## MVP Behavior

- Create a voxel grid.
- Generate a torso box.
- Save `.vxm` and exit.

## Part Generation Responsibilities

- Compute part dimensions from height and style.
- Use primitives to rasterize into the voxel grid.
- Record attachment points.
- Enforce collision and connectivity rules.
