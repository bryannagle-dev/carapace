# Design Overview

## Goals

- Procedurally generate humanoid-like voxel models.
- Deterministic output from a seed.
- Structured generation using explicit rules and parameters.
- Robust output with no self-intersections or disconnected parts.
- Practical rendering via mesh generation rather than per-voxel nodes.

## Core Principles

- Determinism: all randomness is seeded.
- Consistency: body parts are generated from explicit parameter sets.
- Validity: connectivity and proportion checks are enforced.
- Performance: dense voxel grids plus greedy meshing.

## Coordinate System

- Y up
- X left and right
- Z forward and back
- Model origin at pelvis center

## Core Data Model

### VoxelGrid

- Dense `byte[]` storage for voxels.
- Dimensions `SizeX`, `SizeY`, `SizeZ`.
- Index order `x + y * SizeX + z * SizeX * SizeY`.

Key methods:

- `Get(x, y, z) -> byte`
- `Set(x, y, z, material)`
- `InBounds(x, y, z)`
- `FillBox(min, max, material)`
- `CarveBox(min, max)`
- `FloodFill` and `ConnectedComponents` for validation

### Part Metadata

Each generated part should carry:

- Name
- Local bounds
- Attachment points
- Parameters used for generation

### Primitives

- Box
- Cylinder along axis or between two points
- Sphere or ellipsoid
- Tapered cylinder for limbs
- Extrude and inset for surface variation

### Constraints and Helpers

- Symmetry operations on X.
- Collision checks with optional override.
- Thickness clamps.
- Keep-out volumes.
- Support rules to ensure connectivity.
