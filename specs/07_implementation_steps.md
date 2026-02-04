# Implementation Steps

This sequence moves from MVP to a full humanoid generator.

## Step 0: Repo and Projects

- Create the repo layout.
- Create `Generator` Godot 4 C# project.
- Create `Viewer` Godot 4 C# project.
- Add `VoxelCore` shared code.

Deliverable:

- Viewer runs with empty scene.
- Generator prints "hello" and exits.

## Step 1: VoxelGrid and VXM

- Implement `VoxelGrid` dense storage.
- Implement `VxmCodec.Save` and `VxmCodec.Load`.

Deliverable:

- Generator saves an empty grid.
- Viewer loads it without error.

## Step 2: Torso

- Compute torso bounds above pelvis.
- Fill torso box.
- Optional surface noise or inset/extrude.

Deliverable:

- Generator outputs `torso_only.vxm`.
- Viewer renders torso mesh.

## Step 3: Neck

- Create a short cylinder on torso top.
- Store `NeckTop` attachment point.

Deliverable:

- Torso plus neck.

## Step 4: Head

- Ellipsoid head above the neck.
- Optional underside carve.

Deliverable:

- Torso, neck, and head.

## Step 5: Hips

- Box below torso.
- Store `HipSocketL` and `HipSocketR`.

Deliverable:

- Torso, hips, head.

## Step 6: Shoulders

- Small boxes on torso sides.
- Store `ShoulderSocketL` and `ShoulderSocketR`.

Deliverable:

- Torso with clear arm attachment points.

## Step 7: Upper Arms

- Tapered cylinder shoulder to elbow.
- Mirror across X.

Deliverable:

- Upper arms added.

## Step 8: Forearms and Hands

- Forearm tapered cylinder.
- Hand as small box with optional thumb.
- Mirror across X.

Deliverable:

- Full arms.

## Step 9: Upper Legs

- Tapered cylinder hip to knee.
- Mirror across X.

Deliverable:

- Upper legs added.

## Step 10: Lower Legs and Feet

- Tapered cylinder knee to ankle.
- Foot block with forward toe extension.
- Mirror across X.

Deliverable:

- Full humanoid silhouette.

## Step 11: Cleanup Pass

- Carve armpits and between legs.
- Add small bulges at knees and elbows.
- Enforce proportion ranges and regenerate if invalid.

Deliverable:

- Stable, readable humanoids.
