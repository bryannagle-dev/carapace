#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${ROOT_DIR}/tools/godot/Godot_v4.6-stable_mono_linux_x86_64/Godot_v4.6-stable_mono_linux.x86_64"
OUTPUT_DIR="${ROOT_DIR}/output"
OUT_FILE="${OUTPUT_DIR}/torso_only.vxm"

if [ ! -x "${GODOT_BIN}" ]; then
  echo "Godot binary not found. Run ./get_godot.sh first." >&2
  exit 1
fi

mkdir -p "${OUTPUT_DIR}"

if [ ! -f "${ROOT_DIR}/src/Generator/VoxelGenerator.sln" ]; then
  dotnet new sln -n VoxelGenerator -o "${ROOT_DIR}/src/Generator"
  dotnet sln "${ROOT_DIR}/src/Generator/VoxelGenerator.sln" add "${ROOT_DIR}/src/Generator/VoxelGenerator.csproj"
fi

if [ ! -f "${ROOT_DIR}/src/Viewer/VoxelViewer.sln" ]; then
  dotnet new sln -n VoxelViewer -o "${ROOT_DIR}/src/Viewer"
  dotnet sln "${ROOT_DIR}/src/Viewer/VoxelViewer.sln" add "${ROOT_DIR}/src/Viewer/VoxelViewer.csproj"
fi

dotnet build "${ROOT_DIR}/src/Generator/VoxelGenerator.csproj" -v minimal
dotnet build "${ROOT_DIR}/src/Viewer/VoxelViewer.csproj" -v minimal

"${GODOT_BIN}" --headless --path "${ROOT_DIR}/src/Generator" -- \
  --seed 123 --out "${OUT_FILE}" --height 48 --torso_voxels 1000 --style chunky

"${GODOT_BIN}" --path "${ROOT_DIR}/src/Viewer" -- --file "${OUT_FILE}"
