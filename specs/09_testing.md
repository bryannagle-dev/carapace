# Testing Strategy

## Unit Tests

- Voxel primitives correctness.
- `.vxm` round-trip save and load.
- Greedy mesher produces non-empty mesh when voxels exist.

## Golden Test

- Seed 123 produces a stable voxel hash.
- Store expected hash in the repo.
- Fail test if hash changes unexpectedly.

## Scope

- Tests are pure C# where possible.
- Generator and viewer integration tests are optional for MVP.
