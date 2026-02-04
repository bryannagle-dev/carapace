# Validity and Regeneration Rules

## Hard Rules

Generation fails if any of these are true:

- Any limb is disconnected from the torso.
- A part overlaps a forbidden volume except allowed sockets.
- Any voxel cluster is smaller than a minimum size.
- Bounding box height deviates more than 20 percent from target.

## Connectivity Check

- Flood fill from a pelvis voxel.
- Count reachable voxels.
- If reachable count is less than total voxels, the model is invalid.

## Part Sanity Rules

- Head sits above torso top.
- Arms attach within the upper third of the torso.
- Legs attach within the hip volume.
- Feet touch the lowest Y layer.

## Proportion Clamps

- Torso height is 25 to 40 percent of total.
- Leg length is at least torso height.
- Arm length is 70 to 110 percent of torso height.
- Head height is 12 to 18 percent of total.

## Regeneration Loop

```
for (int attempt = 0; attempt < 20; attempt++) {
    Generate(seed + attempt);
    if (IsValid()) return model;
}
throw new GenerationFailure();
```
