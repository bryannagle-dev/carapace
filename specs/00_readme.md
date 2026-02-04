# Project Specs Index

This folder contains the specifications for the Procedural Voxel Humanoid Generator + Viewer project.

## Files

- `01_repo_structure.md` Repository layout and rationale.
- `02_design_overview.md` Goals, principles, and core data model.
- `03_vxm_format.md` `.vxm` binary format specification.
- `04_meshing.md` Greedy meshing algorithm details.
- `05_generator.md` Headless generator behavior and CLI.
- `06_viewer.md` Viewer behavior and UI requirements.
- `07_implementation_steps.md` Step-by-step implementation plan.
- `08_validation_rules.md` Validity rules and regeneration strategy.
- `09_testing.md` Testing strategy and golden tests.
- `10_roadmap_optional.md` Optional `.vox` interoperability phase.

## Scope

These specs define a single repo with two Godot 4 C# projects:

- Generator project for headless model generation and file output.
- Viewer project for interactive 3D display of saved voxel models.

The shared logic lives in a `VoxelCore` library that both projects include.
