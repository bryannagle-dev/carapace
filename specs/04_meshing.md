# Greedy Meshing Specification

## Goal

Generate a minimal quad mesh from voxel occupancy to render efficiently.

## Algorithm

Use the 3-axis greedy merge algorithm with a 2D mask per slice.

### Per-Axis Sweep

For each axis `d` in `{X, Y, Z}`:

1. Define `u` and `v` as the other two axes.
2. For slice index `i` from `-1` to `size[d] - 1`:
   - Build a 2D mask `mask[u][v]` representing exposed faces.
   - Compare voxel `a` at slice `i` and voxel `b` at slice `i + 1`.
   - If `a != b`, a face exists at the boundary.
   - Store the material and face normal direction.
3. Greedily merge adjacent cells in the mask into rectangles.
4. Emit one quad per merged rectangle.
5. Clear the merged region and continue.

### Mask Values

Each mask cell should include:

- Material id
- Normal direction
- Empty when no face exists

### Quad Emission

Each quad should include:

- 4 vertices
- 2 triangles
- Normal per face
- Material color or index
- Optional planar UVs

## Expected Outcomes

- Orders of magnitude fewer faces than naive cube emission.
- Suitable for large voxel models.
- Clean, readable blocky aesthetic.
